// Generated with Claude Code (Opus 4.7) by JJ on 2026-04-21.
// First-cut rail generator: samples a SplineContainer once and emits three
// tube meshes in sync — a smooth continuous trough collider and two decorative
// wires offset laterally by a configurable amount.
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[ExecuteAlways]
[RequireComponent(typeof(SplineContainer))]
[AddComponentMenu("Pinball/Pinball Rail")]
public class PinballRail : MonoBehaviour
{
    [Header("Trough (physics)")]
    [SerializeField] private float troughRadius = 0.3f;
    [SerializeField] private float troughWallThickness = 0.1f;
    [SerializeField] private int troughSides = 16;
    [SerializeField] private PhysicsMaterial troughPhysicsMaterial;
    [SerializeField] private bool showTroughMesh = false;
    [SerializeField] private Material troughDebugMaterial;

    [Header("Wires (decorative)")]
    [SerializeField] private float wireRadius = 0.02f;
    [SerializeField] private float wireLateralOffset = 0.15f;
    [SerializeField] private float wireVerticalOffset = 0.1f;
    [SerializeField] private int wireSides = 8;
    [SerializeField] private Material wireMaterial;

    [Header("Sampling")]
    [SerializeField] private float samplesPerUnit = 10f;
    [SerializeField] private int minSampleCount = 8;

    private const string troughName = "_RailTrough";
    private const string leftWireName = "_RailLeftWire";
    private const string rightWireName = "_RailRightWire";

    private SplineContainer _splineContainer;
    private Mesh _troughMesh;
    private Mesh _leftWireMesh;
    private Mesh _rightWireMesh;
    private MeshCollider _troughCollider;
    private MeshRenderer _troughRenderer;
    private bool _rebuildPending;

    private void OnEnable()
    {
        _splineContainer = GetComponent<SplineContainer>();
        EnsureChildren();
        Rebuild();
        Spline.Changed += OnSplineChanged;
    }

    private void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
    }

    private void Update()
    {
        if (_rebuildPending)
        {
            Rebuild();
            _rebuildPending = false;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _rebuildPending = true;
    }
#endif

    private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
    {
        if (_splineContainer == null)
        {
            return;
        }

        if (_splineContainer.Splines != null && _splineContainer.Splines.Contains(spline))
        {
            _rebuildPending = true;
        }
    }

    public void Rebuild()
    {
        if (_splineContainer == null)
        {
            _splineContainer = GetComponent<SplineContainer>();
        }

        if (_splineContainer == null || _splineContainer.Spline == null
            || _splineContainer.Spline.Count < 2)
        {
            return;
        }

        EnsureChildren();

        Spline spline = _splineContainer.Spline;
        float length = spline.GetLength();
        if (length <= 0f)
        {
            return;
        }

        int sampleCount = Mathf.Max(minSampleCount, Mathf.CeilToInt(length * samplesPerUnit));
        List<RailSample> samples = SampleSpline(spline, sampleCount);
        if (samples.Count < 2)
        {
            return;
        }

        BuildThickTube(_troughMesh, samples, 0f, 0f, troughRadius,
            troughRadius + Mathf.Max(troughWallThickness, 0f), troughSides);
        BuildTube(_leftWireMesh, samples, -wireLateralOffset, wireVerticalOffset,
            wireRadius, wireSides);
        BuildTube(_rightWireMesh, samples, wireLateralOffset, wireVerticalOffset,
            wireRadius, wireSides);

        if (_troughCollider != null)
        {
            _troughCollider.sharedMesh = null;
            _troughCollider.sharedMesh = _troughMesh;
            _troughCollider.sharedMaterial = troughPhysicsMaterial;
        }

        if (_troughRenderer != null)
        {
            _troughRenderer.enabled = showTroughMesh;

            if (showTroughMesh && troughDebugMaterial != null)
            {
                _troughRenderer.sharedMaterial = troughDebugMaterial;
            }
        }
    }

    private static List<RailSample> SampleSpline(Spline spline, int sampleCount)
    {
        List<RailSample> samples = new List<RailSample>(sampleCount);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / (sampleCount - 1);
            if (!SplineUtility.Evaluate(spline, t, out float3 pos, out float3 tan, out float3 up))
            {
                continue;
            }

            float3 tangent = math.normalizesafe(tan, new float3(0f, 0f, 1f));
            float3 upVec = math.normalizesafe(up, new float3(0f, 1f, 0f));
            float3 right = math.normalizesafe(math.cross(upVec, tangent), new float3(1f, 0f, 0f));
            upVec = math.normalizesafe(math.cross(tangent, right), new float3(0f, 1f, 0f));

            samples.Add(new RailSample
            {
                position = pos,
                tangent = tangent,
                up = upVec,
                right = right
            });
        }

        return samples;
    }

    private static void BuildTube(Mesh mesh, List<RailSample> samples,
        float lateralOffset, float verticalOffset, float radius, int sides)
    {
        if (mesh == null)
        {
            return;
        }

        mesh.Clear();

        if (samples.Count < 2 || sides < 3 || radius <= 0f)
        {
            return;
        }

        int ringCount = samples.Count;
        int vertexCount = ringCount * sides;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        int[] triangles = new int[(ringCount - 1) * sides * 6];

        for (int i = 0; i < ringCount; i++)
        {
            RailSample sample = samples[i];
            float3 center = sample.position + sample.right * lateralOffset
                + sample.up * verticalOffset;

            for (int s = 0; s < sides; s++)
            {
                float angle = (float)s / sides * math.PI * 2f;
                float3 radial = math.cos(angle) * sample.right + math.sin(angle) * sample.up;
                float3 vertex = center + radial * radius;

                int index = i * sides + s;
                vertices[index] = vertex;
                normals[index] = math.normalize(radial);
            }
        }

        int tri = 0;
        for (int i = 0; i < ringCount - 1; i++)
        {
            for (int s = 0; s < sides; s++)
            {
                int a = i * sides + s;
                int b = i * sides + (s + 1) % sides;
                int c = (i + 1) * sides + s;
                int d = (i + 1) * sides + (s + 1) % sides;

                triangles[tri++] = a;
                triangles[tri++] = b;
                triangles[tri++] = c;

                triangles[tri++] = b;
                triangles[tri++] = d;
                triangles[tri++] = c;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    // Hollow tube (inner + outer wall per sample, bridged along its length).
    // Inner-surface normals face the tube interior so a ball inside is pushed
    // back toward the center. Ends are left open so the ball can roll in one
    // end and out the other.
    private static void BuildThickTube(Mesh mesh, List<RailSample> samples,
        float lateralOffset, float verticalOffset, float innerRadius, float outerRadius,
        int sides)
    {
        if (mesh == null)
        {
            return;
        }

        mesh.Clear();

        if (samples.Count < 2 || sides < 3 || innerRadius <= 0f || outerRadius <= innerRadius)
        {
            return;
        }

        int ringCount = samples.Count;
        int vertexCount = ringCount * sides * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        int outerTriCount = (ringCount - 1) * sides * 6;
        int innerTriCount = (ringCount - 1) * sides * 6;
        int[] triangles = new int[outerTriCount + innerTriCount];

        for (int i = 0; i < ringCount; i++)
        {
            RailSample sample = samples[i];
            float3 center = sample.position + sample.right * lateralOffset
                + sample.up * verticalOffset;

            for (int s = 0; s < sides; s++)
            {
                float angle = (float)s / sides * math.PI * 2f;
                float3 radial = math.cos(angle) * sample.right + math.sin(angle) * sample.up;
                float3 radialNorm = math.normalize(radial);

                int innerIndex = (i * sides + s) * 2;
                int outerIndex = innerIndex + 1;

                vertices[innerIndex] = center + radial * innerRadius;
                vertices[outerIndex] = center + radial * outerRadius;
                normals[innerIndex] = -(Vector3)radialNorm;
                normals[outerIndex] = radialNorm;
            }
        }

        int tri = 0;

        for (int i = 0; i < ringCount - 1; i++)
        {
            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;
                int a = (i * sides + s) * 2 + 1;
                int b = (i * sides + s1) * 2 + 1;
                int c = ((i + 1) * sides + s) * 2 + 1;
                int d = ((i + 1) * sides + s1) * 2 + 1;

                triangles[tri++] = a;
                triangles[tri++] = b;
                triangles[tri++] = c;

                triangles[tri++] = b;
                triangles[tri++] = d;
                triangles[tri++] = c;
            }
        }

        for (int i = 0; i < ringCount - 1; i++)
        {
            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;
                int a = (i * sides + s) * 2;
                int b = (i * sides + s1) * 2;
                int c = ((i + 1) * sides + s) * 2;
                int d = ((i + 1) * sides + s1) * 2;

                triangles[tri++] = a;
                triangles[tri++] = c;
                triangles[tri++] = b;

                triangles[tri++] = b;
                triangles[tri++] = c;
                triangles[tri++] = d;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private void EnsureChildren()
    {
        _troughMesh = EnsureTroughChild(troughName, out _troughCollider, out _troughRenderer);
        _leftWireMesh = EnsureWireChild(leftWireName);
        _rightWireMesh = EnsureWireChild(rightWireName);
    }

    private Mesh EnsureTroughChild(string childName, out MeshCollider collider,
        out MeshRenderer renderer)
    {
        GameObject go = GetOrCreateChild(childName);

        if (!go.TryGetComponent(out MeshFilter filter))
        {
            filter = go.AddComponent<MeshFilter>();
        }

        if (!go.TryGetComponent(out renderer))
        {
            renderer = go.AddComponent<MeshRenderer>();
        }

        if (!go.TryGetComponent(out collider))
        {
            collider = go.AddComponent<MeshCollider>();
        }

        collider.convex = false;

        Mesh mesh = filter.sharedMesh;
        if (mesh == null || !mesh.name.Equals(childName))
        {
            mesh = new Mesh { name = childName };
            mesh.hideFlags = HideFlags.DontSave;
            filter.sharedMesh = mesh;
        }

        return mesh;
    }

    private Mesh EnsureWireChild(string childName)
    {
        GameObject go = GetOrCreateChild(childName);

        if (!go.TryGetComponent(out MeshFilter filter))
        {
            filter = go.AddComponent<MeshFilter>();
        }

        if (!go.TryGetComponent(out MeshRenderer renderer))
        {
            renderer = go.AddComponent<MeshRenderer>();
        }

        if (wireMaterial != null)
        {
            renderer.sharedMaterial = wireMaterial;
        }

        Mesh mesh = filter.sharedMesh;
        if (mesh == null || !mesh.name.Equals(childName))
        {
            mesh = new Mesh { name = childName };
            mesh.hideFlags = HideFlags.DontSave;
            filter.sharedMesh = mesh;
        }

        return mesh;
    }

    private GameObject GetOrCreateChild(string childName)
    {
        Transform existing = transform.Find(childName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject go = new GameObject(childName);
        go.transform.SetParent(transform, worldPositionStays: false);
        return go;
    }

    private struct RailSample
    {
        public float3 position;
        public float3 tangent;
        public float3 up;
        public float3 right;
    }
}
