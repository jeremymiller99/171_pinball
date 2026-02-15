// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.SceneManagement;

public class ScoreManager : MonoBehaviour
{
    // NOTE: Keep these names/public fields so existing scripts (PointAdder/MultAdder)
    // keep working without modification. Conceptually, `points` are the current ball's points.
    public float points;
    public float mult;

    // Total banked score across the current round (sum of each drained ball's banked score).
    public float roundTotal;

    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text multText;

    [Header("Camera Shake on Score")]
    [Tooltip("Reference to CameraShake. Auto-resolved if not set.")]
    [SerializeField] private CameraShake cameraShake;
    
    [Header("Points Shake Settings")]
    [Tooltip("Base duration of camera shake when earning points.")]
    [SerializeField] private float shakeBaseDuration = 0.2f;
    [Tooltip("How much to increase duration per point earned. E.g., 0.004 means +100 points adds 0.4s.")]
    [SerializeField] private float shakeDurationPerPoint = 0.004f;
    [Tooltip("Maximum shake duration cap for points.")]
    [SerializeField] private float shakeMaxDuration = 0.5f;
    [Tooltip("Base magnitude of camera shake when earning points.")]
    [SerializeField] private float shakeBaseMagnitude = 0.18f;
    [Tooltip("How much to scale shake magnitude per point earned. E.g., 0.008 means +100 points adds 0.8 to magnitude.")]
    [SerializeField] private float shakeMagnitudePerPoint = 0.008f;
    [Tooltip("Maximum shake magnitude cap for points.")]
    [SerializeField] private float shakeMaxMagnitude = 0.8f;
    
    [Header("Multiplier Shake Settings")]
    [Tooltip("Base duration of camera shake when gaining multiplier.")]
    [SerializeField] private float multShakeBaseDuration = 0.25f;
    [Tooltip("How much to increase duration per multiplier gained. E.g., 0.15 means +1x adds 0.15s.")]
    [SerializeField] private float multShakeDurationPerMult = 0.15f;
    [Tooltip("Maximum shake duration cap for multiplier.")]
    [SerializeField] private float multShakeMaxDuration = 0.6f;
    [Tooltip("Base magnitude of camera shake when gaining multiplier.")]
    [SerializeField] private float multShakeBaseMagnitude = 0.22f;
    [Tooltip("How much to scale shake magnitude per multiplier gained. E.g., 0.25 means +1x adds 0.25 to magnitude.")]
    [SerializeField] private float multShakeMagnitudePerMult = 0.25f;
    [Tooltip("Maximum shake magnitude cap for multiplier.")]
    [SerializeField] private float multShakeMaxMagnitude = 0.7f;

    // Optional UI hooks (wire in inspector if you have these labels).
    [Header("Optional extra UI")]
    [SerializeField] private TMP_Text roundIndexText;
    [SerializeField] private TMP_Text roundTotalText;
    [SerializeField] private TMP_Text goalText;
    [SerializeField] private TMP_Text ballsRemainingText;
    [SerializeField] private TMP_Text coinsText;

    [Header("Scoring Control")]
    [SerializeField] private bool scoringLocked;

    [Header("Round Goal Tier Scaling")]
    [Tooltip("If enabled: each time LiveRoundTotal crosses another multiple of Goal (Goal * N), " +
             "the game speeds up and points awarded are increased additively.")]
    [SerializeField] private bool enableGoalTierScaling = true;

    [Tooltip("Game speed increases by this amount per goal tier (additive). Example: 0.10 => +10% per tier.")]
    [Min(0f)]
    [SerializeField] private float speedIncreasePerGoalTier = 0.10f;

    [Tooltip("Points awarded increases by this amount per goal tier (additive). Example: 0.50 => +50% per tier.")]
    [Min(0f)]
    [SerializeField] private float scoreIncreasePerGoalTier = 0.50f;

    [Tooltip("If true, applies speed-up via Time.timeScale (and scales fixedDeltaTime accordingly).")]
    [SerializeField] private bool applySpeedToTimeScale = true;

    [Header("External Multipliers (runtime)")]
    [Tooltip("Additional multiplier applied on top of tier-based SpeedMultiplier when writing Time.timeScale.\n" +
             "Used by cinematic systems (e.g., slow-mo) without fighting tier speed-up.")]
    [Min(0f)]
    [SerializeField] private float externalTimeScaleMultiplier = 1f;

    [Tooltip("Additional multiplier applied to POSITIVE point awards (after tier + round modifier scaling).\n" +
             "Used by Frenzy mode to boost scoring globally.")]
    [Min(0f)]
    [SerializeField] private float externalScoreAwardMultiplier = 1f;

    [Header("TimeScale Safety Caps")]
    [Tooltip("Unity (in-editor) requires Time.timeScale <= 100.\n" +
             "This cap prevents runaway goal-tier scaling (especially during Frenzy).")]
    [Min(0f)]
    [SerializeField] private float maxTimeScale = 100f;

    [Tooltip("Caps Time.fixedDeltaTime after applying timeScale (prevents huge physics steps at high timeScale).")]
    [Min(0.0001f)]
    [SerializeField] private float maxFixedDeltaTime = 0.1f;

    // Stored goal value for the current round (set via SetGoal).
    private float _goal;

    // Tier is floor(LiveRoundTotal / Goal). Tier 0 means "not yet reached the goal".
    [SerializeField, Tooltip("Read-only at runtime. Increments at Goal * N.")]
    private int _goalTier;

    private bool _timeBaseCaptured;
    private float _baseTimeScale = 1f;
    private float _baseFixedDeltaTime = 0.02f;

    /// <summary>
    /// Fired whenever score-related values change (points/mult/roundTotal/goal).
    /// Useful for non-TMP UI like meters/bars that should update immediately.
    /// </summary>
    public event Action ScoreChanged;

    /// <summary>
    /// Fired when the goal tier changes (crossing Goal * N thresholds).
    /// </summary>
    public event Action<int> GoalTierChanged;

    /// <summary>
    /// Current round goal (set by GameRulesManager via SetGoal).
    /// </summary>
    public float Goal => _goal;

    /// <summary>
    /// Current goal tier, computed from LiveRoundTotal / Goal.
    /// Tier 0 means LiveRoundTotal is below the goal.
    /// </summary>
    public int GoalTier => _goalTier;

    /// <summary>
    /// Multiplier applied to awarded points (not to banking).
    /// Computed additively per tier: 1 + tier * scoreIncreasePerGoalTier.
    /// </summary>
    public float ScoreAwardMultiplier => 1f + (_goalTier * scoreIncreasePerGoalTier);

    /// <summary>
    /// Game speed multiplier, computed additively per tier: 1 + tier * speedIncreasePerGoalTier.
    /// </summary>
    public float SpeedMultiplier => 1f + (_goalTier * speedIncreasePerGoalTier);

    /// <summary>
    /// Per-tier speed increase (e.g. 0.10 == +10% per tier).
    /// </summary>
    public float SpeedIncreasePerGoalTier => speedIncreasePerGoalTier;

    /// <summary>
    /// Per-tier score increase (e.g. 0.50 == +50% per tier).
    /// </summary>
    public float ScoreIncreasePerGoalTier => scoreIncreasePerGoalTier;

    /// <summary>
    /// Live round progress total: banked round total plus current ball's (points * mult).
    /// </summary>
    public float LiveRoundTotal => roundTotal + (points * mult);

    /// <summary>
    /// Additional multiplier applied on top of tier-based SpeedMultiplier when writing Time.timeScale.
    /// </summary>
    public float ExternalTimeScaleMultiplier => externalTimeScaleMultiplier;

    /// <summary>
    /// Additional multiplier applied to positive point awards (after tier + modifier scaling).
    /// </summary>
    public float ExternalScoreAwardMultiplier => externalScoreAwardMultiplier;

    // Multiple systems can request slow-mo. Effective request is the MIN of active requests.
    private readonly Dictionary<int, float> _timeScaleRequestBySourceId = new Dictionary<int, float>();
    private float _timeScaleRequestMin = 1f;

    /// <summary>
    /// Current effective requested time scale multiplier (min across active requests).
    /// </summary>
    public float TimeScaleRequestMultiplier => _timeScaleRequestMin;

    /// <summary>
    /// Returns the effective multiplier applied to POSITIVE point awards (as used by AddPointsScaled),
    /// including tier scaling (if enabled), active round modifier multiplier, and external award multiplier.
    /// </summary>
    public float GetEffectivePositivePointAwardMultiplier()
    {
        float m = 1f;
        if (enableGoalTierScaling)
        {
            m *= Mathf.Max(0f, ScoreAwardMultiplier);
        }

        float modifierMult = GetModifierScoreMultiplier();
        if (!Mathf.Approximately(modifierMult, 1f))
        {
            m *= Mathf.Max(0f, modifierMult);
        }

        if (!Mathf.Approximately(externalScoreAwardMultiplier, 1f))
        {
            m *= Mathf.Max(0f, externalScoreAwardMultiplier);
        }

        return m;
    }

    private const string ScorePanelRootName = "Score Panel";
    private const string RoundInfoPanelRootName = "Round Info Panel";
    private const string PointsObjectName = "Points";
    private const string MultObjectName = "Mult";
    private const string RoundIndexObjectName = "Round Index";
    private const string RoundTotalObjectName = "RoundTotal";
    private const string GoalObjectName = "Goal";
    private const string BallsRemainingObjectName = "Balls Remaining";
    private const string CoinsObjectName = "Coins";

    private static string FormatPointsCompact(float value)
    {
        // Keep 1-999 as-is, abbreviate at 4+ digits: 1k, 1.1k, ... 10k, 10.1k, ... 100k.
        float abs = Mathf.Abs(value);
        if (abs < 1000f)
        {
            return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
        }

        float k = abs / 1000f;
        float kRounded1 = Mathf.Round(k * 10f) / 10f;

        // At 100k+ show no decimal: 100k, 101k, ...
        if (kRounded1 >= 100f)
        {
            string s = Mathf.RoundToInt(kRounded1).ToString(CultureInfo.InvariantCulture) + "k";
            return value < 0f ? "-" + s : s;
        }

        // Under 100k show up to 1 decimal, dropping trailing .0: 1k, 1.1k, 10k, 10.1k, ...
        string core = kRounded1.ToString("0.#", CultureInfo.InvariantCulture) + "k";
        return value < 0f ? "-" + core : core;
    }

    private static string FormatMultiplier(float value)
    {
        // Prevent float artifacts like 2.9999999 in UI.
        float rounded = Mathf.Round(value * 100f) / 100f;
        return rounded.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureCoreScoreTextBindings();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        // Keep existing defaults.
        points = 0f;
        mult = 1f;
        roundTotal = 0f;
        _goal = 0f;
        _goalTier = 0;

        CaptureTimeBaseIfNeeded();
        ApplySpeedFromTier(force: true);

        EnsureCoreScoreTextBindings();
        ResolveCameraShake();
        RefreshScoreUI();
        ScoreChanged?.Invoke();
    }

    public void AddPoints(float p)
    {
        AddPointsScaled(p);
    }

    /// <summary>
    /// Adds points, applying the current tier-based award multiplier to positive values.
    /// Also applies the round modifier score multiplier if one is active.
    /// Also applies the external (runtime) score award multiplier if set (e.g. Frenzy).
    /// Returns the applied points amount (after multiplier).
    /// </summary>
    public float AddPointsScaled(float p)
    {
        if (scoringLocked) return 0f;

        EnsureCoreScoreTextBindings();

        // Only scale positive awards. Negative values are used by some reset/undo paths.
        float applied = p;
        if (enableGoalTierScaling && p > 0f)
        {
            applied *= Mathf.Max(0f, ScoreAwardMultiplier);
        }

        // Apply round modifier score multiplier if active
        if (p > 0f)
        {
            float modifierMult = GetModifierScoreMultiplier();
            if (!Mathf.Approximately(modifierMult, 1f))
            {
                applied *= Mathf.Max(0f, modifierMult);
            }
        }

        // Apply external (runtime) score award multiplier last.
        if (p > 0f && !Mathf.Approximately(externalScoreAwardMultiplier, 1f))
        {
            applied *= Mathf.Max(0f, externalScoreAwardMultiplier);
        }

        points += applied;

        // Trigger camera shake scaled by the points earned (only for positive scores).
        if (applied > 0f)
        {
            TriggerScoreShake(applied);
        }

        // Recompute tier and apply speed when crossing Goal * N thresholds.
        UpdateGoalTierAndApplySpeed();

        if (pointsText != null)
            pointsText.text = FormatPointsCompact(points);

        ScoreChanged?.Invoke();
        return applied;
    }

    /// <summary>
    /// Gets the score multiplier from the active round modifier via GameRulesManager.
    /// Returns 1.0 if no modifier is active or GameRulesManager is not found.
    /// </summary>
    private float GetModifierScoreMultiplier()
    {
        GameRulesManager rulesManager = FindRulesManager();
        return rulesManager != null ? rulesManager.GetModifierScoreMultiplier() : 1f;
    }

    /// <summary>
    /// Returns true if the multiplier is disabled by an active round modifier.
    /// </summary>
    private bool IsModifierMultiplierDisabled()
    {
        GameRulesManager rulesManager = FindRulesManager();
        return rulesManager != null && rulesManager.IsMultiplierDisabled();
    }

    private GameRulesManager _cachedRulesManager;

    private GameRulesManager FindRulesManager()
    {
        if (_cachedRulesManager != null)
            return _cachedRulesManager;

#if UNITY_2022_2_OR_NEWER
        _cachedRulesManager = UnityEngine.Object.FindFirstObjectByType<GameRulesManager>();
#else
        _cachedRulesManager = UnityEngine.Object.FindObjectOfType<GameRulesManager>();
#endif
        return _cachedRulesManager;
    }

    public void AddMult(float m)
    {
        if (scoringLocked) return;
        EnsureCoreScoreTextBindings();

        // If multiplier is disabled by round modifier, prevent positive mult gains
        if (m > 0f && IsModifierMultiplierDisabled())
        {
            // Still trigger a small effect to show something happened, but don't increase mult
            return;
        }

        mult += m;

        // Trigger camera shake scaled by multiplier gained (only for positive gains).
        if (m > 0f)
        {
            TriggerMultShake(m);
        }

        UpdateGoalTierAndApplySpeed();
        if (multText != null)
            multText.text = FormatMultiplier(mult);
        ScoreChanged?.Invoke();
    }

    public void SetScoringLocked(bool locked)
    {
        scoringLocked = locked;
    }

    /// <summary>
    /// Bank the current ball score into the round total and reset the per-ball score state.
    /// Returns the banked amount (points * mult).
    /// </summary>
    public float BankCurrentBallScore()
    {
        return BankCurrentBallScore(1f);
    }

    /// <summary>
    /// Bank the current ball score into the round total, multiplied by <paramref name="bankMultiplier"/>,
    /// then reset the per-ball score state.
    /// Returns the banked amount (points * mult * bankMultiplier).
    /// </summary>
    public float BankCurrentBallScore(float bankMultiplier)
    {
        float m = bankMultiplier;
        if (m <= 0f) m = 1f;

        float banked = points * mult * m;
        roundTotal += banked;

        // Reset for next ball.
        points = 0f;
        mult = 1f;

        UpdateGoalTierAndApplySpeed();
        RefreshScoreUI();
        ScoreChanged?.Invoke();
        return banked;
    }

    /// <summary>
    /// Reset round and per-ball scoring back to defaults.
    /// </summary>
    public void ResetForNewRound()
    {
        roundTotal = 0f;
        points = 0f;
        mult = 1f;
        _goalTier = 0;

        // Reset game speed back to baseline at the start of each round.
        ApplySpeedFromTier(force: true);

        RefreshScoreUI();
        ScoreChanged?.Invoke();
    }

    /// <summary>
    /// Optional: If you're using extra labels, call these from your rules/UI layer.
    /// </summary>
    public void SetGoal(float goal)
    {
        _goal = goal;
        UpdateGoalTierAndApplySpeed();
        EnsureCoreScoreTextBindings();
        if (goalText != null)
            goalText.text = goal.ToString();
        ScoreChanged?.Invoke();
    }

    /// <summary>
    /// Sets an external multiplier for time scaling (multiplies with tier-based SpeedMultiplier).
    /// Useful for goal cinematics / slow motion.
    /// </summary>
    public void SetExternalTimeScaleMultiplier(float multiplier)
    {
        // Slow-mo removed: never allow multipliers below 1.
        externalTimeScaleMultiplier = Mathf.Max(1f, multiplier);
        ApplySpeedFromTier(force: true);
    }

    /// <summary>
    /// Registers/updates a time-scale request from a specific source (e.g. a ball proximity trigger).
    /// The effective request is the MIN across all active requests.
    /// Use multiplier=1 to mean "no slow-mo"; prefer ClearTimeScaleRequest when done.
    /// </summary>
    public void SetTimeScaleRequest(UnityEngine.Object source, float multiplier)
    {
        if (source == null) return;
        int id = source.GetInstanceID();
        // Slow-mo removed: treat any request below 1 as "no request".
        _timeScaleRequestBySourceId[id] = Mathf.Max(1f, multiplier);
        RecomputeTimeScaleRequestMin();
        ApplySpeedFromTier(force: true);
    }

    /// <summary>
    /// Clears a previously-set time-scale request from a specific source.
    /// </summary>
    public void ClearTimeScaleRequest(UnityEngine.Object source)
    {
        if (source == null) return;
        int id = source.GetInstanceID();
        if (_timeScaleRequestBySourceId.Remove(id))
        {
            RecomputeTimeScaleRequestMin();
            ApplySpeedFromTier(force: true);
        }
    }

    private void ClearAllTimeScaleRequests()
    {
        _timeScaleRequestBySourceId.Clear();
        _timeScaleRequestMin = 1f;
    }

    private void RecomputeTimeScaleRequestMin()
    {
        float min = 1f;
        foreach (var kv in _timeScaleRequestBySourceId)
        {
            float v = Mathf.Max(0f, kv.Value);
            if (v < min) min = v;
        }
        _timeScaleRequestMin = min;
    }

    /// <summary>
    /// Sets an external multiplier for point awards (applied to positive awards only).
    /// Useful for Frenzy mode.
    /// </summary>
    public void SetExternalScoreAwardMultiplier(float multiplier)
    {
        externalScoreAwardMultiplier = Mathf.Max(0f, multiplier);
    }

    /// <summary>
    /// Resets external (runtime) multipliers back to defaults.
    /// </summary>
    public void ResetExternalMultipliers()
    {
        externalTimeScaleMultiplier = 1f;
        externalScoreAwardMultiplier = 1f;
        ClearAllTimeScaleRequests();
        ApplySpeedFromTier(force: true);
    }

    public void SetRoundIndex(int roundIndex)
    {
        EnsureCoreScoreTextBindings();
        if (roundIndexText != null)
            roundIndexText.text = (roundIndex + 1).ToString();
    }

    public void SetBallsRemaining(int ballsRemaining)
    {
        EnsureCoreScoreTextBindings();
        if (ballsRemainingText != null)
            ballsRemainingText.text = ballsRemaining.ToString();
    }

    public void SetCoins(int coins)
    {
        EnsureCoreScoreTextBindings();
        if (coinsText != null)
            coinsText.text = coins.ToString();
    }

    private void RefreshScoreUI()
    {
        EnsureCoreScoreTextBindings();
        if (pointsText != null)
            pointsText.text = FormatPointsCompact(points);
        if (multText != null)
            multText.text = FormatMultiplier(mult);
        if (roundTotalText != null)
            roundTotalText.text = roundTotal.ToString();
        if (goalText != null)
            goalText.text = _goal.ToString();
    }

    private void UpdateGoalTierAndApplySpeed()
    {
        int newTier = ComputeGoalTier();
        if (newTier != _goalTier)
        {
            _goalTier = newTier;
            ApplySpeedFromTier(force: true);
            GoalTierChanged?.Invoke(_goalTier);
        }
        else
        {
            // Keep speed in sync even if another system changed Time.timeScale.
            ApplySpeedFromTier(force: false);
        }
    }

    private int ComputeGoalTier()
    {
        if (!enableGoalTierScaling) return 0;
        if (_goal <= 0f) return 0;

        float live = LiveRoundTotal;
        if (live <= 0f) return 0;

        int tier = Mathf.FloorToInt(live / _goal);
        return Mathf.Max(0, tier);
    }

    private void CaptureTimeBaseIfNeeded()
    {
        if (_timeBaseCaptured) return;
        _timeBaseCaptured = true;
        _baseTimeScale = Mathf.Max(0f, Time.timeScale);
        _baseFixedDeltaTime = Mathf.Max(0.0001f, Time.fixedDeltaTime);
    }

    private void ApplySpeedFromTier(bool force)
    {
        if (!applySpeedToTimeScale) return;
        CaptureTimeBaseIfNeeded();

        // Target is baseline * tier multiplier.
        // Slow-mo removed: never allow any multiplier to reduce speed below baseline.
        float requestMult = Mathf.Max(1f, _timeScaleRequestMin);
        float extMult = Mathf.Max(1f, externalTimeScaleMultiplier);
        float targetScale = _baseTimeScale * Mathf.Max(0f, SpeedMultiplier) * extMult * requestMult;
        if (maxTimeScale > 0f)
        {
            targetScale = Mathf.Min(targetScale, maxTimeScale);
        }
        if (!force && Mathf.Approximately(Time.timeScale, targetScale))
            return;

        Time.timeScale = targetScale;

        // Keep physics stepping consistent relative to scaled time.
        // Standard approach: fixedDeltaTime scales with timeScale.
        float targetFixed = _baseFixedDeltaTime * Mathf.Max(0f, Time.timeScale);
        if (maxFixedDeltaTime > 0f)
        {
            targetFixed = Mathf.Min(targetFixed, maxFixedDeltaTime);
        }
        Time.fixedDeltaTime = Mathf.Max(0.0001f, targetFixed);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // In additive-scene setups, the Score UI may live in a different scene than this manager.
        // Re-resolve references whenever a new scene is loaded.
        EnsureCoreScoreTextBindings();
        ResolveCameraShake();
        RefreshScoreUI();
    }

    private void ResolveCameraShake()
    {
        if (cameraShake != null && cameraShake.isActiveAndEnabled)
            return;

        cameraShake = CameraShake.Instance;
        if (cameraShake != null && cameraShake.isActiveAndEnabled)
            return;

#if UNITY_2022_2_OR_NEWER
        cameraShake = FindFirstObjectByType<CameraShake>();
#else
        cameraShake = FindObjectOfType<CameraShake>();
#endif
    }

    private void TriggerScoreShake(float pointsEarned)
    {
        if (cameraShake == null || !cameraShake.isActiveAndEnabled)
        {
            ResolveCameraShake();
        }

        if (cameraShake == null || !cameraShake.isActiveAndEnabled)
            return;

        // Calculate duration based on points earned.
        float duration = shakeBaseDuration + (pointsEarned * shakeDurationPerPoint);
        duration = Mathf.Clamp(duration, shakeBaseDuration, shakeMaxDuration);

        // Calculate magnitude based on points earned.
        float magnitude = shakeBaseMagnitude + (pointsEarned * shakeMagnitudePerPoint);
        magnitude = Mathf.Clamp(magnitude, shakeBaseMagnitude, shakeMaxMagnitude);

        cameraShake.Shake(duration, magnitude);
    }

    private void TriggerMultShake(float multGained)
    {
        if (cameraShake == null || !cameraShake.isActiveAndEnabled)
        {
            ResolveCameraShake();
        }

        if (cameraShake == null || !cameraShake.isActiveAndEnabled)
            return;

        // Calculate duration based on multiplier gained.
        float duration = multShakeBaseDuration + (multGained * multShakeDurationPerMult);
        duration = Mathf.Clamp(duration, multShakeBaseDuration, multShakeMaxDuration);

        // Calculate magnitude based on multiplier gained.
        float magnitude = multShakeBaseMagnitude + (multGained * multShakeMagnitudePerMult);
        magnitude = Mathf.Clamp(magnitude, multShakeBaseMagnitude, multShakeMaxMagnitude);

        cameraShake.Shake(duration, magnitude);
    }

    private void EnsureCoreScoreTextBindings()
    {
        bool scorePanelBound = IsLiveSceneText(pointsText) && IsLiveSceneText(multText);
        bool roundInfoBound = IsLiveSceneText(roundIndexText) && IsLiveSceneText(roundTotalText) 
                              && IsLiveSceneText(goalText) && IsLiveSceneText(ballsRemainingText) 
                              && IsLiveSceneText(coinsText);

        if (scorePanelBound && roundInfoBound)
            return;

        // Prefer binding within a Score Panel root if present.
        if (!scorePanelBound)
        {
            GameObject scorePanel = GameObject.Find(ScorePanelRootName);
            if (scorePanel != null)
            {
                if (!IsLiveSceneText(pointsText))
                    pointsText = FindTmpTextInChildrenByName(scorePanel.transform, PointsObjectName);
                if (!IsLiveSceneText(multText))
                    multText = FindTmpTextInChildrenByName(scorePanel.transform, MultObjectName);
            }
        }

        // Prefer binding within Round Info Panel root if present.
        if (!roundInfoBound)
        {
            GameObject roundInfoPanel = GameObject.Find(RoundInfoPanelRootName);
            if (roundInfoPanel != null)
            {
                if (!IsLiveSceneText(roundIndexText))
                    roundIndexText = FindTmpTextInChildrenByName(roundInfoPanel.transform, RoundIndexObjectName);
                if (!IsLiveSceneText(roundTotalText))
                    roundTotalText = FindTmpTextInChildrenByName(roundInfoPanel.transform, RoundTotalObjectName);
                if (!IsLiveSceneText(goalText))
                    goalText = FindTmpTextInChildrenByName(roundInfoPanel.transform, GoalObjectName);
                if (!IsLiveSceneText(ballsRemainingText))
                    ballsRemainingText = FindTmpTextInChildrenByName(roundInfoPanel.transform, BallsRemainingObjectName);
                if (!IsLiveSceneText(coinsText))
                    coinsText = FindTmpTextInChildrenByName(roundInfoPanel.transform, CoinsObjectName);
            }
        }

        // Recheck after panel-based search.
        scorePanelBound = IsLiveSceneText(pointsText) && IsLiveSceneText(multText);
        roundInfoBound = IsLiveSceneText(roundIndexText) && IsLiveSceneText(roundTotalText) 
                         && IsLiveSceneText(goalText) && IsLiveSceneText(ballsRemainingText) 
                         && IsLiveSceneText(coinsText);

        if (scorePanelBound && roundInfoBound)
            return;

        // Fallback: search all loaded-scene TMP_Text objects (including inactive).
        // Resources.FindObjectsOfTypeAll includes assets/prefabs too, so filter by valid scene.
        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < allTexts.Length; i++)
        {
            TMP_Text t = allTexts[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (!t.gameObject.activeInHierarchy) continue;

            string n = t.gameObject.name;
            
            // Score Panel elements
            if (!IsLiveSceneText(pointsText) && string.Equals(n, PointsObjectName, StringComparison.OrdinalIgnoreCase))
                pointsText = t;
            else if (!IsLiveSceneText(multText) && string.Equals(n, MultObjectName, StringComparison.OrdinalIgnoreCase))
                multText = t;
            // Round Info Panel elements
            else if (!IsLiveSceneText(roundIndexText) && string.Equals(n, RoundIndexObjectName, StringComparison.OrdinalIgnoreCase))
                roundIndexText = t;
            else if (!IsLiveSceneText(roundTotalText) && string.Equals(n, RoundTotalObjectName, StringComparison.OrdinalIgnoreCase))
                roundTotalText = t;
            else if (!IsLiveSceneText(goalText) && string.Equals(n, GoalObjectName, StringComparison.OrdinalIgnoreCase))
                goalText = t;
            else if (!IsLiveSceneText(ballsRemainingText) && string.Equals(n, BallsRemainingObjectName, StringComparison.OrdinalIgnoreCase))
                ballsRemainingText = t;
            else if (!IsLiveSceneText(coinsText) && string.Equals(n, CoinsObjectName, StringComparison.OrdinalIgnoreCase))
                coinsText = t;
        }
    }

    private static TMP_Text FindTmpTextInChildrenByName(Transform root, string childName)
    {
        if (root == null) return null;

        // Look for an exact name match (case-insensitive) and grab TMP on that object.
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t == null) continue;
            if (!t.gameObject.activeInHierarchy) continue;
            if (string.Equals(t.gameObject.name, childName, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }

    private static bool IsLiveSceneText(TMP_Text t)
    {
        if (t == null) return false;
        if (!t.gameObject.scene.IsValid()) return false;
        if (!t.gameObject.activeInHierarchy) return false;
        return true;
    }
}
