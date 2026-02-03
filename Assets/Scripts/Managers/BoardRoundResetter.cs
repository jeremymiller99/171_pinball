using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures board objects return to default each round.
/// Listens to GameRulesManager.RoundStarted and resets all FrenzyExplodable objects in loaded scenes,
/// including inactive ones (since destroyed objects may have been deactivated).
/// </summary>
[DisallowMultipleComponent]
public sealed class BoardRoundResetter : MonoBehaviour
{
    private GameRulesManager _rules;

    private void OnEnable()
    {
        ResolveRules();
        Hook();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unhook();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveRules();
        Hook();
    }

    private void ResolveRules()
    {
        if (_rules != null) return;

#if UNITY_2022_2_OR_NEWER
        _rules = FindFirstObjectByType<GameRulesManager>();
#else
        _rules = FindObjectOfType<GameRulesManager>();
#endif
    }

    private void Hook()
    {
        if (_rules == null) return;
        _rules.RoundStarted -= OnRoundStarted;
        _rules.RoundStarted += OnRoundStarted;
    }

    private void Unhook()
    {
        if (_rules == null) return;
        _rules.RoundStarted -= OnRoundStarted;
    }

    private void OnRoundStarted()
    {
        ResetAllExplodablesInLoadedScenes();
    }

    private static void ResetAllExplodablesInLoadedScenes()
    {
        // Includes inactive objects, which we need because FrenzyExplodable often deactivates the target.
        var all = Resources.FindObjectsOfTypeAll<FrenzyExplodable>();
        for (int i = 0; i < all.Length; i++)
        {
            var e = all[i];
            if (e == null) continue;

            // Ignore prefab assets.
            if (!e.gameObject.scene.IsValid()) continue;
            if (!e.gameObject.scene.isLoaded) continue;

            e.ResetToDefaultState();
        }
    }
}

internal sealed class BoardRoundResetterBootstrapper : MonoBehaviour
{
    private const string GameplayCoreSceneName = "GameplayCore";

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstallIntoLoadedScenes();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstallIntoScene(scene);
    }

    private static void TryInstallIntoLoadedScenes()
    {
        int count = SceneManager.sceneCount;
        for (int i = 0; i < count; i++)
        {
            TryInstallIntoScene(SceneManager.GetSceneAt(i));
        }
    }

    private static void TryInstallIntoScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (!string.Equals(scene.name, GameplayCoreSceneName, StringComparison.OrdinalIgnoreCase))
            return;

        // Already installed in this scene?
        var existing = UnityEngine.Object.FindObjectsByType<BoardRoundResetter>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].gameObject.scene == scene)
                return;
        }

        var go = new GameObject(nameof(BoardRoundResetter));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<BoardRoundResetter>();
    }
}

internal static class BoardRoundResetterBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
#if UNITY_2022_2_OR_NEWER
        if (UnityEngine.Object.FindFirstObjectByType<BoardRoundResetterBootstrapper>() != null)
            return;
#else
        if (UnityEngine.Object.FindObjectOfType<BoardRoundResetterBootstrapper>() != null)
            return;
#endif

        var go = new GameObject(nameof(BoardRoundResetterBootstrapper));
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<BoardRoundResetterBootstrapper>();
    }
}

