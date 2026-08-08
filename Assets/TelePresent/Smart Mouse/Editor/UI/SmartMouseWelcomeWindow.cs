/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;
using UnityEditor;
using UnityEngine;

namespace TelePresent.SmartMouse
{
    internal class SmartMouseWelcomeWindow : WelcomeWindowBase
    {
        const string ShownThisSessionKey = "SmartMouse_WelcomeShownThisSession";
        const string LaunchSampledKey = "SmartMouse_WelcomeLaunchSampled";
        const string LaunchedRecentlyKey = "SmartMouse_WelcomeLaunchedRecently";

        static bool _launchedRecently;

        [InitializeOnLoadMethod]
        static void Init()
        {
            if (!SessionState.GetBool(LaunchSampledKey, false))
            {
                SessionState.SetBool(LaunchSampledKey, true);
                SessionState.SetBool(LaunchedRecentlyKey, EditorApplication.timeSinceStartup < 30f);
            }
            _launchedRecently = SessionState.GetBool(LaunchedRecentlyKey, false);
            EditorApplication.update -= TriggerWelcomeScreen;
            EditorApplication.update += TriggerWelcomeScreen;
        }

        static void TriggerWelcomeScreen()
        {
            if (SessionState.GetBool(ShownThisSessionKey, false))
            {
                EditorApplication.update -= TriggerWelcomeScreen;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (SmartMouse_EditorStartupHelper.FirstInitialization)
            {
                SmartMouse_EditorStartupHelper.FirstInitialization = false;
                EditorApplication.delayCall += ShowWindow;
            }
            else if (SmartMouse_EditorStartupHelper.DisplayWelcomeOnLaunch && _launchedRecently)
            {
                EditorApplication.delayCall += ShowWindow;
            }


            SessionState.SetBool(ShownThisSessionKey, true);
            EditorApplication.update -= TriggerWelcomeScreen;
        }

        public static void ShowWindow()
        {
            GetWindow<SmartMouseWelcomeWindow>("Welcome Window").Show();
        }

        protected override WelcomeWindowConfig BuildConfig()
        {
            return new WelcomeWindowConfig
            {
                Title = "Thank you for choosing\nSmart Mouse!",
                Intro = "I hope this tool speeds up your editor workflow.\n" +
                        "Below you'll find helpful resources and ways to reach out.",
                PrimaryAction = new WelcomeAction
                {
                    Label = "Enable Smart Mouse",
                    DoneLabel = "Smart Mouse is enabled",
                    IsDone = () => SmartMouseSettings.IsSmartMouseEnabled,
                    Invoke = () => { if (!SmartMouseSettings.IsSmartMouseEnabled) SmartMouseToolbarActions.ToggleSmartMouseState(); },
                    // The toolbar and the Tools menu both toggle this behind the window's back.
                    Subscribe = h => SmartMouseSettings.EnabledChanged += h,
                    Unsubscribe = h => SmartMouseSettings.EnabledChanged -= h
                },
                Links =
                {
                    WelcomeLink.Url("Documentation",
                        "Learn about features, setup, and best practices.",
                        SmartMouseLinks.Documentation, LoadDocsIcon()),
                    WelcomeLink.Url("Join Discord",
                        "Ask questions, troubleshoot, and share your work.",
                        SmartMouseLinks.Discord, LoadDiscordIcon(), pulse: true)
                },
                NewsUrl = SmartMouseLinks.News,
                Footer = $"© {DateTime.Now.Year} TelePresent Games",
                GetShowOnStartup = () => SmartMouse_EditorStartupHelper.DisplayWelcomeOnLaunch,
                SetShowOnStartup = value => SmartMouse_EditorStartupHelper.DisplayWelcomeOnLaunch = value,
                MinSize = new Vector2(500, 660),
                StyleSheetGuid = "f2ee43647ef6044429ad6d4356ddf7df",
                MotionStyleSheetGuid = "eea3c16c4baf345cb9c7e51eaacd291a"
            };
        }

        static Texture2D LoadDocsIcon()
        {
            Texture2D bundled = LoadBundledIcon("documentation");
            if (bundled != null) return bundled;
            return EditorGUIUtility.IconContent("_Help").image as Texture2D;
        }

        static Texture2D LoadDiscordIcon()
        {
            return LoadBundledIcon("discord");
        }

        static Texture2D LoadBundledIcon(string fileName)
        {
            string expectedGuid = string.Equals(fileName, "documentation", StringComparison.OrdinalIgnoreCase)
                ? "88b786c160f6d4c418e322ae792b32cc"
                : string.Equals(fileName, "discord", StringComparison.OrdinalIgnoreCase)
                    ? "58d3868674a5244c2a78eb687f7a04c5"
                    : null;
            if (!string.IsNullOrEmpty(expectedGuid))
            {
                string expectedPath = AssetDatabase.GUIDToAssetPath(expectedGuid);
                Texture2D expected = string.IsNullOrEmpty(expectedPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<Texture2D>(expectedPath);
                if (expected != null) return expected;
            }

            foreach (string guid in AssetDatabase.FindAssets($"{fileName} t:Texture2D"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), fileName, StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }
    }
}
