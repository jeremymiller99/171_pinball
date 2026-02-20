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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveRules();
    }

    private void ResolveRules()
    {
        if (_rules != null) return;

        _rules = FindFirstObjectByType<GameRulesManager>();
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

