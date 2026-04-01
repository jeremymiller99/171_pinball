// Updated with Antigravity by jjmil on 2026-03-29.
// Updated with Cursor (Composer) by assistant on 2026-03-31 (CoinController for coin awards).
// Originally generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using UnityEngine;
using System;
using System.Collections.Generic;

public enum TypeOfScore
{
    points,
    mult,
    coins
}

public class ScoreManager : MonoBehaviour
{
    // Conceptually, `points` are the current ball's points.
    public float points;
    public float mult;

    // Total banked score across the current round (sum of each drained ball's banked score).
    public float roundTotal;

    [Header("Level Progress (runtime)")]
    [SerializeField, Tooltip("Read-only at runtime. Tracks how much cumulative score has been consumed by level-ups.")]
    private float levelProgressOffset;

    [SerializeField] private FloatingTextSpawner floatingTextSpawner;

    [Header("Scoring Control")]
    [SerializeField] private bool scoringLocked;

    [Header("Round Goal Tier Scaling")]
    [Tooltip("If enabled: each time LiveRoundTotal crosses another multiple of Goal (Goal * N), " +
             "the game speeds up and points awarded are increased additively.")]
    [SerializeField] private bool enableGoalTierScaling = false;

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
             "Set to 1.1 to make the whole game run 10% faster.")]
    [Min(0f)]
    [SerializeField] private float baselineTimeScale = 1.1f;

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

    [Header("Component Hit Bonuses (Post-Level)")]
    [Tooltip("After completing a level, each component hit adds this much to ball anti-stall assist acceleration (scaled back).")]
    [SerializeField] private float componentHitAssistAccelerationIncrement = 0f;
    [Tooltip("After completing a level, each component hit adds this much to Time.timeScale (e.g. 0.02 = +2% per hit).")]
    [SerializeField] private float componentHitTimeScaleIncrement = 0f;
    [Tooltip("After completing a level, each component hit adds this much to the score multiplier (e.g. 0.05 = +5% per hit).")]
    [SerializeField] private float componentHitScoreIncrement = 0f;

    private float _componentHitAssistAccelerationBonus;
    private float _componentHitTimeScaleBonus;
    private float _componentHitScoreBonus;
    private bool _hasCompletedLevelThisRound;

    [SerializeField] private int totalComponentHits;

    private float _goal;

    [SerializeField, Tooltip("Read-only at runtime. Increments at Goal * N.")]
    private int _goalTier;

    private bool _timeBaseCaptured;
    private float _baseTimeScale = 1f;
    private float _baseFixedDeltaTime = 0.02f;

    // Events

    public event Action ScoreChanged;
    /// <summary>Fired when live ball score is cleared (banked or round/run reset) so presentation (e.g. coin pitch ramp) can reset.</summary>
    public event Action BallBanked;
    public event Action<int> GoalTierChanged;

    public event Action<float, float> PointsAdded; // (appliedAmount, currentStoredPoints)
    public event Action<float, float> MultAdded; // (appliedAmount, currentStoredMult)
    public event Action<int, int> CoinsAdded; // (appliedAmount, currentCoins)

    // Properties

    public float Goal => _goal;
    public float CumulativeGoal => Mathf.Max(0f, levelProgressOffset + _goal);
    public int GoalTier => _goalTier;
    public float ScoreAwardMultiplier => 1f + (_goalTier * scoreIncreasePerGoalTier);
    public float SpeedMultiplier => 1f + (_goalTier * speedIncreasePerGoalTier);
    public float SpeedIncreasePerGoalTier => speedIncreasePerGoalTier;
    public float ScoreIncreasePerGoalTier => scoreIncreasePerGoalTier;
    public float LiveRoundTotal => roundTotal + (points * mult);
    public float LiveLevelProgress => Mathf.Max(0f, LiveRoundTotal - levelProgressOffset);
    public float ComponentHitAssistAccelerationBonus => _componentHitAssistAccelerationBonus;
    public float ExternalTimeScaleMultiplier => externalTimeScaleMultiplier;
    public float ExternalScoreAwardMultiplier => externalScoreAwardMultiplier;

    public void SetExternalScoreAwardMultiplier(float multiplier)
    {
        externalScoreAwardMultiplier = Mathf.Max(0f, multiplier);
    }

    private readonly Dictionary<int, float> _timeScaleRequestBySourceId = new Dictionary<int, float>();
    private float _timeScaleRequestMin = 1f;
    public float TimeScaleRequestMultiplier => _timeScaleRequestMin;

    [SerializeField] private float pointMultiplier;
    public float pointsModifierMultiplier;
    [SerializeField] private float multMultiplier;
    public float multModifierMultiplier;
    [SerializeField] private int coinMultiplier;
    public float coinModifierMultiplier;

    public float modifierTimeScaleMultiplier = 1f;

    void Awake()
    {
        ServiceLocator.Register<ScoreManager>(this);
        EnsureRefs();
    }  

    void EnsureRefs()
    {
        if (floatingTextSpawner == null)
            floatingTextSpawner = ServiceLocator.Get<FloatingTextSpawner>();
    }

    public float GetAppliedPointsForDisplay(float rawAmount)
    {
        return rawAmount * pointMultiplier * pointsModifierMultiplier;
    }

    public int TotalComponentHits => totalComponentHits;

    public void RegisterComponentHit()
    {
        totalComponentHits++;

        if (_hasCompletedLevelThisRound)
        {
            _componentHitAssistAccelerationBonus += componentHitAssistAccelerationIncrement;
            _componentHitTimeScaleBonus += componentHitTimeScaleIncrement;
            _componentHitScoreBonus += componentHitScoreIncrement;
            ApplySpeedFromTier(force: true);
        }
    }

    private void Start()
    {
        points = 0f;
        mult = 1f;
        roundTotal = 0f;
        _goal = 0f;
        _goalTier = 0;

        CaptureTimeBaseIfNeeded();
        ApplySpeedFromTier(force: true);

        ScoreChanged?.Invoke();
    }

    public void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        AddScore(amount, typeOfScore, pos, Vector2.zero);
    }

    public void AddScore(float amount, TypeOfScore typeOfScore, Transform pos, Vector2 popupAnchorOffset)
    {
        RegisterComponentHit();
        float componentScoreMult = _hasCompletedLevelThisRound ? (1f + _componentHitScoreBonus) : 1f;
        amount *= componentScoreMult;

        switch(typeOfScore)
        {
            case TypeOfScore.points:
                float appliedPts = amount * pointMultiplier * pointsModifierMultiplier * externalScoreAwardMultiplier;
                AddPoints(appliedPts, pos, popupAnchorOffset);
                break;
            case TypeOfScore.mult:
                float appliedMult = amount * multMultiplier * multModifierMultiplier * externalScoreAwardMultiplier;
                AddMult(appliedMult, pos);
                break;
            case TypeOfScore.coins:
                int appliedCoins = Mathf.RoundToInt(amount * coinMultiplier * coinModifierMultiplier * externalScoreAwardMultiplier);
                AddCoins(appliedCoins, pos);
                break;
        }
    }

    private void AddPoints(float applied, Transform pos, Vector2 popupAnchorOffset = default)
    {
        if (scoringLocked || applied == 0) return;

        points += applied;
        
        if (floatingTextSpawner != null)
            floatingTextSpawner.SpawnPointsText(pos.position, "+" + applied, applied, null, popupAnchorOffset);

        PointsAdded?.Invoke(applied, points);
        UpdateGoalTierAndApplySpeed();
        ScoreChanged?.Invoke();
    }

    private void AddMult(float applied, Transform pos)
    {
        if (scoringLocked || applied == 0) return;

        mult += applied;

        if (floatingTextSpawner != null)
            floatingTextSpawner.SpawnMultText(pos.position, "x" + ScoreUIController.FormatMultiplier(applied), applied);

        MultAdded?.Invoke(applied, mult);
        UpdateGoalTierAndApplySpeed();
        ScoreChanged?.Invoke();
    }

    private void AddCoins(int applied, Transform pos)
    {
        if (scoringLocked || applied == 0) return;

        EnsureRefs();
        ServiceLocator.Get<CoinController>()?.AddCoinsUnscaled(applied);

        if (floatingTextSpawner != null)
            floatingTextSpawner.SpawnGoldText(pos.position, "+$" + applied, applied);

        int totalCoins = ServiceLocator.Get<CoinController>()?.Coins ?? 0;
        CoinsAdded?.Invoke(applied, totalCoins);
        ScoreChanged?.Invoke();
    }

    public void SetScoringLocked(bool locked)
    {
        scoringLocked = locked;
    }

    public float BankCurrentBallScore()
    {
        return BankCurrentBallScore(1f);
    }

    public float BankCurrentBallScore(float bankMultiplier)
    {
        float m = bankMultiplier;
        if (m <= 0f) m = 1f;

        float banked = points * mult * m;
        roundTotal += banked;

        BallBanked?.Invoke();
        points = 0f;
        mult = 1f;
        _componentHitAssistAccelerationBonus = 0f;
        _componentHitTimeScaleBonus = 0f;
        _componentHitScoreBonus = 0f;

        UpdateGoalTierAndApplySpeed();
        ScoreChanged?.Invoke();
        return banked;
    }

    public void ResetForNewRound()
    {
        levelProgressOffset = 0f;
        roundTotal = 0f;
        points = 0f;
        BallBanked?.Invoke();
        mult = 1f;
        _goalTier = 0;
        totalComponentHits = 0;
        _componentHitAssistAccelerationBonus = 0f;
        _componentHitTimeScaleBonus = 0f;
        _componentHitScoreBonus = 0f;
        _hasCompletedLevelThisRound = false;

        ApplySpeedFromTier(force: true);
        ScoreChanged?.Invoke();
    }

    public void ResetGameSpeedOnShopReturn()
    {
        _componentHitAssistAccelerationBonus = 0f;
        _componentHitTimeScaleBonus = 0f;
        _componentHitScoreBonus = 0f;
        ApplySpeedFromTier(force: true);
    }

    public void ConsumeLevelProgress(float amount)
    {
        float a = Mathf.Max(0f, amount);
        if (a <= 0.0001f) return;

        levelProgressOffset = Mathf.Max(0f, levelProgressOffset + a);
        _hasCompletedLevelThisRound = true;
        UpdateGoalTierAndApplySpeed();
        ScoreChanged?.Invoke();
    }

    public void ResetForNewRun()
    {
        ResetForNewRound();
    }

    public void SetGoal(float goal)
    {
        _goal = goal;
        UpdateGoalTierAndApplySpeed();
        ScoreChanged?.Invoke();
    }

    public void SetTimeScaleRequest(UnityEngine.Object source, float multiplier)
    {
        if (source == null) return;
        int id = source.GetInstanceID();
        _timeScaleRequestBySourceId[id] = Mathf.Max(1f, multiplier);
        RecomputeTimeScaleRequestMin();
        ApplySpeedFromTier(force: true);
    }

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

    public void ResetExternalMultipliers()
    {
        externalTimeScaleMultiplier = 1f;
        externalScoreAwardMultiplier = 1f;
        modifierTimeScaleMultiplier = 1f;
        ClearAllTimeScaleRequests();
        ApplySpeedFromTier(force: true);
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

        if (Time.timeScale <= 0f) return;

        float requestMult = Mathf.Max(1f, _timeScaleRequestMin);
        float extMult = Mathf.Max(1f, externalTimeScaleMultiplier);
        float modTimeScale = Mathf.Max(0.1f, modifierTimeScaleMultiplier);
        float effectiveBase = _baseTimeScale + _componentHitTimeScaleBonus;
        float targetScale = effectiveBase * Mathf.Max(0f, SpeedMultiplier) * extMult * requestMult * modTimeScale;
        
        if (maxTimeScale > 0f)
            targetScale = Mathf.Min(targetScale, maxTimeScale);

        if (!force && Mathf.Approximately(Time.timeScale, targetScale))
            return;

        Time.timeScale = targetScale;

        float targetFixed = _baseFixedDeltaTime * Mathf.Max(0f, Time.timeScale);
        if (maxFixedDeltaTime > 0f)
            targetFixed = Mathf.Min(targetFixed, maxFixedDeltaTime);

        Time.fixedDeltaTime = Mathf.Max(0.0001f, targetFixed);
    }
}
