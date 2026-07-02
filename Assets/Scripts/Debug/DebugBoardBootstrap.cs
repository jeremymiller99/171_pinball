#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only dev helper. When you press Play with the GameplayCore scene open
/// (instead of coming through the main menu), this configures the run session
/// with the board chosen via the "Tools/Debug/Play Selected Board" menu, so the
/// normal <see cref="RunFlowController"/> boots straight into that board.
///
/// It no-ops when a run is already configured (i.e. you started from the menu),
/// so it never interferes with the real flow.
/// </summary>
public static class DebugBoardBootstrap
{
    /// <summary>EditorPrefs key holding the GUID of the BoardDefinition to boot into.</summary>
    public const string BoardGuidPrefKey = "Pinball.Debug.BootBoardGuid";

    private const string GameplayCoreSceneName = "GameplayCore";

    // AfterSceneLoad runs after the first scene's Awake/OnEnable (so GameSession
    // already exists) but before any Start() (so RunFlowController.Start sees the
    // configured session). This only fires for the initial scene load, so it never
    // triggers on the menu -> GameplayCore transition.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConfigureDebugBoardIfNeeded()
    {
        if (SceneManager.GetActiveScene().name != GameplayCoreSceneName) return;

        GameSession session = GameSession.Instance;
        if (session == null) return;

        // Came in through the menu already? Leave the real run plan untouched.
        if (session.GetCurrentBoard() != null) return;

        string guid = EditorPrefs.GetString(BoardGuidPrefKey, string.Empty);
        if (string.IsNullOrEmpty(guid)) return;

        string path = AssetDatabase.GUIDToAssetPath(guid);
        BoardDefinition board = string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<BoardDefinition>(path);

        if (board == null)
        {
            Debug.LogWarning("[DebugBoardBootstrap] No BoardDefinition found for the stored debug GUID. " +
                             "Pick one via Tools/Debug/Play Selected Board.");
            return;
        }

        session.ConfigureQuickRun(new[] { board }, Random.Range(int.MinValue, int.MaxValue));
        Debug.Log($"[DebugBoardBootstrap] Booting straight into '{board.name}' (scene '{board.boardSceneName}').");
    }
}
#endif
