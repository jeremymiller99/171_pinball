// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.SceneManagement;

public enum TypeOfScore
{
    points,
    mult,
    coins
}


public class ScoreManager : MonoBehaviour
{
    // NOTE: Keep these names/public fields so existing scripts (PointAdder/MultAdder)
    // keep working without modification. Conceptually, `points` are the current ball's points.
    public float points;
    public float mult;

    // Total banked score across the current round (sum of each drained ball's banked score).
    public float roundTotal;

    [Header("Level Progress (runtime)")]
    [SerializeField, Tooltip("Read-only at runtime. Tracks how much cumulative score has been consumed by level-ups.")]
    private float levelProgressOffset;

    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text multText;

    private float pointsUiDisplayed;
    private float multUiDisplayed;
    private bool deferPointsAndMultUiUpdates;
    private int coinsUiDisplayed;

    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;

    [Header("Camera Shake on Score")]
    [Tooltip("Reference to CameraShake. Auto-resolved if not set.")]
    [SerializeField] private CameraShake cameraShake;
    
    [Header("Points Shake Settings")]
    [Tooltip("Base duration of camera shake when earning points.")]
    [SerializeField] private float shakeBaseDuration = 0.22f;

    [Tooltip("Exponent applied to points before converting to shake. Lower (< 1) ramps sooner.")]
    [Range(0.25f, 1.5f)]
    [SerializeField] private float shakePointsExponent = 0.75f;

    [Tooltip("How much to increase duration per shaped points. Shaped = points ^ exponent.")]
    [SerializeField] private float shakeDurationPerPoint = 0.01f;

    [Tooltip("Maximum shake duration cap for points.")]
    [SerializeField] private float shakeMaxDuration = 0.65f;
    [Tooltip("Base magnitude of camera shake when earning points.")]
    [SerializeField] private float shakeBaseMagnitude = 0.24f;

    [Tooltip("How much to scale shake magnitude per shaped points. Shaped = points ^ exponent.")]
    [SerializeField] private float shakeMagnitudePerPoint = 0.02f;
    [Tooltip("Maximum shake magnitude cap for points.")]
    [SerializeField] private float shakeMaxMagnitude = 1.0f;
    
    [Header("Multiplier Shake Settings")]
    [Tooltip("Base duration of camera shake when gaining multiplier.")]
    [SerializeField] private float multShakeBaseDuration = 0.28f;

    [Tooltip("Exponent applied to mult gain before converting to shake. Lower (< 1) ramps sooner.")]
    [Range(0.25f, 1.5f)]
    [SerializeField] private float multShakeExponent = 0.75f;

    [Tooltip("How much to increase duration per shaped mult gain. Shaped = multGain ^ exponent.")]
    [SerializeField] private float multShakeDurationPerMult = 0.22f;

    [Tooltip("Maximum shake duration cap for multiplier.")]
    [SerializeField] private float multShakeMaxDuration = 0.75f;
    [Tooltip("Base magnitude of camera shake when gaining multiplier.")]
    [SerializeField] private float multShakeBaseMagnitude = 0.26f;

    [Tooltip("How much to scale shake magnitude per shaped mult gain. Shaped = multGain ^ exponent.")]
    [SerializeField] private float multShakeMagnitudePerMult = 0.28f;
    [Tooltip("Maximum shake magnitude cap for multiplier.")]
    [SerializeField] private float multShakeMaxMagnitude = 0.85f;

    // Optional UI hooks (wire in inspector if you have these labels).
    [Header("Optional extra UI")]
    [SerializeField] private TMP_Text roundIndexText;
    [SerializeField] private TMP_Text roundTotalText;
    [SerializeField] private TMP_Text goalText;
    [SerializeField] private TMP_Text ballsRemainingText;
    [SerializeField] private TMP_Text coinsText;

    [Header("Round Index Juice (optional)")]
    [Tooltip("If enabled, plays a 'pop' animation when the displayed round/level index increases.")]
    [SerializeField] private bool enableRoundIndexPop = true;

    [Min(0f)]
    [SerializeField] private float roundIndexPopDuration = 0.22f;

    [Min(1f)]
    [SerializeField] private float roundIndexPopPeakScale = 1.35f;

    [Tooltip("Optional vertical offset (anchoredPosition Y) applied during the pop.")]
    [SerializeField] private float roundIndexPopYOffset = 12f;

    [SerializeField] private bool roundIndexPopFlashColor = true;
    [SerializeField] private Color roundIndexPopFlashTargetColor = new Color(1f, 0.85f, 0.2f, 1f);

    [SerializeField] private AnimationCurve roundIndexPopCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.22f, 1f),
        new Keyframe(0.52f, -0.18f),
        new Keyframe(0.78f, 0.12f),
        new Keyframe(1f, 0f));

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

    [Header("Global Baseline Speed")]
    [Tooltip("Baseline game speed applied via Time.timeScale before any tier or external multipliers.\n" +
             "Set to 1.05 to make the whole game run 5% faster.")]
    [Min(0f)]
    [SerializeField] private float baselineTimeScale = 1.05f;

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

    private int _roundIndexUiLast = -1;
    private int _roundIndexJuiceTextInstanceId;
    private bool _roundIndexJuiceBaselineCaptured;
    private Vector3 _roundIndexBaseLocalScale;
    private Vector2 _roundIndexBaseAnchoredPos;
    private Color _roundIndexBaseColor;
    private Coroutine _roundIndexPopRoutine;

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
    /// Live progress toward the current level goal.
    /// Computed as LiveRoundTotal minus the consumed progress offset from previous level-ups.
    /// </summary>
    public float LiveLevelProgress => Mathf.Max(0f, LiveRoundTotal - levelProgressOffset);

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

    //multipliers for point, mult, and coins for the AddScore() function
    //use the modifier multiplier SPECIFICALLY for modifiers.

    [SerializeField] private float pointMultiplier;
    public float pointsModifierMultiplier;
    [SerializeField] private float multMultiplier;
    public float multModifierMultiplier;
    [SerializeField] private int coinMultiplier;
    public int coinModifierMultiplier;

    [SerializeField] private int roundsToAddToMultipliers;
    [SerializeField] private float amountToAddToMultipliers;

    private Queue<float> pointQueue = new Queue<float>();
    private Queue<float> multQueue = new Queue<float>();
    private Queue<int> coinQueue = new Queue<int>();

    void Awake()
    {
        EnsureRefs();
    }  

    void EnsureRefs()
    {
        gameRulesManager = FindFirstObjectByType<GameRulesManager>();
        floatingTextSpawner = FindFirstObjectByType<FloatingTextSpawner>();
    }

    public void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        switch(typeOfScore)
        {
            case TypeOfScore.points:
                AddPoints(amount * pointMultiplier * pointsModifierMultiplier, pos);
                break;
            case TypeOfScore.mult:
                AddMult(amount * multMultiplier * multModifierMultiplier, pos);
                break;
            case TypeOfScore.coins:
                AddCoins((int)amount * coinMultiplier* coinModifierMultiplier, pos);
                break;
        }
    }

    public void OnNewRound(int round)
    {   
        if (round % roundsToAddToMultipliers == 0)
        {
            pointMultiplier += amountToAddToMultipliers;
            multMultiplier += amountToAddToMultipliers;
        }
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
        // Keep 1-999 as-is, abbreviate at 4+ digits: 1K, 1.1K, ... 1M, 1.1M, ... 1B, 1.1B, ...
        float abs = Mathf.Abs(value);
        if (abs < 1000f)
        {
            return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);
        }

        float scale = 1000f;
        string suffix = "K";
        if (abs >= 1000000000f)
        {
            scale = 1000000000f;
            suffix = "B";
        }
        else if (abs >= 1000000f)
        {
            scale = 1000000f;
            suffix = "M";
        }

        float scaled = abs / scale;
        float scaledRounded1 = Mathf.Round(scaled * 10f) / 10f;

        // If rounding pushes us to 1000 of the current unit, roll up to next unit.
        if (scaledRounded1 >= 1000f)
        {
            if (suffix == "K")
            {
                scale = 1000000f;
                suffix = "M";
            }
            else if (suffix == "M")
            {
                scale = 1000000000f;
                suffix = "B";
            }

            scaled = abs / scale;
            scaledRounded1 = Mathf.Round(scaled * 10f) / 10f;
        }

        // At 100+ of a unit show no decimal: 100K, 101K, 100M, ...
        if (scaledRounded1 >= 100f)
        {
            string s = Mathf.RoundToInt(scaledRounded1).ToString(CultureInfo.InvariantCulture) + suffix;
            return value < 0f ? "-" + s : s;
        }

        // Under 100 show up to 1 decimal, dropping trailing .0: 1K, 1.1K, 10K, 10.1K, ...
        string core = scaledRounded1.ToString("0.#", CultureInfo.InvariantCulture) + suffix;
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

    private void AddPoints(float applied, Transform pos)
    {
        if (scoringLocked) return;

        EnsureCoreScoreTextBindings();

        points += applied;
        UpdateStoredScores();
        floatingTextSpawner.SpawnPointsText(pos.position, "+" + applied, applied);

        // Trigger camera shake scaled by the points earned (only for positive scores).
        if (applied > 0f)
        {
            TriggerScoreShake(applied);
        }

        // Recompute tier and apply speed when crossing Goal * N thresholds.
        UpdateGoalTierAndApplySpeed();


        ScoreChanged?.Invoke();
    }

    private void AddMult(float applied, Transform pos)
    {
        if (scoringLocked) return;

        EnsureCoreScoreTextBindings();

        mult += applied;
        UpdateStoredScores();
        floatingTextSpawner.SpawnMultText(pos.position, "x" + FormatMultiplier(applied), applied);

        // Trigger camera shake scaled by the mult earned (only for positive scores).
        if (applied > 0f)
        {
            TriggerMultShake(applied);
        }

        // Recompute tier and apply speed when crossing Goal * N thresholds.
        UpdateGoalTierAndApplySpeed();


        ScoreChanged?.Invoke();
    }

    private void AddCoins(int applied, Transform pos)
    {
        if (scoringLocked) return;

        EnsureCoreScoreTextBindings();

        gameRulesManager.AddCoinsUnscaled(applied);
        UpdateStoredScores();
        floatingTextSpawner?.SpawnGoldText(pos.position, "+$" + applied, applied);
    }

    public void SetScoringLocked(bool locked)
    {
        scoringLocked = locked;
    }

    private void UpdateStoredScores()
    {
        pointQueue.Enqueue(points);
        multQueue.Enqueue(mult);
        coinQueue.Enqueue(gameRulesManager.Coins);
    }

    public void UpdateScoreText()
    {
        if (pointQueue.Count == 0) return;
        pointsText.text = FormatPointsCompact(pointQueue.Dequeue());
        multText.text = FormatMultiplier(multQueue.Dequeue());
        coinsText.text = "$" + coinQueue.Dequeue().ToString();
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
        levelProgressOffset = 0f;
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
    /// Consumes level progress by advancing the internal offset.
    /// Use this when a level goal is reached so overflow score continues into the next level.
    /// This does NOT change points/mult or the banked total.
    /// </summary>
    public void ConsumeLevelProgress(float amount)
    {
        float a = Mathf.Max(0f, amount);
        if (a <= 0.0001f)
        {
            return;
        }

        levelProgressOffset = Mathf.Max(0f, levelProgressOffset + a);
        UpdateGoalTierAndApplySpeed();
        RefreshScoreUI();
        ScoreChanged?.Invoke();
    }

    /// <summary>
    /// Resets all score state, including level progress offset.
    /// Intended for starting a new run.
    /// </summary>
    public void ResetForNewRun()
    {
        ResetForNewRound();
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
        if (roundIndexText == null)
            return;

        int prev = _roundIndexUiLast;
        _roundIndexUiLast = roundIndex;

        roundIndexText.text = (roundIndex + 1).ToString();

        bool shouldAnimate =
            enableRoundIndexPop &&
            prev >= 0 &&
            roundIndex > prev;

        if (shouldAnimate)
        {
            CaptureRoundIndexJuiceBaselineIfNeeded();
            PlayRoundIndexPop();
        }
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
            coinsText.text = $"${coins}";

        coinsUiDisplayed = coins;
    }

    public void ApplyDeferredCoinsUi(int applied)
    {
        EnsureCoreScoreTextBindings();

        coinsUiDisplayed += applied;
        if (coinsText != null)
            coinsText.text = $"${coinsUiDisplayed}";
    }

    private void RefreshScoreUI()
    {
        EnsureCoreScoreTextBindings();
        pointsUiDisplayed = points;
        multUiDisplayed = mult;
        if (pointsText != null)
            pointsText.text = FormatPointsCompact(pointsUiDisplayed);
        if (multText != null)
            multText.text = FormatMultiplier(multUiDisplayed);
        if (roundTotalText != null)
            roundTotalText.text = roundTotal.ToString();
        if (goalText != null)
            goalText.text = _goal.ToString();
    }

    private void CaptureRoundIndexJuiceBaselineIfNeeded()
    {
        if (roundIndexText == null)
            return;

        int id = roundIndexText.GetInstanceID();
        if (_roundIndexJuiceBaselineCaptured && id == _roundIndexJuiceTextInstanceId)
            return;

        _roundIndexJuiceTextInstanceId = id;
        _roundIndexJuiceBaselineCaptured = true;

        RectTransform rt = roundIndexText.rectTransform;
        _roundIndexBaseLocalScale = rt.localScale;
        _roundIndexBaseAnchoredPos = rt.anchoredPosition;
        _roundIndexBaseColor = roundIndexText.color;
    }

    private void PlayRoundIndexPop()
    {
        if (roundIndexText == null)
            return;

        if (!_roundIndexJuiceBaselineCaptured)
        {
            CaptureRoundIndexJuiceBaselineIfNeeded();
            if (!_roundIndexJuiceBaselineCaptured)
                return;
        }

        if (_roundIndexPopRoutine != null)
            StopCoroutine(_roundIndexPopRoutine);

        _roundIndexPopRoutine = StartCoroutine(RoundIndexPopRoutine());
    }

    private System.Collections.IEnumerator RoundIndexPopRoutine()
    {
        TMP_Text text = roundIndexText;
        if (text == null)
            yield break;

        RectTransform rt = text.rectTransform;
        Vector3 baseScale = _roundIndexBaseLocalScale;
        Vector2 basePos = _roundIndexBaseAnchoredPos;
        Color baseColor = _roundIndexBaseColor;

        float duration = Mathf.Max(0.01f, roundIndexPopDuration);
        float amp = Mathf.Max(0f, roundIndexPopPeakScale - 1f);

        float t = 0f;
        while (t < duration)
        {
            if (text == null)
                yield break;

            float n = Mathf.Clamp01(t / duration);

            float k = roundIndexPopCurve != null ? roundIndexPopCurve.Evaluate(n) : Mathf.Sin(n * Mathf.PI);
            float scaleMul = 1f + (k * amp);

            rt.localScale = baseScale * scaleMul;

            if (!Mathf.Approximately(roundIndexPopYOffset, 0f))
            {
                rt.anchoredPosition = basePos + new Vector2(0f, roundIndexPopYOffset * k);
            }

            if (roundIndexPopFlashColor)
            {
                float flashT = Mathf.Clamp01(n / 0.35f);
                text.color = Color.Lerp(roundIndexPopFlashTargetColor, baseColor, flashT);
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (text != null)
        {
            rt.localScale = baseScale;
            rt.anchoredPosition = basePos;
            text.color = baseColor;
        }

        _roundIndexPopRoutine = null;
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

        float live = LiveLevelProgress;
        if (live <= 0f) return 0;

        int tier = Mathf.FloorToInt(live / _goal);
        return Mathf.Max(0, tier);
    }

    private void CaptureTimeBaseIfNeeded()
    {
        if (_timeBaseCaptured) return;
        _timeBaseCaptured = true;
        _baseTimeScale = Mathf.Max(0f, baselineTimeScale);
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
        if (cameraShake != null && cameraShake.isActiveAndEnabled){
            return;
        }

        cameraShake = CameraShake.Instance;
        if (cameraShake != null && cameraShake.isActiveAndEnabled){
            return;
        }

        cameraShake = FindFirstObjectByType<CameraShake>();

    }

    private void TriggerScoreShake(float pointsEarned)
    {
        if (cameraShake == null || !cameraShake.isActiveAndEnabled)
        {
            ResolveCameraShake();
        }

        float shaped = ShapeShakeInput(pointsEarned, shakePointsExponent);

        // Calculate duration based on points earned.
        float duration = shakeBaseDuration + (shaped * shakeDurationPerPoint);
        duration = Mathf.Clamp(duration, shakeBaseDuration, shakeMaxDuration);

        // Calculate magnitude based on points earned.
        float magnitude = shakeBaseMagnitude + (shaped * shakeMagnitudePerPoint);
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

        float shaped = ShapeShakeInput(multGained, multShakeExponent);

        // Calculate duration based on multiplier gained.
        float duration = multShakeBaseDuration + (shaped * multShakeDurationPerMult);
        duration = Mathf.Clamp(duration, multShakeBaseDuration, multShakeMaxDuration);

        // Calculate magnitude based on multiplier gained.
        float magnitude = multShakeBaseMagnitude + (shaped * multShakeMagnitudePerMult);
        magnitude = Mathf.Clamp(magnitude, multShakeBaseMagnitude, multShakeMaxMagnitude);

        cameraShake.Shake(duration, magnitude);
    }

    private static float ShapeShakeInput(float value, float exponent)
    {
        if (value <= 0f)
        {
            return 0f;
        }

        float safeExponent = Mathf.Max(0.0001f, exponent);
        return Mathf.Pow(value, safeExponent);
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
