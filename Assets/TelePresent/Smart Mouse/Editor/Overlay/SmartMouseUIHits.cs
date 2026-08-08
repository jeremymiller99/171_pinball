/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace TelePresent.SmartMouse
{
    internal static class SmartMouseUIHits
    {
        const float RenderOrderBias = 1e-6f;
        const float MaxRenderOrderBias = 0.001f;
        const float AlphaEpsilon = 0.001f;
        static readonly Vector3[] _corners = new Vector3[4];
        static readonly List<RectTransform> _rtBuffer = new List<RectTransform>();

        public static void CheckUIHits(
            Ray ray,
            Canvas[] canvases,
            HashSet<GameObject> uniqueObjects,
            List<(float distance, Vector3 point, GameObject obj)> hits)
        {
            int renderOrder = 0;

            foreach (var canvas in canvases)
            {
                if (canvas == null || !canvas.isActiveAndEnabled)
                    continue;

                canvas.GetComponentsInChildren(false, _rtBuffer);
                foreach (var rt in _rtBuffer)
                {
                    renderOrder++;
                    if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                    if (rt.GetComponentInParent<Canvas>() != canvas) continue;
                    if (!SmartMouseSettings.IncludesLayer(rt.gameObject.layer)) continue;

                    GetUIElementWorldCorners(rt, _corners);
                    if (!SmartMouseGeometry.RayIntersectsQuad(ray, _corners, out Vector3 hitPoint)) continue;

                    if (!SmartMouseSettings.IncludeUIContainers && !IsPerceivable(rt.gameObject)) continue;

                    if (!uniqueObjects.Add(rt.gameObject)) continue;

                    float d = Vector3.Distance(ray.origin, hitPoint);
                    float distance = d * (1f - Mathf.Min(renderOrder * RenderOrderBias, MaxRenderOrderBias));
                    hits.Add((distance, hitPoint, rt.gameObject));
                }
            }
        }

        static bool IsPerceivable(GameObject go)
        {
            if (!go.TryGetComponent<Graphic>(out var graphic)) return false;
            if (!graphic.isActiveAndEnabled || graphic.color.a <= AlphaEpsilon) return false;
            if (graphic.canvasRenderer.cull || graphic.canvasRenderer.GetInheritedAlpha() <= AlphaEpsilon) return false;
            if (HiddenByCanvasGroup(go.transform)) return false;
            return true;
        }

        static bool HiddenByCanvasGroup(Transform t)
        {
            while (t != null)
            {
                if (t.TryGetComponent<CanvasGroup>(out var group))
                {
                    if (group.alpha <= AlphaEpsilon) return true;
                    if (group.ignoreParentGroups) break;
                }
                t = t.parent;
            }
            return false;
        }

        internal static void GetUIElementWorldCorners(RectTransform rt, Vector3[] corners)
        {
            if (TryGetTextBounds(rt, out Bounds b))
            {
                Rect r = rt.rect;
                float xMin = Mathf.Min(r.xMin, b.min.x), xMax = Mathf.Max(r.xMax, b.max.x);
                float yMin = Mathf.Min(r.yMin, b.min.y), yMax = Mathf.Max(r.yMax, b.max.y);
                Matrix4x4 m = rt.localToWorldMatrix;
                corners[0] = m.MultiplyPoint3x4(new Vector3(xMin, yMin, 0f));
                corners[1] = m.MultiplyPoint3x4(new Vector3(xMin, yMax, 0f)); 
                corners[2] = m.MultiplyPoint3x4(new Vector3(xMax, yMax, 0f));
                corners[3] = m.MultiplyPoint3x4(new Vector3(xMax, yMin, 0f)); 
                return;
            }

            rt.GetWorldCorners(corners);
        }

        static readonly Dictionary<Type, Func<Graphic, Bounds>> _textBoundsGetterByType = new Dictionary<Type, Func<Graphic, Bounds>>();

        static bool TryGetTextBounds(RectTransform rt, out Bounds bounds)
        {
            bounds = default;
            if (!rt.TryGetComponent<Graphic>(out var graphic)) return false;

            Type type = graphic.GetType();
            if (!_textBoundsGetterByType.TryGetValue(type, out var getter))
            {
                getter = BuildTextBoundsGetter(type);
                _textBoundsGetterByType[type] = getter;
            }
            if (getter == null) return false;

            bounds = getter(graphic);
            return true;
        }

        static Func<Graphic, Bounds> BuildTextBoundsGetter(Type type)
        {
           
            PropertyInfo prop = type.GetProperty("textBounds", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || prop.PropertyType != typeof(Bounds)) return null;

            bool isTmp = false;
            for (Type t = type; t != null; t = t.BaseType)
                if (t.Namespace == "TMPro") { isTmp = true; break; }
            if (!isTmp) return null;

            ParameterExpression g = Expression.Parameter(typeof(Graphic), "g");
            Expression body = Expression.Property(Expression.Convert(g, type), prop);
            return Expression.Lambda<Func<Graphic, Bounds>>(body, g).Compile();
        }

    }
}
