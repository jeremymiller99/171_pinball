using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Repeats (and bends) a single segment mesh along a Unity Spline by distance.
///
/// Assumptions:
/// - The segment mesh is modeled "forward" along +Y (its length axis).
/// - The segment's cross-section is in X (right) and Z (up) directions.
/// - Spline roll (knot roll) is respected via the spline's up vector.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class RailSplineRepeater : MonoBehaviour
{
    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;
    [Min(0)]
    [SerializeField] private int splineIndex = 0;

    [Header("Segment mesh (modeled along +Y)")]
    [SerializeField] private Mesh segmentMesh;
    [Tooltip("If 0, uses the segment mesh bounds size.y.")]
    [Min(0f)]
    [SerializeField] private float segmentLengthOverrideY = 0f;
    [Tooltip("Applies a scale to the segment mesh while generating.")]
    [SerializeField] private Vector3 segmentScale = Vector3.one;
    [Tooltip("If your segment is modeled along +Y, it's common for the cross-section Z to need flipping to avoid mirrored (inside-out) faces.")]
    [SerializeField] private bool flipCrossSectionZ = true;
    [Tooltip("Roll offset around the spline tangent, in degrees. Use 180 to flip an upside-down segment without rotating the spline.")]
    [SerializeField] private float rollOffsetDegrees = 0f;
    [Tooltip("Offset applied in the segment cross-section plane, in the segment's local units (X = right, Y = up).")]
    [SerializeField] private Vector2 crossSectionOffset = Vector2.zero;

    [Header("Repeat")]
    [Min(0f)]
    [SerializeField] private float startDistance = 0f;
    [Tooltip("If true, the final segment stretches to exactly reach the end of the spline.")]
    [SerializeField] private bool stretchLastSegment = true;
    [Tooltip("Higher = more accurate distance-to-t mapping (and length).")]
    [Min(16)]
    [SerializeField] private int arcLengthSamples = 256;
    [SerializeField] private bool autoRebuild = true;

    [Header("Normals")]
    [Tooltip("If true, ignores provided normals and recalculates normals on the baked mesh.")]
    [SerializeField] private bool recalcNormals = false;
    [Tooltip("Fallback: reverses triangle winding if faces still render inside-out (e.g., due to negative scale in hierarchy).")]
    [SerializeField] private bool invertFaces = false;

    [Header("Collider (optional)")]
    [Tooltip("If enabled, keeps a MeshCollider on this GameObject updated with the baked mesh.")]
    [SerializeField] private bool updateMeshCollider = true;
    [Tooltip("Extra contact offset can reduce snagging on triangle edges (at the cost of slightly 'fatter' collisions).")]
    [Min(0f)]
    [SerializeField] private float meshColliderContactOffset = 0.01f;

    private Mesh _bakedMesh;

    private void OnEnable()
    {
        if (autoRebuild)
            Rebuild();
    }

    private void OnDisable()
    {
        var mc = GetComponent<MeshCollider>();
        if (mc != null && mc.sharedMesh == _bakedMesh)
            mc.sharedMesh = null;

        DestroyBakedMesh();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isActiveAndEnabled)
            return;

        if (autoRebuild)
            Rebuild();
    }
#endif

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (!splineContainer || segmentMesh == null)
            return;

        if (!segmentMesh.isReadable)
        {
            Debug.LogError(
                $"RailSplineRepeater: Segment mesh '{segmentMesh.name}' is not readable. " +
                "Enable Read/Write on the mesh import settings (select the model asset in the Project window -> Model tab -> Read/Write Enabled), " +
                "or use a mesh asset that has Read/Write enabled.",
                this);
            return;
        }

        if (splineIndex < 0 || splineIndex >= splineContainer.Splines.Count)
            return;

        var spline = splineContainer.Splines[splineIndex];
        if (spline == null)
            return;

        int tableSamples = Mathf.Max(16, arcLengthSamples);
        BuildArcLengthTableWorld(spline, splineContainer.transform, tableSamples, out var tTable, out var distTable, out float totalLength);

        float usableLength = Mathf.Max(0f, totalLength - startDistance);
        if (usableLength <= 1e-5f)
            return;

        float segLenMeshY = segmentLengthOverrideY > 0f ? segmentLengthOverrideY : segmentMesh.bounds.size.y;
        if (segLenMeshY <= 1e-5f)
            return;

        float segLenWorld = segLenMeshY * Mathf.Abs(segmentScale.y);
        if (segLenWorld <= 1e-5f)
            return;

        int segmentCount = Mathf.Max(1, Mathf.CeilToInt(usableLength / segLenWorld));

        // Source data
        var srcVerts = segmentMesh.vertices;
        var srcNormals = segmentMesh.normals;
        bool hasSrcNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
        var srcUV = segmentMesh.uv;
        bool hasSrcUV = srcUV != null && srcUV.Length == srcVerts.Length;
        var srcTriangles = segmentMesh.triangles;

        // Robust Y mapping even if the mesh pivot isn't at y=0.
        Bounds b = segmentMesh.bounds;
        float yMin = b.min.y;
        float yLen = Mathf.Max(1e-5f, b.size.y);

        int vPerSeg = srcVerts.Length;
        int triPerSeg = srcTriangles.Length;

        int totalVerts = vPerSeg * segmentCount;
        int totalTris = triPerSeg * segmentCount;

        var dstVerts = new Vector3[totalVerts];
        var dstNormals = (!recalcNormals && hasSrcNormals) ? new Vector3[totalVerts] : null;
        var dstUV = hasSrcUV ? new Vector2[totalVerts] : null;
        var dstTriangles = new int[totalTris];

        for (int seg = 0; seg < segmentCount; seg++)
        {
            float segStart = startDistance + seg * segLenWorld;

            float segThisLen = segLenWorld;
            if (seg == segmentCount - 1 && stretchLastSegment)
            {
                float remaining = (startDistance + usableLength) - segStart;
                segThisLen = Mathf.Max(1e-5f, remaining);
            }

            int vBase = seg * vPerSeg;
            int tBase = seg * triPerSeg;

            // Triangles
            if (!invertFaces)
            {
                for (int ti = 0; ti < triPerSeg; ti++)
                    dstTriangles[tBase + ti] = vBase + srcTriangles[ti];
            }
            else
            {
                // Swap winding (0,1,2) -> (1,0,2)
                for (int ti = 0; ti < triPerSeg; ti += 3)
                {
                    int a = srcTriangles[ti + 0];
                    int bTri = srcTriangles[ti + 1];
                    int c = srcTriangles[ti + 2];
                    dstTriangles[tBase + ti + 0] = vBase + bTri;
                    dstTriangles[tBase + ti + 1] = vBase + a;
                    dstTriangles[tBase + ti + 2] = vBase + c;
                }
            }

            // Vertices
            for (int vi = 0; vi < vPerSeg; vi++)
            {
                Vector3 v = srcVerts[vi];

                // Distance along spline for this vertex
                float y01 = Mathf.Clamp01((v.y - yMin) / yLen);
                float d = segStart + (y01 * segThisLen);
                float t = DistanceToT(d, tTable, distTable, totalLength);

                SampleFrameWorld(spline, splineContainer.transform, t, out Vector3 p, out Vector3 tangent, out Vector3 up, out Vector3 right);

                // Cross-section offset (x,z) into spline frame, with optional scaling.
                if (Mathf.Abs(rollOffsetDegrees) > 1e-6f)
                {
                    var rollQ = Quaternion.AngleAxis(rollOffsetDegrees, tangent);
                    right = rollQ * right;
                    up = rollQ * up;
                }

                float x = (v.x + crossSectionOffset.x) * segmentScale.x;
                float z = (v.z + crossSectionOffset.y) * segmentScale.z * (flipCrossSectionZ ? -1f : 1f);
                Vector3 worldV = p + (right * x) + (up * z);

                dstVerts[vBase + vi] = transform.InverseTransformPoint(worldV);

                if (dstNormals != null)
                {
                    Vector3 n = srcNormals[vi];
                    float nz = n.z * (flipCrossSectionZ ? -1f : 1f);
                    Vector3 worldN = (right * n.x) + (up * nz) + (tangent * n.y);
                    dstNormals[vBase + vi] = transform.InverseTransformDirection(worldN).normalized;
                }

                if (dstUV != null)
                    dstUV[vBase + vi] = srcUV[vi];
            }
        }

        DestroyBakedMesh();

        _bakedMesh = new Mesh
        {
            name = $"{segmentMesh.name}_RailBaked"
        };
        _bakedMesh.indexFormat = (totalVerts > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        _bakedMesh.vertices = dstVerts;
        _bakedMesh.triangles = dstTriangles;

        if (dstUV != null)
            _bakedMesh.uv = dstUV;

        if (dstNormals != null)
            _bakedMesh.normals = dstNormals;
        else
            _bakedMesh.RecalculateNormals();

        _bakedMesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _bakedMesh;

        if (updateMeshCollider)
            UpdateOrCreateMeshCollider();
    }

    private void UpdateOrCreateMeshCollider()
    {
        var mc = GetComponent<MeshCollider>();
        if (mc == null)
            mc = gameObject.AddComponent<MeshCollider>();

        mc.convex = false;
        mc.contactOffset = meshColliderContactOffset;
        mc.cookingOptions =
            MeshColliderCookingOptions.EnableMeshCleaning |
            MeshColliderCookingOptions.WeldColocatedVertices |
            MeshColliderCookingOptions.CookForFasterSimulation;

        // Force recook
        mc.sharedMesh = null;
        mc.sharedMesh = _bakedMesh;
    }

    private void DestroyBakedMesh()
    {
        if (_bakedMesh == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(_bakedMesh);
        else
            Destroy(_bakedMesh);
#else
        Destroy(_bakedMesh);
#endif
        _bakedMesh = null;
    }

    private static void BuildArcLengthTableWorld(
        Spline spline,
        Transform splineTransform,
        int samples,
        out float[] tTable,
        out float[] distTable,
        out float totalLength)
    {
        int n = Mathf.Max(2, samples);
        tTable = new float[n + 1];
        distTable = new float[n + 1];

        float3 p0Local = SplineUtility.EvaluatePosition(spline, 0f);
        Vector3 p0World = splineTransform.TransformPoint((Vector3)p0Local);

        tTable[0] = 0f;
        distTable[0] = 0f;

        float accum = 0f;
        Vector3 prev = p0World;

        for (int i = 1; i <= n; i++)
        {
            float t = i / (float)n;
            float3 pLocal = SplineUtility.EvaluatePosition(spline, t);
            Vector3 pWorld = splineTransform.TransformPoint((Vector3)pLocal);

            accum += Vector3.Distance(prev, pWorld);
            prev = pWorld;

            tTable[i] = t;
            distTable[i] = accum;
        }

        totalLength = accum;
    }

    private static float DistanceToT(float d, float[] tTable, float[] distTable, float totalLength)
    {
        if (d <= 0f)
            return 0f;
        if (d >= totalLength)
            return 1f;

        int lo = 0;
        int hi = distTable.Length - 1;

        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (distTable[mid] <= d)
                lo = mid;
            else
                hi = mid;
        }

        float d0 = distTable[lo];
        float d1 = distTable[hi];
        float t0 = tTable[lo];
        float t1 = tTable[hi];

        float a = (Mathf.Abs(d1 - d0) <= 1e-6f) ? 0f : Mathf.InverseLerp(d0, d1, d);
        return Mathf.LerpUnclamped(t0, t1, a);
    }

    private static void SampleFrameWorld(
        Spline spline,
        Transform splineTransform,
        float t,
        out Vector3 position,
        out Vector3 tangent,
        out Vector3 up,
        out Vector3 right)
    {
        SplineUtility.Evaluate(spline, t, out float3 pLocal, out float3 tanLocal, out float3 upLocal);

        position = splineTransform.TransformPoint((Vector3)pLocal);
        tangent = splineTransform.TransformDirection((Vector3)tanLocal).normalized;
        up = splineTransform.TransformDirection((Vector3)upLocal).normalized;

        // Orthonormalize
        right = Vector3.Cross(up, tangent).normalized;
        up = Vector3.Cross(tangent, right).normalized;
    }
}

