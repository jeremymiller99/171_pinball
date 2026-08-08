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
    internal static class SmartMouseMenu
    {
        const string Root = "Tools/TelePresent/Smart Mouse/";
        const string EnablePath = Root + "Enable Smart Mouse";
        const string MeasurePath = Root + "Toggle Measurement Mode";
        const string SurfaceSnapPath = Root + "Show Surface Snap Tool";
#if !UNITY_2022_1_OR_NEWER
        const string SurfaceSnapModePath = Root + "Surface Snap Mode";
#endif

        [MenuItem(EnablePath, priority = 0)]
        static void ToggleEnable()
        {
            SmartMouseToolbarActions.ToggleSmartMouseState();
            SceneView.RepaintAll();
        }

        [MenuItem(EnablePath, true)]
        static bool ToggleEnableValidate()
        {
            Menu.SetChecked(EnablePath, SmartMouseSettings.IsSmartMouseEnabled);
            return true;
        }

        [MenuItem(Root + "Manage Prefabs...", priority = 20)] static void OpenPrefabManager() => SmartMousePrefabManagerWindow.ShowWindow();

        [MenuItem(Root + "Manage Folders...", priority = 21)] static void OpenFolderManager() => SmartMouseFolderManagerWindow.ShowWindow();

        [MenuItem(MeasurePath, priority = 40)]
        static void ToggleMeasure()
        {
            bool turningOn = !SmartMouseMeasureTool.IsMeasurementActive;
            if (turningOn && !SmartMouseSettings.IsSmartMouseEnabled)
                SmartMouseToolbarActions.ToggleSmartMouseState();

            SmartMouseMeasureTool.ToggleMeasurementMode();
            SceneView.RepaintAll();
        }

        [MenuItem(MeasurePath, true)]
        static bool ToggleMeasureValidate()
        {
            Menu.SetChecked(MeasurePath, SmartMouseMeasureTool.IsMeasurementActive);
            return true;
        }

#if !UNITY_2022_1_OR_NEWER
        // The scene-view toolbar needs the Overlays API (2021.2+); older editors get the same popup as a menu entry.
        [MenuItem(Root + "Settings...", priority = 42)]
        static void OpenSettings()
        {
            if (SmartMousePopupWindow.IsOpen)
            {
                SmartMousePopupWindow.CloseActive();
                return;
            }

            SceneView view = SceneView.lastActiveSceneView;
            Rect anchor = view != null
                ? new Rect(view.position.x + 8f, view.position.y + 30f, 220f, 0f)
                : new Rect(200f, 200f, 220f, 0f);
            SmartMousePopupWindow.Show(anchor);
        }
#endif

#if !UNITY_2022_1_OR_NEWER
        // Only editors without the Overlays toolbar need this; elsewhere the toolbar button enters the mode.
        [MenuItem(SurfaceSnapModePath, priority = 39)]
        static void ToggleSurfaceSnapMode()
        {
            if (!SmartMouseSurfaceSnapTool.IsActive && !SmartMouseSettings.IsSmartMouseEnabled)
                SmartMouseToolbarActions.ToggleSmartMouseState();

            SmartMouseSurfaceSnapTool.Toggle();
        }

        [MenuItem(SurfaceSnapModePath, true)]
        static bool ToggleSurfaceSnapModeValidate()
        {
            Menu.SetChecked(SurfaceSnapModePath, SmartMouseSurfaceSnapTool.IsActive);
            // SetActive refuses during play mode; the item would still enable the persisted
            // Smart Mouse setting while the mode never turns on.
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
#endif

#if UNITY_2022_1_OR_NEWER
        // Shows or hides the Surface Snap button on the scene-view toolbar, which older editors do not have.
        [MenuItem(SurfaceSnapPath, priority = 41)] static void ToggleSurfaceSnapTool() => SmartMousePopupWindow.ToggleSurfaceSnapVisibility();

        [MenuItem(SurfaceSnapPath, true)]
        static bool ToggleSurfaceSnapToolValidate()
        {
            Menu.SetChecked(SurfaceSnapPath, SmartMouseSettings.ShowSurfaceSnapTool);
            return true;
        }
#endif

        [MenuItem(Root + "Welcome Window", priority = 60)] static void OpenWelcome() => SmartMouseWelcomeWindow.ShowWindow();

        [MenuItem(Root + "Documentation", priority = 61)] static void OpenDocumentation() => Application.OpenURL(SmartMouseLinks.Documentation);
    }
}
