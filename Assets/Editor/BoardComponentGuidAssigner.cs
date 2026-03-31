// Generated with Antigravity by jjmil on 2026-03-29.
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helper that scans all prefabs for
/// <see cref="BoardComponent"/> instances with empty
/// ComponentGuid fields and auto-assigns a GUID.
///
/// Menu: Pinball > Assign Missing Board Component GUIDs
/// </summary>
public static class BoardComponentGuidAssigner
{
    [MenuItem("Pinball/Assign Missing Board Component GUIDs")]
    public static void AssignMissingGuids()
    {
        string[] prefabGuids =
            AssetDatabase.FindAssets("t:Prefab");

        int assignedCount = 0;

        foreach (string assetGuid in prefabGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(assetGuid);

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                continue;
            }

            BoardComponent[] components =
                prefab.GetComponentsInChildren<BoardComponent>(
                    true);

            if (components.Length == 0)
            {
                continue;
            }

            bool modified = false;

            foreach (BoardComponent bc in components)
            {
                if (bc == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(bc.ComponentGuid))
                {
                    continue;
                }

                SerializedObject so =
                    new SerializedObject(bc);

                SerializedProperty guidProp =
                    so.FindProperty("componentGuid");

                if (guidProp == null)
                {
                    continue;
                }

                guidProp.stringValue =
                    System.Guid.NewGuid().ToString();

                so.ApplyModifiedPropertiesWithoutUndo();
                modified = true;
                assignedCount++;
            }

            if (modified)
            {
                EditorUtility.SetDirty(prefab);
            }
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[BoardComponentGuidAssigner] Assigned " +
            $"{assignedCount} GUIDs to BoardComponents.");

        EditorUtility.DisplayDialog(
            "Board Component GUID Assigner",
            $"Assigned {assignedCount} GUIDs.",
            "OK");
    }
}
