/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

namespace TelePresent.SmartMouse
{

    [Serializable]
    internal class SmartMouse_EditorStartupHelper : ScriptableObject
    {
        private static SmartMouse_EditorStartupHelper _singletonInstance;

        public static SmartMouse_EditorStartupHelper Singleton
        {
            get
            {
                if (_singletonInstance == null)
                {
                    _singletonInstance = Resources.Load<SmartMouse_EditorStartupHelper>("SmartMouse_EditorStartupHelper");
                    if (_singletonInstance == null)
                    {
                        _singletonInstance = CreateInstance<SmartMouse_EditorStartupHelper>();
                    }
                }
                return _singletonInstance;
            }
        }

        [SerializeField] private bool displayWelcomeMessageOnLaunch = true;
        [SerializeField] private bool firstInitialization = true;

        public static bool DisplayWelcomeOnLaunch
        {
            get => Singleton.displayWelcomeMessageOnLaunch;
            set
            {
                if (value != Singleton.displayWelcomeMessageOnLaunch)
                {
                    Singleton.displayWelcomeMessageOnLaunch = value;
                    PersistStartupPreferences();
                }
            }
        }

        public static bool FirstInitialization
        {
            get => Singleton.firstInitialization;
            set
            {
                if (value != Singleton.firstInitialization)
                {
                    Singleton.firstInitialization = value;
                    PersistStartupPreferences();
                }
            }
        }

        public static void PersistStartupPreferences()
        {
            if (!AssetDatabase.Contains(Singleton))
            {
                var temporaryCopy = CreateInstance<SmartMouse_EditorStartupHelper>();
                EditorUtility.CopySerialized(Singleton, temporaryCopy);

                string assetPath = ResolveAssetPath();

                _singletonInstance = Resources.Load<SmartMouse_EditorStartupHelper>("SmartMouse_EditorStartupHelper");
                if (_singletonInstance == null)
                {
                    AssetDatabase.CreateAsset(temporaryCopy, assetPath);
                    AssetDatabase.Refresh();
                    _singletonInstance = temporaryCopy;
                    return;
                }
                EditorUtility.CopySerialized(temporaryCopy, _singletonInstance);
            }
            EditorUtility.SetDirty(Singleton);
        }


        private static string ResolveAssetPath()
        {
            const string fallback = "Assets/TelePresent/Smart Mouse/Editor/Resources/SmartMouse_EditorStartupHelper.asset";

            string[] guids = AssetDatabase.FindAssets("SmartMouse_EditorStartupHelper t:Script");
            if (guids.Length == 0) return fallback;

            string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            string dir = System.IO.Path.GetDirectoryName(scriptPath);
            if (string.IsNullOrEmpty(dir)) return fallback;
            dir = dir.Replace("\\", "/");

            string resourcesDir = dir + "/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesDir))
                AssetDatabase.CreateFolder(dir, "Resources");

            return resourcesDir + "/SmartMouse_EditorStartupHelper.asset";
        }
    }
}
#endif