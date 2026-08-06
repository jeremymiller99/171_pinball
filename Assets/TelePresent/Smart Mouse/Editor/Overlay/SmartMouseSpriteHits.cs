/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace TelePresent.SmartMouse
{

    internal static class SmartMouseSpriteHits
    {
        const float AlphaEpsilon = 0.001f;

        const float SortingBias = 1e-6f;
        const float MaxSortingBias = 0.001f;

        static readonly Vector3[] _corners = new Vector3[4];

        public static void CheckSpriteHits(
            Ray ray,
            SpriteRenderer[] sprites,
            HashSet<GameObject> uniqueObjects,
            List<(float distance, Vector3 point, GameObject obj)> hits)
        {
            foreach (var sr in sprites)
            {
                if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy ||
                    sr.forceRenderingOff || sr.sprite == null) continue;
                if (sr.color.a <= AlphaEpsilon) continue;
                if (!SmartMouseSettings.IncludesLayer(sr.gameObject.layer)) continue;

                if (!sr.bounds.IntersectRay(ray)) continue;

                GetSpriteWorldCorners(sr, _corners);
                if (!SmartMouseGeometry.RayIntersectsQuad(ray, _corners, out Vector3 hitPoint)) continue;

                if (!uniqueObjects.Add(sr.gameObject)) continue;

                float bias = Mathf.Clamp(SortingKey(sr) * SortingBias, -MaxSortingBias, MaxSortingBias);
                float distance = Vector3.Distance(ray.origin, hitPoint) * (1f - bias);
                hits.Add((distance, hitPoint, sr.gameObject));
            }
        }

        static float SortingKey(SpriteRenderer sr)
        {
            return SortingLayer.GetLayerValueFromID(sr.sortingLayerID) * 1000f + sr.sortingOrder;
        }

        internal static void GetSpriteWorldCorners(SpriteRenderer sr, Vector3[] corners)
        {
            // localBounds is 2021.2+; older editors run the approximation below.
#if UNITY_2021_2_OR_NEWER
            Bounds b = sr.localBounds;
#else
            // Sprite.bounds already sits around the pivot; the sized draw modes report through size.
            Bounds b;
            if (sr.drawMode == SpriteDrawMode.Simple)
            {
                b = sr.sprite != null ? sr.sprite.bounds : new Bounds(Vector3.zero, Vector3.zero);
            }
            else
            {
                // A sized sprite is laid out around its own pivot, not the transform origin,
                // so the box must not centre on zero.
                Vector2 pivot = new Vector2(0.5f, 0.5f);
                if (sr.sprite != null && sr.sprite.rect.width > 0f && sr.sprite.rect.height > 0f)
                    pivot = new Vector2(sr.sprite.pivot.x / sr.sprite.rect.width,
                                        sr.sprite.pivot.y / sr.sprite.rect.height);
                b = new Bounds(new Vector3((0.5f - pivot.x) * sr.size.x, (0.5f - pivot.y) * sr.size.y, 0f),
                               new Vector3(sr.size.x, sr.size.y, 0f));
            }

            // localBounds is flip-aware; neither branch above is. Flipping mirrors about the
            // transform origin, so only the centre moves.
            if (sr.flipX || sr.flipY)
                b.center = new Vector3(sr.flipX ? -b.center.x : b.center.x,
                                       sr.flipY ? -b.center.y : b.center.y,
                                       b.center.z);
#endif
            Vector3 min = b.min, max = b.max;
            Matrix4x4 m = sr.transform.localToWorldMatrix;
            corners[0] = m.MultiplyPoint3x4(new Vector3(min.x, min.y, 0f));
            corners[1] = m.MultiplyPoint3x4(new Vector3(min.x, max.y, 0f));
            corners[2] = m.MultiplyPoint3x4(new Vector3(max.x, max.y, 0f));
            corners[3] = m.MultiplyPoint3x4(new Vector3(max.x, min.y, 0f));
        }

    }
}
