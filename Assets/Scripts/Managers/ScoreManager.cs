// Updated with Antigravity by jjmil on 2026-04-07 (mult rework: persistent board multiplier, applied at earn-time).
// Updated with Antigravity by jjmil on 2026-03-29.
// Updated with Cursor (Composer) by assistant on 2026-03-31 (Phase 7: EnsureRefs in Awake only; CoinController for coin awards).
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
    [SerializeField] private double points;
    [SerializeField] private float mult;
    [SerializeField] private double roundTotal;

    public double Points => points;
    public float Mult => mult;
    public double RoundTotal => roundTotal;

    // Mult cap (set per-ship; default = uncapped)
    private float _multCap = float.MaxValue;
    // Frenzy bonus sits on top of the cap — not subject to it
    private float _frenzyMult;

    public float MultCap => _multCap;
    public float FrenzyMult => _frenzyMult;
    /// <summary>Effective multiplier used for scoring: capped earned mult + uncapped frenzy bonus.</summary>
    public float EffectiveMult => mult + _frenzyMult;
    public bool IsFrenzyActive => _frenzyMult > 0f;

    private double displayPoints;
    private float displayMult = 1f;

    public double DisplayPoints => displayPoints;
    public float DisplayMult => displayMult;
    public double DisplayRoundTotal => roundTotal + displayPoints;


    [Header("Level Progress (runtime)")]
    [SerializeField, Tooltip("Read-only at runtime. Tracks how much cumulative score has been consumed by level-ups.")]
    private double levelProgressOffset;

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

    public event Action<double, double> PointsAdded; // (appliedAmount, currentStoredPoints)
    public event Action<float, float> MultAdded; // (appliedAmount, currentStoredMult)
    public event Action MultReset;
    public event Action<int, int> CoinsAdded; // (appliedAmount, currentCoins)

    // Properties

    public float Goal => _goal;
    public double CumulativeGoal => Math.Max(0d, levelProgressOffset + _goal);
    public int GoalTier => _goalTier;
    public float ScoreAwardMultiplier => 1f + (_goalTier * scoreIncreasePerGoalTier);
    public float SpeedMultiplier => 1f + (_goalTier * speedIncreasePerGoalTier);
    public float SpeedIncreasePerGoalTier => speedIncreasePerGoalTier;
    public float ScoreIncreasePerGoalTier => scoreIncreasePerGoalTier;
    public double LiveRoundTotal => roundTotal + points;
    public double LiveLevelProgress => Math.Max(0d, LiveRoundTotal - levelProgressOffset);
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
    [SerializeField] private float pointsModifierMultiplier;
    [SerializeField] private float multMultiplier;
    [SerializeField] private float multModifierMultiplier;
    [SerializeField] private int coinMultiplier;
    [SerializeField] private float coinModifierMultiplier;

    public float PointsModifierMultiplier => pointsModifierMultiplier;
    public float MultModifierMultiplier => multModifierMultiplier;
    public float CoinModifierMultiplier => coinModifierMultiplier;

    public void SetModifierMultipliers(
        float pointsMod, float multMod, float coinMod)
    {
        pointsModifierMultiplier = Mathf.Max(0f, pointsMod);
        multModifierMultiplier = Mathf.Max(0f, multMod);
        coinModifierMultiplier = Mathf.Max(0f, coinMod);
        ApplySpeedFromTier(force: true);
    }

    public void ResetModifierMultipliers()
    {
        pointsModifierMultiplier = 1f;
        multModifierMultiplier = 1f;
        coinModifierMultiplier = 1f;
        ApplySpeedFromTier(force: true);
    }

    public void AddRawPoints(double amount)
    {
        points += amount;
        ScoreChanged?.Invoke();
    }

    public void AddRawMult(float amount)
    {
        mult += amount;
        if (mult < 1f) mult = 1f;
        if (_multCap > 0f && _multCap < float.MaxValue)
            mult = Mathf.Min(mult, _multCap);
        displayMult = mult;
        MultAdded?.Invoke(amount, mult);
        ScoreChanged?.Invoke();
    }

    /// <summary>Sets the maximum mult the player can earn (from their ship). Does not affect frenzy bonus.</summary>
    public void SetMultCap(float cap)
    {
        _multCap = Mathf.Max(1f, cap);
        // Clamp current mult to the new cap
        if (mult > _multCap)
        {
            mult = _multCap;
            displayMult = mult;
            ScoreChanged?.Invoke();
        }
    }

    /// <summary>Adds a frenzy multiplier bonus that bypasses the cap. Call from frenzy systems only.</summary>
    public void AddFrenzyMult(float amount)
    {
        _frenzyMult += amount;
        if (_frenzyMult < 0f) _frenzyMult = 0f;
        SteamAchievements.CheckMultMilestone(EffectiveMult);
        ScoreChanged?.Invoke();
    }

    /// <summary>Removes the frenzy multiplier bonus entirely.</summary>
    public void RemoveFrenzyMult()
    {
        _frenzyMult = 0f;
        ScoreChanged?.Invoke();
    }

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
        points = 0d;
        mult = 1f;
        roundTotal = 0d;
        _goal = 0f;
        _goalTier = 0;

        displayPoints = 0d;
        displayMult = 1f;

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

        double scaled = applied * EffectiveMult;
        points += scaled;

        if (floatingTextSpawner != null)
        {
            floatingTextSpawner.SpawnPointsText(pos.position, "+" + scaled, (float)scaled, () =>
            {
                displayPoints += scaled;
                ScoreChanged?.Invoke();
            }, popupAnchorOffset);
        }
        else
        {
            displayPoints += scaled;
        }

        PointsAdded?.Invoke(scaled, points);
        UpdateGoalTierAndApplySpeed();
        ScoreChanged?.Invoke();
    }

    private void AddMult(float applied, Transform pos)
    {
        if (scoringLocked || applied == 0) return;

        mult += applied;
        if (_multCap > 0f && _multCap < float.MaxValue)
            mult = Mathf.Min(mult, _multCap);

        SteamAchievements.CheckMultMilestone(EffectiveMult);

        if (floatingTextSpawner != null)
        {
            floatingTextSpawner.SpawnMultText(pos.position, "+x" + (Mathf.Round(applied * 100f) / 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), applied, () => 
            {
                displayMult += applied;
                ScoreChanged?.Invoke();
            });
        }
        else
        {
            displayMult += applied;
        }

        MultAdded?.Invoke(applied, mult);
        UpdateGoalTierAndApplySpeed();
        ScoreChanged?.Invoke();
    }

    private void AddCoins(int applied, Transform pos)
    {
        if (scoringLocked || applied == 0) return;

        var cc = ServiceLocator.Get<CoinController>();
        int actual = cc != null ? cc.AddCoinsUnscaledDeferredUi(applied) : 0;

        if (floatingTextSpawner != null && actual > 0)
            floatingTextSpawner.SpawnGoldText(pos.position, "+$" + actual, actual,
                () => cc?.ApplyDeferredCoinsUi(actual));

        int totalCoins = cc?.Coins ?? 0;
        CoinsAdded?.Invoke(actual, totalCoins);
        ScoreChanged?.Invoke();
    }

    public void SetScoringLocked(bool locked)
    {
        scoringLocked = locked;
    }

    public void ResetMultiplier()
    {
        mult = 1f;
        displayMult = 1f;
        _frenzyMult = 0f;
        MultReset?.Invoke();
        ScoreChanged?.Invoke();
    }

    public double BankCurrentBallScore()
    {
        double banked = points;
        roundTotal += banked;

        BallBanked?.Invoke();
        points = 0d;
        displayPoints = 0d;
        displayMult = mult; // Sync display mult just in case
        _componentHitAssistAccelerationBonus = 0f;
        _componentHitTimeScaleBonus = 0f;
        _componentHitScoreBonus = 0f;

        UpdateGoalTierAndApplySpeed();
        ScoreChanged?.Invoke();
        return banked;
    }

    public void ResetForNewRound()
    {
        levelProgressOffset = 0d;
        roundTotal = 0d;
        points = 0d;
        BallBanked?.Invoke();
        mult = 1f;
        _frenzyMult = 0f;
        displayPoints = 0d;
        displayMult = 1f;
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
        double a = Math.Max(0d, amount);
        if (a <= 0.0001d) return;

        levelProgressOffset = Math.Max(0d, levelProgressOffset + a);
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

        double live = LiveLevelProgress;
        if (live <= 0d) return 0;

        int tier = (int)Math.Floor(live / _goal);
        return Math.Max(0, tier);
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
        float effectiveBase = _baseTimeScale + _componentHitTimeScaleBonus;
        float targetScale = effectiveBase * Mathf.Max(0f, SpeedMultiplier) * extMult * requestMult;
        
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
