/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TelePresent.SmartMouse
{

    internal static class GroupUtility
    {
        [SmartMouseContextMenuItem(24)]
        public static void RegisterGroupMenuItems(GenericMenu menu, SceneView sceneView, Vector2 mousePosition, Ray worldRay)
        {
            if (SmartMouseCompat.EditableSceneSelection().Length > 1)
            {
                menu.AddItem(new GUIContent("Selection/Group Selected Objects"), false, GroupSelectedObjects);
                menu.AddItem(new GUIContent("Selection/Parent to Last"), false, ParentObjectsToLast);
            }
        }

        static bool CanReparent(GameObject obj)
        {
            if (PrefabUtility.IsPartOfNonAssetPrefabInstance(obj) &&
                !PrefabUtility.IsOutermostPrefabInstanceRoot(obj))
            {
                Debug.LogWarning($"Smart Mouse: '{obj.name}' is part of a prefab instance and cannot be moved on its own. Use the prefab root, or unpack the instance first.", obj);
                return false;
            }
            return true;
        }

        public static void GroupSelectedObjects()
        {
            GameObject[] selectedObjects = SmartMouseCompat.EditableSceneSelection(topLevelOnly: true);
            selectedObjects = System.Array.FindAll(selectedObjects, CanReparent);
            if (selectedObjects.Length == 0) return;

            var byScene = new Dictionary<Scene, List<GameObject>>();
            foreach (GameObject obj in selectedObjects)
            {
                if (!byScene.TryGetValue(obj.scene, out List<GameObject> objects))
                {
                    objects = new List<GameObject>();
                    byScene.Add(obj.scene, objects);
                }
                objects.Add(obj);
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Group Selected Objects");
            var groups = new List<GameObject>(byScene.Count);

            try
            {
                foreach (KeyValuePair<Scene, List<GameObject>> pair in byScene)
                {
                    Vector3 averagePosition = Vector3.zero;
                    foreach (GameObject obj in pair.Value)
                        averagePosition += obj.transform.position;
                    averagePosition /= pair.Value.Count;

                    GameObject group = new GameObject("Group");
                    Undo.RegisterCreatedObjectUndo(group, "Group Selected Objects");
                    if (group.scene != pair.Key)
                        Undo.MoveGameObjectToScene(group, pair.Key, "Group Selected Objects");
                    Transform stageRoot = SmartMouseCompat.ActiveStageRoot;
                    if (stageRoot != null && stageRoot.gameObject.scene == pair.Key)
                        Undo.SetTransformParent(group.transform, stageRoot, "Group Selected Objects");
                    group.transform.position = averagePosition;

                    foreach (GameObject obj in pair.Value)
                        Undo.SetTransformParent(obj.transform, group.transform, "Group Selected Objects");
                    groups.Add(group);
                }

                Selection.objects = groups.ToArray();
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        public static void ParentObjectsToLast()
        {
            GameObject[] selectedObjects = SmartMouseCompat.EditableSceneSelection(topLevelOnly: false);
            if (selectedObjects.Length < 2) return;

            GameObject lastSelected = Selection.activeGameObject;
            if (lastSelected == null || System.Array.IndexOf(selectedObjects, lastSelected) < 0)
            {
                Debug.LogWarning("Smart Mouse: 'Parent to Last' needs the active object to be an editable scene object.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Parent Objects to Last");
            bool skippedScene = false;
            bool skippedCycle = false;

            try
            {
                foreach (GameObject obj in selectedObjects)
                {
                    if (obj == lastSelected) continue;
                    if (!CanReparent(obj)) continue;
                    if (obj.scene != lastSelected.scene)
                    {
                        skippedScene = true;
                        continue;
                    }
                    if (lastSelected.transform.IsChildOf(obj.transform))
                    {
                        skippedCycle = true;
                        continue;
                    }

                    Undo.SetTransformParent(obj.transform, lastSelected.transform, "Parent Objects to Last");
                }

                Selection.activeGameObject = lastSelected;
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            if (skippedScene)
                Debug.LogWarning("Smart Mouse: objects in other scenes were not parented; cross-scene parenting would move them into the active object's scene.");
            if (skippedCycle)
                Debug.LogWarning("Smart Mouse: an object was not parented because that would create a transform cycle.");
        }

    }
}
