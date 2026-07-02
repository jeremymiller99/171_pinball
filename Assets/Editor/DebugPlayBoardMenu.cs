using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor menu that jumps straight into a board in Play mode, skipping the main
/// menu and run-selector. Select a BoardDefinition asset in the Project window
/// (ideally one whose scene contains BoardSections), then run the menu; it stores
/// the choice, opens GameplayCore, and enters Play mode. <see cref="RunFlowController"/>
/// plus <see cref="DebugBoardBootstrap"/> do the rest. Pair with the F8 "force shop"
/// hotkey to reach the component-group shop in a couple of seconds.
/// </summary>
public static class DebugPlayBoardMenu
{
    private const string MenuPath = "Tools/Debug/Play Selected Board (skip menu)";
    private const string GameplayCoreSceneName = "GameplayCore";

    [MenuItem(MenuPath, priority = 0)]
    private static void PlaySelectedBoard()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DebugPlayBoardMenu] Already in (or entering) Play mode.");
            return;
        }

        BoardDefinition board = ResolveBoard();
        if (board == null)
        {
            EditorUtility.DisplayDialog(
                "Play Selected Board",
                "Select a BoardDefinition asset in the Project window first " +
                "(ideally one whose scene contains BoardSections), then run this menu again.",
                "OK");
            return;
        }

        string boardPath = AssetDatabase.GetAssetPath(board);
        string guid = AssetDatabase.AssetPathToGUID(boardPath);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogError($"[DebugPlayBoardMenu] Could not resolve a GUID for '{board.name}'.");
            return;
        }

        string corePath = FindScenePath(GameplayCoreSceneName);
        if (string.IsNullOrEmpty(corePath))
        {
            Debug.LogError($"[DebugPlayBoardMenu] Could not find a scene named '{GameplayCoreSceneName}'.");
            return;
        }

        // Prompt to save any unsaved scene edits before we switch scenes.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorPrefs.SetString(DebugBoardBootstrap.BoardGuidPrefKey, guid);
        EditorSceneManager.OpenScene(corePath, OpenSceneMode.Single);

        Debug.Log($"[DebugPlayBoardMenu] Booting into '{board.name}' via {GameplayCoreSceneName}.");
        EditorApplication.isPlaying = true;
    }

    private static BoardDefinition ResolveBoard()
    {
        // Prefer the current Project-window selection if it's a BoardDefinition.
        if (Selection.activeObject is BoardDefinition selected) return selected;

        // Otherwise reuse the last board that was booted this way.
        string guid = EditorPrefs.GetString(DebugBoardBootstrap.BoardGuidPrefKey, string.Empty);
        if (string.IsNullOrEmpty(guid)) return null;

        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<BoardDefinition>(path);
    }

    private static string FindScenePath(string sceneName)
    {
        // Prefer a Build Settings entry (that's what actually loads additively at runtime).
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (!string.IsNullOrEmpty(s.path) &&
                Path.GetFileNameWithoutExtension(s.path) == sceneName)
            {
                return s.path;
            }
        }

        // Fall back to any matching scene asset in the project.
        foreach (string guid in AssetDatabase.FindAssets($"t:Scene {sceneName}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == sceneName) return path;
        }

        return null;
    }
}
