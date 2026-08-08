// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-07 (FTUE Phase 0 debug launcher).
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-only launcher that plays the FTUE board without going through the menu. Use
/// <c>Pinball/FTUE/Play FTUE Board</c>: it opens GameplayCore, flags the request, and enters
/// play mode. On the next play session the flag configures a <see cref="GameSession"/> for the
/// FTUE mission, board and ship, so <see cref="RunFlowController"/> loads Board_FTUE instead of
/// bouncing back to the main menu for want of a board.
///
/// Two placement decisions worth knowing before moving this file:
///
/// - It sits under Assets/Scripts rather than Assets/Editor because
///   <see cref="RuntimeInitializeOnLoadMethod"/> is not invoked for editor assemblies, and it is
///   the only hook guaranteed to run before RunFlowController.Start reads the session. Keeping
///   the menu item alongside it also puts the whole file inside Assembly-CSharp, which is what
///   the project's headless compile check actually covers.
/// - The whole file is UNITY_EDITOR-guarded, so it contributes nothing to a player build.
///
/// Temporary scaffolding for Phases 0-4. Phase 5 replaces it with the real boot rule
/// (profile has not completed the FTUE -> load it); delete this file then.
/// </summary>
public static class FtueDebugBoot
{
    private const string menuPath = "Pinball/FTUE/Play FTUE Board";

    // SessionState rather than EditorPrefs: the request is meant for the very next play session,
    // not for every session until someone clears it, and this leaves nothing behind on disk.
    private const string bootRequestKey = "Ftue.DebugBootRequested";

    private const string gameplayCoreScenePath = "Assets/Scenes/Core/GameplayCore.unity";
    private const string boardAssetPath = "Assets/Data/FTUE/Board_FTUE.asset";
    private const string missionAssetPath = "Assets/Data/FTUE/Mission_FTUE.asset";
    private const string shipAssetPath = "Assets/Data/FTUE/Ship_FTUE.asset";

    // Fixed so a tutorial plays the same way every time; the FTUE must not vary by seed.
    private const int ftueSeed = 1337;

    [MenuItem(menuPath)]
    private static void PlayFtueBoard()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[FtueDebugBoot] Already entering or in play mode. "
                + "Stop play mode first, then run this again.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(gameplayCoreScenePath, OpenSceneMode.Single);

        SessionState.SetBool(bootRequestKey, true);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>
    /// Configures the session for the FTUE when the menu item asked for it. BeforeSceneLoad runs
    /// ahead of every Awake and Start in the scene, which is what keeps this ordered correctly
    /// against RunFlowController.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureSessionIfRequested()
    {
        if (!SessionState.GetBool(bootRequestKey, false))
        {
            return;
        }

        // Cleared up front so a plain Play from GameplayCore afterwards behaves normally, and so
        // an exception below cannot strand the flag on and hijack later sessions.
        SessionState.EraseBool(bootRequestKey);

        var board = AssetDatabase.LoadAssetAtPath<BoardDefinition>(boardAssetPath);
        var mission = AssetDatabase.LoadAssetAtPath<ChallengeModeDefinition>(missionAssetPath);
        var ship = AssetDatabase.LoadAssetAtPath<PlayerShipDefinition>(shipAssetPath);

        if (board == null || mission == null || ship == null)
        {
            Debug.LogError("[FtueDebugBoot] Missing FTUE data asset(s). Expected "
                + $"'{boardAssetPath}', '{missionAssetPath}' and '{shipAssetPath}'. "
                + "Playing GameplayCore without an FTUE session.");
            return;
        }

        GameSession session = EnsureSession();
        if (session == null)
        {
            Debug.LogError("[FtueDebugBoot] Could not obtain a GameSession to configure.");
            return;
        }

        session.ConfigureChallenge(mission, board, ship, ftueSeed);

        // GenerateRounds is deliberately not called: nothing in the shipped flow calls it either,
        // and it would mark every fifth round a Devil round for the tutorial.
        Debug.Log($"[FtueDebugBoot] FTUE session configured -> board '{board.boardSceneName}'.");
    }

    /// <summary>
    /// Returns the session singleton, creating it if its own bootstrap has not run yet. The order
    /// between two BeforeSceneLoad hooks is undefined, so this has to work from either side:
    /// GameSession.Awake destroys a duplicate, and its own bootstrap early-outs once Instance is
    /// set, which makes creating it here safe rather than merely likely to work.
    /// </summary>
    private static GameSession EnsureSession()
    {
        if (GameSession.Instance != null)
        {
            return GameSession.Instance;
        }

        var go = new GameObject(nameof(GameSession));
        Object.DontDestroyOnLoad(go);
        go.AddComponent<GameSession>();

        return GameSession.Instance;
    }
}
#endif
