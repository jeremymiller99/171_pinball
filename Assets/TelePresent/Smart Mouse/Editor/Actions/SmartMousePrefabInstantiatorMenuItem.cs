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

namespace TelePresent.SmartMouse
{
    internal static class SmartMousePrefabInstantiatorMenuItem
    {
        const float FallbackSpawnDistance = 10f;

        [SmartMouseContextMenuItem(11)]
        public static void AddPrefabInstantiatorMenuItem(GenericMenu menu, SceneView sceneView, Vector2 mousePosition, Ray worldRay)
        {
            List<GameObject> _prefabs = SmartMouseSettings.LoadObjectList();

            if (_prefabs.Count > 0)
            {
                foreach (var prefab in _prefabs)
                {
                    if (prefab != null)
                    {
                        menu.AddItem(new GUIContent($"Create/Prefab/{prefab.name}"), false, () =>
                        {
                            InstantiatePrefabAtMouse(prefab, worldRay);
                        });
                    }
                }
                menu.AddSeparator("Create/Prefab/");
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Create/Prefab/No prefabs added"));
            }

            menu.AddItem(new GUIContent("Create/Prefab/Manage Prefabs..."), false, () =>
            {
                SmartMousePrefabManagerWindow.ShowWindow();
            });
        }

        static void InstantiatePrefabAtMouse(GameObject prefab, Ray worldRay)
        {
            // Meshes are only hittable through the BVH; make sure it exists (cheap when already built), like Paste does.
            SmartMouseUtility.EnsureBVH();

            Vector3? spawnNormal = null;
            if (SnapAndAlignUtility.TryGetClosestHit(worldRay.origin, worldRay.direction, out Vector3 spawnPosition, out Vector3 hitNormal))
                spawnNormal = Vector3.Dot(hitNormal, worldRay.direction) > 0f ? -hitNormal : hitNormal;
            else
                spawnPosition = worldRay.GetPoint(FallbackSpawnDistance);

            GameObject instance = null;
            try
            {
                Transform stageRoot = SmartMouseCompat.ActiveStageRoot;
                instance = stageRoot != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, stageRoot)
                    : (GameObject)PrefabUtility.InstantiatePrefab(prefab, SmartMouseCompat.ActiveStageScene);
                if (instance == null)
                {
                    Debug.LogWarning($"Smart Mouse: could not instantiate prefab '{prefab.name}'.", prefab);
                    return;
                }

                Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {prefab.name}");
                EditorUtility.SetDirty(instance);
                Selection.activeGameObject = instance;
                SmartMouseUtility.PlaceObjectAtPosition(instance, spawnPosition, spawnNormal);
            }
            catch (System.Exception e)
            {
                if (instance != null)
                    Undo.DestroyObjectImmediate(instance);
                Debug.LogError($"Smart Mouse: could not place prefab '{prefab.name}': {e.Message}", prefab);
            }
        }
    }

    internal class SmartMousePrefabManagerWindow : SmartMouseListManagerWindow<GameObject>
    {
        public static void ShowWindow()
        {
            GetWindow<SmartMousePrefabManagerWindow>("Prefab Manager");
        }

        protected override string HeaderLabel => "Manage Prefabs for Smart Mouse Context Menu";
        protected override string AddButtonLabel => "Add New Prefab Slot";

        protected override List<GameObject> Load() => SmartMouseSettings.LoadObjectList();
        protected override void Save(List<GameObject> items) => SmartMouseSettings.SaveObjectList(items);
        protected override GameObject NewItem() => null;

        protected override GameObject DrawItemRow(GameObject current)
        {
            return (GameObject)EditorGUILayout.ObjectField(current, typeof(GameObject), false);
        }
    }
}
