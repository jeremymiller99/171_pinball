/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEngine;
using System.Collections.Generic;
namespace TelePresent.SmartMouse
{
    internal class SmartMouseBVHNode
    {
        public UnityEngine.Bounds Bounds;
        public Vector3 Center;       // cached Bounds.center, so the median-split build never recomputes it
        public List<GameObject> Objects;
        public SmartMouseBVHNode LeftChild;
        public SmartMouseBVHNode RightChild;

        public SmartMouseBVHNode(GameObject gameObject)
        {
            if (gameObject != null)
            {
                Objects = new List<GameObject> { gameObject };
                Bounds = CalculateAccurateBounds(gameObject);
            }
            else
            {
                Objects = new List<GameObject>();
                Bounds = new UnityEngine.Bounds(Vector3.zero, Vector3.zero);
            }
            Center = Bounds.center;
        }

        public SmartMouseBVHNode(SmartMouseBVHNode left, SmartMouseBVHNode right, UnityEngine.Bounds bounds)
        {
            LeftChild = left;
            RightChild = right;
            Bounds = bounds;
            Center = bounds.center;
        }

        UnityEngine.Bounds CalculateAccurateBounds(GameObject obj)
        {
            if (obj == null) return new UnityEngine.Bounds(Vector3.zero, Vector3.zero);

            if (obj.TryGetComponent<MeshFilter>(out MeshFilter meshFilter)
                && meshFilter.sharedMesh != null
                && obj.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer)
                && meshRenderer.enabled && meshRenderer.gameObject.activeInHierarchy && !meshRenderer.forceRenderingOff)
            {
                return meshRenderer.bounds;
            }

            else if (obj.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer skinnedMeshRenderer)
                && skinnedMeshRenderer.sharedMesh != null
                && skinnedMeshRenderer.enabled && skinnedMeshRenderer.gameObject.activeInHierarchy &&
                !skinnedMeshRenderer.forceRenderingOff)
            {
                return skinnedMeshRenderer.bounds;
            }
            else
            {
                return new UnityEngine.Bounds(obj.transform.position, Vector3.zero);
            }
        }

        public bool IsLeaf => LeftChild == null && RightChild == null;

        // Appends each object's nearest surface hit (one per object), non-allocating and unsorted.
        public void TryRaycast(Ray ray, List<(Vector3, GameObject)> results)
        {
            if (!Bounds.IntersectRay(ray, out float _))
                return;

            foreach (GameObject obj in Objects)
            {
                if (obj == null) continue;

                if (obj.TryGetComponent<MeshFilter>(out MeshFilter meshFilter) &&
                         obj.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer) &&
                         meshRenderer.enabled && meshRenderer.gameObject.activeInHierarchy &&
                         !meshRenderer.forceRenderingOff)
                {
                    Mesh mesh = meshFilter.sharedMesh;
                    if (mesh != null)
                    {
                        Transform transform = meshFilter.transform;
                        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
                        Ray localRay = new Ray(worldToLocal.MultiplyPoint(ray.origin), worldToLocal.MultiplyVector(ray.direction));

                        if (SmartMouseController.RayIntersectsMesh(localRay, mesh, out Vector3 localHit))
                            results.Add((transform.TransformPoint(localHit), obj));
                    }
                }
                else if (obj.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer skinnedMeshRenderer) &&
                         skinnedMeshRenderer.enabled && skinnedMeshRenderer.gameObject.activeInHierarchy &&
                         !skinnedMeshRenderer.forceRenderingOff)
                {
                    // dist > 0 keeps a camera inside the AABB from hitting for every cursor position.
                    if (skinnedMeshRenderer.sharedMesh != null &&
                        skinnedMeshRenderer.bounds.IntersectRay(ray, out float dist) && dist > 0f)
                        results.Add((ray.GetPoint(dist), obj));
                }
            }
        }

        public void Clear()
        {
            Objects?.Clear();
            LeftChild?.Clear();
            RightChild?.Clear();
            LeftChild = null;
            RightChild = null;
        }
    }
}
