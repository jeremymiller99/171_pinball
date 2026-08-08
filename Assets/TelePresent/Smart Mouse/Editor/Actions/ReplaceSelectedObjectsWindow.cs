/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

#if UNITY_2021_2_OR_NEWER
using PrefabStage = UnityEditor.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.SceneManagement.PrefabStageUtility;
#else
using PrefabStage = UnityEditor.Experimental.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.Experimental.SceneManagement.PrefabStageUtility;
#endif

namespace TelePresent.SmartMouse
{
    internal static class ReplaceSelectedObjectsWindow
    {
        static GameObject _replacementObject;
        static bool _keepScale = true;
        static bool _keepRotation = true;

        static readonly SmartMouseScenePanel _window = new SmartMouseScenePanel("Replace Selected Objects");

        public static void ShowWindow()
        {
            if (_window.IsOpen) return;
            LoadPersistedSettings();
            _window.Open(BuildContent(), SmartMouseScenePanel.Corner.TopLeft, new Vector2(12f, 12f), 260f);
        }

        static void LoadPersistedSettings()
        {
            _keepScale = SmartMouseSettings.ReplaceKeepScale;
            _keepRotation = SmartMouseSettings.ReplaceKeepRotation;
            string path = SmartMouseSettings.ReplaceObjectPath;
            _replacementObject = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        [SmartMouseContextMenuItem(23)]
        public static void RegisterReplaceMenuItems(GenericMenu menu, SceneView sceneView, Vector2 mousePosition, Ray worldRay)
        {
            menu.AddSeparator("Selection/");
            if (SmartMouseCompat.EditableSceneSelection(topLevelOnly: true).Length > 0)
                menu.AddItem(new GUIContent("Selection/Replace Selected Objects..."), false, ShowWindow);
            else
                menu.AddDisabledItem(new GUIContent("Selection/Replace Selected Objects..."));
        }

        static VisualElement BuildContent()
        {
            var content = new VisualElement();
            content.Add(SmartMouseUI.Help("Swaps every selected object for instances of the replacement prefab."));

            var section = SmartMouseUI.Section();

            var objectField = new ObjectField("Replacement Object")
            { objectType = typeof(GameObject), allowSceneObjects = false, value = _replacementObject };
            objectField.RegisterValueChangedCallback(evt =>
            {
                _replacementObject = evt.newValue as GameObject;
                SmartMouseSettings.ReplaceObjectPath = _replacementObject != null ? AssetDatabase.GetAssetPath(_replacementObject) : "";
            });
            section.Add(objectField);

            section.Add(SmartMouseUI.SwitchRow("Keep Scale", "Give each replacement the original object's scale.",
                () => _keepScale, v => { _keepScale = v; SmartMouseSettings.ReplaceKeepScale = v; }));
            section.Add(SmartMouseUI.SwitchRow("Keep Rotation", "Give each replacement the original object's rotation.",
                () => _keepRotation, v => { _keepRotation = v; SmartMouseSettings.ReplaceKeepRotation = v; }));

            content.Add(section);
            content.Add(SmartMouseUI.AccentButton("Replace Selected Objects", ReplaceObjects));
            return content;
        }

        static void ReplaceObjects()
        {
            if (_replacementObject == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a replacement object.", "OK");
                return;
            }

            GameObject[] selectedObjects = SmartMouseCompat.EditableSceneSelection(topLevelOnly: true);

            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "No objects selected.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace Selected Objects");

            List<GameObject> newSelection = new List<GameObject>();

            try
            {
                foreach (var obj in selectedObjects)
                {
                    if (obj == null) continue;

                    PrefabStage stage =
                        PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null && obj == stage.prefabContentsRoot)
                    {
                        Debug.LogWarning("Smart Mouse: the root of an open Prefab Stage cannot be replaced. Replace one of its children or edit the prefab asset directly.", obj);
                        continue;
                    }

                    if (PrefabUtility.IsPartOfNonAssetPrefabInstance(obj) &&
                        !PrefabUtility.IsOutermostPrefabInstanceRoot(obj))
                    {
                        Debug.LogWarning($"Smart Mouse: '{obj.name}' is part of a prefab instance and cannot be replaced on its own. Replace the prefab root, or unpack the instance first.", obj);
                        continue;
                    }

                    Transform origParent = obj.transform.parent;
                    GameObject newObject = origParent != null
                        ? (GameObject)PrefabUtility.InstantiatePrefab(_replacementObject, origParent)
                        : (GameObject)PrefabUtility.InstantiatePrefab(_replacementObject, obj.scene);

                    if (newObject == null)
                    {
                        Debug.LogWarning($"Smart Mouse: could not instantiate replacement '{_replacementObject.name}' for '{obj.name}'.", obj);
                        continue;
                    }

                    Undo.RegisterCreatedObjectUndo(newObject, "Replace Object");

                    newObject.transform.position = obj.transform.position;
                    newObject.transform.SetSiblingIndex(obj.transform.GetSiblingIndex());

                    if (_keepScale)
                        newObject.transform.localScale = obj.transform.localScale;

                    if (_keepRotation)
                        newObject.transform.rotation = obj.transform.rotation;

                    Undo.DestroyObjectImmediate(obj);

                    newSelection.Add(newObject);
                }

                if (newSelection.Count > 0)
                    Selection.objects = newSelection.ToArray();
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }
    }
}
