// Updated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Peggle-style Frenzy controller.
/// - Starts Frenzy the first time the round goal is reached (ScoreManager.GoalTier becomes 1).
/// - Applies a large external score multiplier via ScoreManager.
/// - Spawns a "goal pop" cube burst at the active ball.
/// - Resets on each round start.
///
/// Intended to live in the GameplayCore scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class FrenzyController : MonoBehaviour
{
    public static FrenzyController Instance { get; private set; }
    public static bool IsFrenzyActive => Instance != null && Instance._frenzyActive;

    [Header("Frenzy scoring")]
    [Min(0f)]
    [Tooltip("Multiplier applied to positive point awards during Frenzy. Example: 1.10 = +10%.")]
    [SerializeField] private float frenzyScoreAwardMultiplier = 1.10f;

    [Header("Goal pop (cube burst)")]
    [Min(0)]
    [SerializeField] private int goalBurstCubeCount = 18;
    [Min(0f)]
    [SerializeField] private float goalBurstRadius = 0.75f;
    [Min(0f)]
    [SerializeField] private float goalBurstForce = 7.5f;
    [Min(0f)]
    [SerializeField] private float goalBurstUpwardsModifier = 0.15f;
    [Min(0f)]
    [SerializeField] private float goalBurstTorque = 6f;
    [SerializeField] private Vector2 goalBurstCubeSizeRange = new Vector2(0.22f, 0.45f);
    [Min(0f)]
    [SerializeField] private float goalBurstShardLifetime = 6.0f;

    [Header("Optional camera shake")]
    [SerializeField] private bool shakeOnGoal = true;
    [Min(0f)]
    [SerializeField] private float goalShakeDuration = 0.35f;
    [Min(0f)]
    [SerializeField] private float goalShakeMagnitude = 0.55f;

    [Tooltip("Extra shake pulses after the initial hit (uses unscaled time).")]
    [SerializeField] private bool useMultiPulseShake = true;
    [Min(0)]
    [SerializeField] private int shakePulseCount = 2;
    [Min(0f)]
    [SerializeField] private float shakePulseIntervalSeconds = 0.12f;
    [Range(0f, 1f)]
    [SerializeField] private float shakePulseMagnitudeFalloff = 0.55f;
    [Range(0f, 1f)]
    [SerializeField] private float shakePulseDurationFalloff = 0.75f;

    private ScoreManager _score;
    private GameRulesManager _rules;
    private bool _frenzyActive;
    private Coroutine _shakeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

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
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-resolve after loads since core/board scenes are additive.
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
        _frenzyActive = false;

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        if (_score != null)
        {
            _score.ResetExternalMultipliers();
        }
    }

    private void OnGoalTierChanged(int tier)
    {
        // Goal reached for the first time this round.
        if (tier < 1 || _frenzyActive)
            return;

        BeginFrenzy();
    }

    private void BeginFrenzy()
    {
        _frenzyActive = true;

        if (_score != null)
        {
            // Apply frenzy scoring.
            _score.SetExternalScoreAwardMultiplier(frenzyScoreAwardMultiplier);
        }

        Vector3 origin = ResolveGoalBurstOrigin();
        SpawnCubeBurst(origin);

        if (shakeOnGoal)
        {
            var shake = CameraShake.Instance;
            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                _shakeRoutine = null;
            }
            _shakeRoutine = StartCoroutine(GoalShakeRoutine(shake));
        }
    }

    private IEnumerator GoalShakeRoutine(CameraShake shake)
    {
        if (shake == null) yield break;

        float baseDur = Mathf.Max(0f, goalShakeDuration);
        float baseMag = Mathf.Max(0f, goalShakeMagnitude);

        shake.Shake(baseDur, baseMag);

        if (!useMultiPulseShake)
        {
            _shakeRoutine = null;
            yield break;
        }

        int pulses = Mathf.Max(0, shakePulseCount);
        if (pulses == 0)
        {
            _shakeRoutine = null;
            yield break;
        }

        float interval = Mathf.Max(0f, shakePulseIntervalSeconds);
        float magFall = Mathf.Clamp01(shakePulseMagnitudeFalloff);
        float durFall = Mathf.Clamp01(shakePulseDurationFalloff);

        float mag = baseMag;
        float dur = baseDur;

        for (int i = 0; i < pulses; i++)
        {
            if (interval > 0f)
                yield return new WaitForSecondsRealtime(interval);
            else
                yield return null;

            mag *= magFall;
            dur *= durFall;

            if (mag <= 0.0001f || dur <= 0.0001f)
                break;

            shake.Shake(dur, mag);
        }

        _shakeRoutine = null;
    }

    private Vector3 ResolveGoalBurstOrigin()
    {
        if (_rules != null && _rules.ActiveBalls.Count != 0)
            return _rules.ActiveBalls[0].transform.position;

        Camera cam = Camera.main;
        if (cam != null)
            return cam.transform.position + cam.transform.forward * 8f;

        return Vector3.zero;
    }

    private void SpawnCubeBurst(Vector3 origin)
    {
        int n = Mathf.Max(0, goalBurstCubeCount);
        if (n == 0) return;

        float radius = Mathf.Max(0.01f, goalBurstRadius);
        float force = Mathf.Max(0f, goalBurstForce);
        float torque = Mathf.Max(0f, goalBurstTorque);

        float minSize = Mathf.Max(0.01f, Mathf.Min(goalBurstCubeSizeRange.x, goalBurstCubeSizeRange.y));
        float maxSize = Mathf.Max(minSize, Mathf.Max(goalBurstCubeSizeRange.x, goalBurstCubeSizeRange.y));

        for (int i = 0; i < n; i++)
        {
            Vector3 pos = origin + UnityEngine.Random.insideUnitSphere * radius;
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "GoalBurstShard";
            shard.transform.position = pos;
            shard.transform.rotation = UnityEngine.Random.rotation;

            float s = UnityEngine.Random.Range(minSize, maxSize);
            shard.transform.localScale = new Vector3(s, s, s);

            var rb = shard.AddComponent<Rigidbody>();
            rb.mass = 0.06f;
            // These shards are purely visual flair. Use cheap physics settings to avoid
            // frame drops that can feel like "slow motion" after goal reached.
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.interpolation = RigidbodyInterpolation.None;

            rb.AddExplosionForce(force, origin, radius * 2f, goalBurstUpwardsModifier, ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * torque, ForceMode.Impulse);

            var autoDestroy = shard.AddComponent<CubeShardAutoDestroy>();
            autoDestroy.SetLifetime(goalBurstShardLifetime);
        }
    }
}

internal sealed class FrenzyControllerBootstrapper : MonoBehaviour
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
        var existing = UnityEngine.Object.FindObjectsByType<FrenzyController>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].gameObject.scene == scene)
                return;
        }

        var go = new GameObject(nameof(FrenzyController));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<FrenzyController>();
    }
}

internal static class FrenzyControllerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
#if UNITY_2022_2_OR_NEWER
        if (UnityEngine.Object.FindFirstObjectByType<FrenzyControllerBootstrapper>() != null)
            return;
#else
        if (UnityEngine.Object.FindObjectOfType<FrenzyControllerBootstrapper>() != null)
            return;
#endif

        var go = new GameObject(nameof(FrenzyControllerBootstrapper));
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<FrenzyControllerBootstrapper>();
    }
}

