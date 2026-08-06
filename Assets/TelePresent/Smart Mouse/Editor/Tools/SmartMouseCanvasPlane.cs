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
using UnityEngine.UI;

namespace TelePresent.SmartMouse
{
    internal static class SmartMouseCanvasPlane
    {

        public static bool TryGetCanvasPlane(GameObject obj, out Plane plane)
        {
            plane = default;
            if (!SmartMouseUtility.IsUIElement(obj)) return false;

            Canvas canvas = obj.GetComponentInParent<Canvas>();
            Transform source = canvas != null ? canvas.transform : obj.transform;
            plane = new Plane(source.forward, source.position);
            return true;
        }

        public static bool TryProjectOntoPlane(Ray ray, Plane plane, out Vector3 point)
        {
            point = Vector3.zero;
            if (!plane.Raycast(ray, out float distance)) return false;

            point = ray.GetPoint(distance);
            return true;
        }

        public static bool SharesOneCanvas(GameObject[] selection)
        {
            Canvas shared = null;
            bool hasFirst = false;
            foreach (GameObject obj in selection)
            {
                if (obj == null) continue;
                Canvas canvas = obj.GetComponentInParent<Canvas>();
                Canvas root = canvas != null ? canvas.rootCanvas : null;
                if (!hasFirst)
                {
                    shared = root;
                    hasFirst = true;
                }
                else if (root != shared) return false;
            }
            return true;
        }

        public static bool IsDrivenCanvasRoot(GameObject obj)
        {
            return obj != null &&
                   obj.TryGetComponent(out Canvas canvas) &&
                   canvas.isRootCanvas &&
                   canvas.renderMode != RenderMode.WorldSpace;
        }

        public static bool IsLayoutControlled(GameObject obj)
        {
            if (obj == null) return false;

            Transform parent = obj.transform.parent;
            if (parent == null) return false;
            if (!parent.TryGetComponent(out LayoutGroup layout) || !layout.enabled) return false;

            bool hasIgnorer = false;
            foreach (ILayoutIgnorer ignorer in obj.GetComponents<ILayoutIgnorer>())
            {
                hasIgnorer = true;
                if (!ignorer.ignoreLayout) return true;
            }
            return !hasIgnorer;
        }

        static readonly Vector3[] _borderCorners = new Vector3[4];
        static readonly Color BorderColor = new Color(0.30f, 0.80f, 1f, 1f);
        
        public static bool TryDrawBorder(GameObject obj)
        {
            if (!SmartMouseUtility.IsUIElement(obj) || !SmartMouseCompat.IsEditableSceneObject(obj))
                return false;

            Canvas canvas = obj.GetComponentInParent<Canvas>();
            if (canvas == null || !(canvas.transform is RectTransform rect)) return false;

            rect.GetWorldCorners(_borderCorners);

            Color previous = Handles.color;
            Handles.color = BorderColor;
            Handles.DrawLine(_borderCorners[0], _borderCorners[1]);
            Handles.DrawLine(_borderCorners[1], _borderCorners[2]);
            Handles.DrawLine(_borderCorners[2], _borderCorners[3]);
            Handles.DrawLine(_borderCorners[3], _borderCorners[0]);
            Handles.color = previous;
            return true;
        }

        public static bool TryGetSelectionPlanePoint(Ray ray, out Vector3 point)
        {
            point = Vector3.zero;
            foreach (GameObject selected in SmartMouseCompat.EditableSceneSelection())
            {
                if (TryGetCanvasPlane(selected, out Plane plane) && TryProjectOntoPlane(ray, plane, out point))
                    return true;
            }
            return false;
        }
    }
}
