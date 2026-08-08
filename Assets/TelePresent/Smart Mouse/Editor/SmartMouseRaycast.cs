/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEngine;

namespace TelePresent.SmartMouse
{
    /// <summary>
    /// Surface queries using Smart Mouse's own scene detection, for custom context-menu items and editor tools.
    /// </summary>
    public static class SmartMouseRaycast
    {
        /// <summary>Finds the nearest visible surface along the ray, with the normal oriented toward the viewer.</summary>

        public static bool TryGetSurfaceHit(Ray worldRay, out Vector3 point, out Vector3 normal, out GameObject hitObject)
        {
            SmartMouseUtility.EnsureBVH();
            if (!SnapAndAlignUtility.TryGetClosestHit(worldRay.origin, worldRay.direction,
                    out point, out normal, out hitObject))
                return false;

            if (Vector3.Dot(normal, worldRay.direction) > 0f) normal = -normal;
            return true;
        }

        /// <summary>Returns the surface point under the ray, falling back to the 2D work plane or a point fallbackDistance along the ray.</summary>
        public static Vector3 GetPointOrDefault(Ray worldRay, float fallbackDistance = 4f)
        {
            if (TryGetSurfaceHit(worldRay, out Vector3 point, out _, out _)) return point;
            if (SmartMouse2DPlane.TryGetPlacementPoint(worldRay, out Vector3 planePoint)) return planePoint;
            return worldRay.GetPoint(fallbackDistance);
        }
    }
}
