/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEditor;
using UnityEngine;

namespace TelePresent.SmartMouse
{
    internal static class SmartMouse2DPlane
    {
        public static bool IsActive(SceneView sceneView)
        {
            return sceneView != null && sceneView.in2DMode;
        }

        public static bool IsActive()
        {
            SceneView sceneView = SceneView.currentDrawingSceneView;
            if (sceneView == null) sceneView = SceneView.lastActiveSceneView;
            return IsActive(sceneView);
        }

        public static bool TryGetPlanePoint(Ray ray, float planeZ, out Vector3 point, out float distanceAlongRay)
        {
            point = Vector3.zero;
            distanceAlongRay = 0f;
            if (Mathf.Abs(ray.direction.z) <= 1e-6f) return false;

            float t = (planeZ - ray.origin.z) / ray.direction.z;
            if (float.IsNaN(t) || float.IsInfinity(t)) return false;

            distanceAlongRay = t;
            point = ray.GetPoint(t);
            return true;
        }

        public static bool TryGetPlanePoint(Ray ray, float planeZ, out Vector3 point)
        {
            return TryGetPlanePoint(ray, planeZ, out point, out _);
        }

        public static bool TryGetPlacementPoint(Ray ray, out Vector3 point)
        {
            point = Vector3.zero;
            SceneView sceneView = SceneView.currentDrawingSceneView;
            if (sceneView == null) sceneView = SceneView.lastActiveSceneView;
            if (!IsActive(sceneView)) return false;

            if (TryGetPlanePoint(ray, 0f, out point, out float distance) && distance > 0f)
                return true;

            if (sceneView != null
                && TryGetPlanePoint(ray, sceneView.pivot.z, out Vector3 pivotPoint, out float pivotDistance)
                && pivotDistance > 0f)
            {
                point = pivotPoint;
                return true;
            }
            return false;
        }
    }
}
