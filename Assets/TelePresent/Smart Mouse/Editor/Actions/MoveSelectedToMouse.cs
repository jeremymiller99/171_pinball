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
    internal static class MoveSelectedToMouse
    {
        const float DefaultRayDistance = 4f;

        static Vector3? _menuSurfaceNormal;

        [SmartMouseContextMenuItem(20)]
        public static void RegisterMoveMenuItem(GenericMenu menu, SceneView sceneView, Vector2 mousePosition, Ray worldRay)
        {
            Vector3 hitPoint = SmartMouseController.GetRayHitPointOrDefault(worldRay, DefaultRayDistance, out _menuSurfaceNormal);

            if (SmartMouseCompat.EditableSceneSelection(topLevelOnly: true).Length > 0)
            {
                menu.AddItem(new GUIContent("Selection/Move to Mouse"), false, () => MoveSelectedObjects(hitPoint));
            }
        }

        static void MoveSelectedObjects(Vector3 targetPosition)
        {
            GameObject[] selectedObjects = SmartMouseCompat.EditableSceneSelection(topLevelOnly: true);
            if (selectedObjects.Length == 0) return;
            Transform[] selected = System.Array.ConvertAll(selectedObjects, obj => obj.transform);

            Undo.IncrementCurrentGroup();
            int undoGroupIndex = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Move Selected Objects");

            try
            {
                Vector3 centerOfSelection = CalculateCenterPoint(selected);

                Vector3[] offsets = new Vector3[selected.Length];
                for (int i = 0; i < selected.Length; i++)
                    offsets[i] = selected[i] != null ? selected[i].position - centerOfSelection : Vector3.zero;

                for (int i = 0; i < selected.Length; i++)
                {
                    Transform t = selected[i];
                    if (t == null) continue;

                    GameObject obj = t.gameObject;
                    Undo.RegisterCompleteObjectUndo(t, "Move " + obj.name);

                    Vector3 newPosition = targetPosition + offsets[i];

                    bool isPlane = obj.TryGetComponent(out MeshFilter meshFilter) &&
                        meshFilter.sharedMesh != null && meshFilter.sharedMesh.name == "Plane";
                    if (isPlane)
                    {
                        t.position = newPosition;
                    }
                    else
                    {
                        SmartMouseUtility.PlaceObjectAtPosition(obj, newPosition, _menuSurfaceNormal, targetPosition, selectedObjects);
                    }

                    EditorUtility.SetDirty(obj);
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroupIndex);
            }
        }

        static Vector3 CalculateCenterPoint(Transform[] transforms)
        {
            if (transforms == null || transforms.Length == 0) return Vector3.zero;

            int counted = 0;
            Vector3 cumulativePosition = Vector3.zero;
            foreach (Transform t in transforms)
            {
                if (t != null)
                {
                    cumulativePosition += t.position;
                    counted++;
                }
            }

            return counted == 0 ? Vector3.zero : cumulativePosition / counted;
        }
    }
}
