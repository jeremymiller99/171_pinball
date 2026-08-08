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
    internal static class SmartMouseFolderMenuItem
    {
        [SmartMouseContextMenuItem(40)]
        public static void AddFolderMenuItem(GenericMenu menu, SceneView sceneView, Vector2 mousePosition, Ray worldRay)
        {
            menu.AddItem(new GUIContent("Windows/Unity/Lighting Window"), false, () => EditorApplication.ExecuteMenuItem("Window/Rendering/Lighting"));
            menu.AddItem(new GUIContent("Windows/Unity/Inspector"), false, () => EditorApplication.ExecuteMenuItem("Window/General/Inspector"));
            menu.AddItem(new GUIContent("Windows/Unity/Hierarchy"), false, () => EditorApplication.ExecuteMenuItem("Window/General/Hierarchy"));
            menu.AddItem(new GUIContent("Windows/Unity/Project"), false, () => EditorApplication.ExecuteMenuItem("Window/General/Project"));
            menu.AddItem(new GUIContent("Windows/Unity/Console"), false, () => EditorApplication.ExecuteMenuItem("Window/General/Console"));
            menu.AddItem(new GUIContent("Windows/Unity/Game Window"), false, () => EditorApplication.ExecuteMenuItem("Window/General/Game"));
            menu.AddItem(new GUIContent("Windows/Unity/Animator"), false, () => EditorApplication.ExecuteMenuItem("Window/Animation/Animator"));
            menu.AddItem(new GUIContent("Windows/Unity/Animation Window"), false, () => EditorApplication.ExecuteMenuItem("Window/Animation/Animation"));
            menu.AddItem(new GUIContent("Windows/Unity/Profiler"), false, () => EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler"));

            List<string> _folders = SmartMouseSettings.LoadFolderList();
            if (_folders.Count > 0)
            {
                foreach (var folderPath in _folders)
                {
                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        string folderName = System.IO.Path.GetFileName(folderPath);
                        if (string.IsNullOrEmpty(folderName)) folderName = folderPath;

                        menu.AddItem(new GUIContent($"Windows/Custom/{folderName}"), false, () =>
                        {
                            OpenFolder(folderPath);
                        });
                    }
                }
                menu.AddSeparator("Windows/Custom/");
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Windows/Custom/No folders added"));
            }

            menu.AddItem(new GUIContent("Windows/Custom/Manage Folders..."), false, () =>
            {
                SmartMouseFolderManagerWindow.ShowWindow();
            });
        }

        static void OpenFolder(string path)
        {
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj != null)
            {
                EditorUtility.FocusProjectWindow();
                EditorGUIUtility.PingObject(obj);
            }
            else
            {
                Debug.LogWarning($"Smart Mouse: could not find folder at path '{path}'.");
            }
        }
    }

    internal class SmartMouseFolderManagerWindow : SmartMouseListManagerWindow<string>
    {
        public static void ShowWindow()
        {
            GetWindow<SmartMouseFolderManagerWindow>("Folder Manager");
        }

        protected override string HeaderLabel => "Manage Custom Folders for Smart Mouse Context Menu";
        protected override string AddButtonLabel => "Add New Folder Slot";

        protected override List<string> Load() => SmartMouseSettings.LoadFolderList();
        protected override void Save(List<string> items) => SmartMouseSettings.SaveFolderList(items);
        protected override string NewItem() => "";

        protected override void DrawHelp()
        {
            EditorGUILayout.HelpBox("Enter the relative project path (e.g., Assets/MyFolder) or use the Browse button.", MessageType.Info);
        }

        protected override string DrawItemRow(string current)
        {
            current = EditorGUILayout.TextField(current);

            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                RequestExitGUI = true;
                string selectedPath = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    if (selectedPath.StartsWith(Application.dataPath))
                    {
                        current = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                    }
                    else if (selectedPath.Contains("Assets/"))
                    {
                        int index = selectedPath.IndexOf("Assets/");
                        current = selectedPath.Substring(index);
                    }
                    else
                    {
                        Debug.LogWarning("Smart Mouse: the selected folder must be inside the Assets directory.");
                    }
                }
            }

            return current;
        }
    }
}
