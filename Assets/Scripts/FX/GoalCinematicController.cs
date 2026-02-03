using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Peggle-style pre-goal cinematic:
/// - As the player approaches the round goal, ramps slow-mo (NO camera zoom).
/// - On goal hit, holds slow-mo briefly to emphasize the explosion.
/// - Releases after the hold window (and stays off during Frenzy afterwards).
///
/// Uses ScoreManager time-scale requests so it can coexist with other slow-mo systems.
/// Intended to live in the GameplayCore scene.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class GoalCinematicController : MonoBehaviour
{
    [Header("Activation")]
    [Range(0f, 1f)]
    [SerializeField] private float startThreshold01 = 0.85f;
    [Range(0f, 1f)]
    [SerializeField] private float fullEffectThreshold01 = 0.98f;

    [Header("Slow motion (external time scale multiplier)")]
    [Min(0f)]
    [SerializeField] private float minTimeScaleMultiplier = 0.35f;
    [Min(0f)]
    [SerializeField] private float timeResponse = 10f;

    // Camera zoom intentionally removed per request.

    [Header("Goal hit emphasis (hold)")]
    [Min(0f)]
    [SerializeField] private float goalHitHoldSeconds = 2.0f;
    [Min(0f)]
    [SerializeField] private float goalHitTimeScaleMultiplier = 0.25f;
    // Camera zoom intentionally removed per request.

    [Header("Pre-hit snap (right before goal)")]
    [Tooltip("When progress reaches this threshold (but before goal is actually hit), snap into the goal-hit cinematic.\n" +
             "This makes the explosion much easier to read.")]
    [Range(0f, 1f)]
    [SerializeField] private float preHitSnapThreshold01 = 0.985f;

    private ScoreManager _score;
    private GameRulesManager _rules;
    private float _currentTimeMult = 1f;

    private float _goalHoldUntilUnscaledTime;

    private void OnEnable()
    {
        ResolveRefs();

        if (_score != null)
            _score.GoalTierChanged += OnGoalTierChanged;
        if (_rules != null)
            _rules.RoundStarted += OnRoundStarted;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_score != null)
            _score.GoalTierChanged -= OnGoalTierChanged;
        if (_rules != null)
            _rules.RoundStarted -= OnRoundStarted;

        // Always release on disable.
        if (_score != null)
        {
            _score.ClearTimeScaleRequest(this);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveRefs();
    }

    private void ResolveRefs()
    {
        if (_score == null)
        {
#if UNITY_2022_2_OR_NEWER
            _score = FindFirstObjectByType<ScoreManager>();
#else
            _score = FindObjectOfType<ScoreManager>();
#endif
        }

        if (_rules == null)
        {
#if UNITY_2022_2_OR_NEWER
            _rules = FindFirstObjectByType<GameRulesManager>();
#else
            _rules = FindObjectOfType<GameRulesManager>();
#endif
        }
    }

    private void OnRoundStarted()
    {
        // New round = reset cinematic state.
        _goalHoldUntilUnscaledTime = 0f;
        _currentTimeMult = 1f;
        if (_score != null)
        {
            _score.ClearTimeScaleRequest(this);
        }
    }

    private void OnGoalTierChanged(int tier)
    {
        if (tier >= 1)
        {
            // Goal reached: hold slow-mo + zoom briefly to emphasize the explosion.
            float hold = Mathf.Max(0f, goalHitHoldSeconds);
            _goalHoldUntilUnscaledTime = hold > 0f ? (Time.unscaledTime + hold) : 0f;

            // Snap to the emphasis immediately (reads best for the "goal pop").
            ApplyImmediate(goalHitTimeScaleMultiplier);
        }
    }

    private void LateUpdate()
    {
        ResolveRefs();

        if (_score == null)
            return;

        // If we're in the goal-hit hold window, keep the emphasis on even if Frenzy has begun.
        if (_goalHoldUntilUnscaledTime > 0f && Time.unscaledTime < _goalHoldUntilUnscaledTime)
        {
            SmoothToward(Mathf.Max(0f, goalHitTimeScaleMultiplier));
            return;
        }

        // If Frenzy has begun (and we're not in the hold window), this cinematic should be off.
        if (FrenzyController.IsFrenzyActive)
        {
            SmoothToward(1f);
            return;
        }

        float goal = _score.Goal;
        float live = _score.LiveRoundTotal;
        if (goal <= 0.0001f || live <= 0f)
        {
            SmoothToward(1f);
            return;
        }

        float progress01 = Mathf.Clamp01(live / goal);

        // Only run before the goal is hit.
        if (progress01 >= 1f)
        {
            SmoothToward(1f);
            return;
        }

        // Snap into the goal-hit cinematic right before the goal is reached (strong emphasis).
        if (progress01 >= Mathf.Clamp01(preHitSnapThreshold01))
        {
            SmoothToward(Mathf.Max(0f, goalHitTimeScaleMultiplier));
            return;
        }

        float t = Mathf.InverseLerp(startThreshold01, Mathf.Max(startThreshold01, fullEffectThreshold01), progress01);
        t = Smooth01(Mathf.Clamp01(t));

        float targetTimeMult = Mathf.Lerp(1f, Mathf.Max(0f, minTimeScaleMultiplier), t);

        SmoothToward(targetTimeMult);
    }

    private void SmoothToward(float targetTimeMult)
    {
        float dt = Time.unscaledDeltaTime;

        // Time multiplier smoothing
        float timeLerp = 1f - Mathf.Exp(-Mathf.Max(0f, timeResponse) * dt);
        _currentTimeMult = Mathf.Lerp(_currentTimeMult, targetTimeMult, timeLerp);
        if (_score != null)
        {
            _score.SetTimeScaleRequest(this, _currentTimeMult);
        }
    }

    private void ApplyImmediate(float timeMult)
    {
        _currentTimeMult = Mathf.Max(0f, timeMult);
        if (_score != null)
        {
            _score.SetTimeScaleRequest(this, _currentTimeMult);
        }
    }

    private static float Smooth01(float u)
    {
        return u * u * (3f - 2f * u);
    }
}

internal sealed class GoalCinematicControllerBootstrapper : MonoBehaviour
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

        var existing = UnityEngine.Object.FindObjectsByType<GoalCinematicController>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].gameObject.scene == scene)
                return;
        }

        var go = new GameObject(nameof(GoalCinematicController));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<GoalCinematicController>();
    }
}

internal static class GoalCinematicControllerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
#if UNITY_2022_2_OR_NEWER
        if (UnityEngine.Object.FindFirstObjectByType<GoalCinematicControllerBootstrapper>() != null)
            return;
#else
        if (UnityEngine.Object.FindObjectOfType<GoalCinematicControllerBootstrapper>() != null)
            return;
#endif

        var go = new GameObject(nameof(GoalCinematicControllerBootstrapper));
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<GoalCinematicControllerBootstrapper>();
    }
}

