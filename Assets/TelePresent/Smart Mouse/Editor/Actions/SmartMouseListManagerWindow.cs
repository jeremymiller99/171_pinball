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
    // Base for the small list-manager windows (prefabs, folders); subclasses supply the per-item editor.
    internal abstract class SmartMouseListManagerWindow<T> : EditorWindow
    {
        List<T> _items;
        Vector2 _scrollPosition;
        
        protected bool RequestExitGUI;

        protected abstract string HeaderLabel { get; }
        protected abstract string AddButtonLabel { get; }

        protected abstract List<T> Load();
        protected abstract void Save(List<T> items);
        protected abstract T NewItem();

        protected abstract T DrawItemRow(T current);

        protected virtual void DrawHelp() { }

        void OnEnable()
        {
            _items = Load();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(HeaderLabel, EditorStyles.boldLabel);
            DrawHelp();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (int i = 0; i < _items.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _items[i] = DrawItemRow(_items[i]);

                if (RequestExitGUI)
                {
                    RequestExitGUI = false;
                    Save(_items);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    _items.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button(AddButtonLabel))
            {
                _items.Add(NewItem());
            }

            if (GUI.changed)
            {
                Save(_items);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Save and Close"))
            {
                Save(_items);
                Close();
            }
        }
    }
}
