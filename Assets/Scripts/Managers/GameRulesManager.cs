// Updated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-19.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameRulesManager : MonoBehaviour
{
    [Header("Scoring")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ScoreTallyAnimator scoreTallyAnimator;
    [SerializeField] private float pointsPerCoin = 100f;

    [Header("Popups (optional)")]
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;
    [SerializeField] private bool showLevelUpCoinsPopup = true;

    public enum GoalScalingMode
    {
        LegacyList = 0,
        Exponential = 1
    }

    [Header("Level Goal Scaling")]
    [SerializeField] private GoalScalingMode goalScalingMode = GoalScalingMode.Exponential;

    [Min(0f)]
    [SerializeField] private float baseGoal = 500f;

    [Min(1f)]
    [SerializeField] private float goalGrowthPerLevel = 1.35f;

    [Tooltip("0 means no rounding. Otherwise, rounds the computed exponential goal to nearest step.")]
    [Min(0f)]
    [SerializeField] private float goalRoundingStep = 100f;

    [Header("Legacy level goals (optional)")]
    [SerializeField] private List<float> goalByRound = new List<float> { 500f, 800f, 1200f, 1700f, 2300f, 3000f, 4000f };

    // Generated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-17.
    [Header("Coins (score conversion cap)")]
    [Tooltip("Hard cap on coins awarded from round score when clearing the goal.\n" +
             "0 or less means no cap.")]
    [Min(0)]
    [SerializeField] private int maxCoinsFromRoundTotal = 20;

    [Header("Balls / Rounds")]
    [SerializeField] private int startingMaxBalls = 5;
    [SerializeField] private bool autoStartOnPlay = true;

    [Header("Levels")]
    [Min(0)]
    [SerializeField] private int coinsPerLevelUp = 10;

    [Header("Ball Spawning")]
    [Tooltip("Required: BallSpawner pre-spawns a hand of balls and lerps the next ball to spawnPoint.")]
    [SerializeField] private BallSpawner ballSpawner;

    [Header("Ball Loadout (hand)")]
    [Tooltip("Optional: starting ball prefabs for the player's hand/loadout. If empty, falls back to repeating Ball Prefab.")]
    [SerializeField] private List<GameObject> startingBallLoadout = new List<GameObject>();

    [Tooltip("Preferred: starting ball definitions for the player's hand/loadout.")]
    [SerializeField] private List<BallDefinition> startingBallLoadoutDefinitions = new List<BallDefinition>();

    [Header("UI (optional)")]
    [SerializeField] private GameObject shopCanvasRoot;
    [SerializeField] private GameObject roundFailedUIRoot;
    [SerializeField] private GameObject homeRunUIRoot;
    [SerializeField] private TMP_Text homeRunMessageText;

    [Header("Transitions (optional)")]
    [SerializeField] private ShopTransitionController shopTransitionController;

    [Header("Debug (read-only at runtime)")]
    [SerializeField] private int roundIndex;
    [SerializeField] private int maxBalls;
    [SerializeField] private int ballsRemaining;
    [SerializeField] private int coins;
    [SerializeField] private float roundTotal;

    private bool runActive;
    private bool shopOpen;
    private bool _drainProcessing;
    private bool _levelUpProcessing;

    // If true, the NEXT drain after at least one level-up will not consume the ball from the loadout.
    private bool _shopBallSaveAvailable;

    // Tracks which loadout slot the currently-active ball came from (set when spawning).
    private int _activeBallSlotIndex = -1;

    // Which definitions will be used for the next round's hand (size == maxBalls).
    private readonly List<BallDefinition> _ballLoadout = new List<BallDefinition>();

    // Active level modifier (rolled per level).
    private RoundModifierDefinition _activeModifier;
    private RoundData _currentRoundData;
    /// <summary>When Unlucky Day (or similar) applies two devil modifiers, this holds the combined goal modifier for GetGoalForRound.</summary>
    private float _effectiveGoalModifierForRound;
    /// <summary>When Unlucky Day is active, the two devil modifiers that were picked (for display in orange).</summary>
    private List<RoundModifierDefinition> _unluckyDayActiveModifiers;
    /// <summary>Remaining flipper uses this round. -1 = no limit.</summary>
    private int _flipperUsesRemaining = -1;

    [Header("Level Modifiers (runtime)")]
    [Min(1)]
    [SerializeField] private int guaranteedBagSizeFallback = 7;

    [Header("Default Modifier Pools (when no challenge)")]
    [Tooltip("Used so every round has an angel or devil modifier even in Quick Run. Assign at least one.")]
    [SerializeField] private RoundModifierPool defaultAngelPool;
    [SerializeField] private RoundModifierPool defaultDevilPool;

    private System.Random _levelModifierRng;
    private readonly List<RoundType> _guaranteedTypeBag = new List<RoundType>();
    private int _guaranteedTypeBagPos;

    /// <summary>
    /// Fired whenever a new round is started (after goal/round UI is reset).
    /// Useful for per-round systems like Frenzy.
    /// </summary>
    public event Action RoundStarted;

    /// <summary>
    /// Fired whenever the current level changes (level-up), after the modifier is rolled/applied.
    /// </summary>
    public event Action LevelChanged;

    /// <summary>
    /// Fired when the shop is opened for the current round.
    /// </summary>
    public event Action ShopOpened;

    /// <summary>
    /// Fired when the shop is closed.
    /// </summary>
    public event Action ShopClosed;

    public int LevelIndex => roundIndex;
    public float TotalScore => roundTotal;

    public int RoundIndex => roundIndex;
    public int MaxBalls => maxBalls;
    public int BallsRemaining => ballsRemaining;
    public int Coins => coins;
    public float RoundTotal => roundTotal;
    public float CurrentGoal => GetGoalForRound(roundIndex);
    public int BallLoadoutCount
    {
        get
        {
            EnsureLoadoutWithinCapacity();

            int count = 0;
            for (int i = 0; i < _ballLoadout.Count; i++)
            {
                BallDefinition def = _ballLoadout[i];
                if (def != null && def.Prefab != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
    public List<GameObject> ActiveBalls => ballSpawner != null ? ballSpawner.ActiveBalls : null;
    public bool IsShopOpen => shopOpen;

    /// <summary>
    /// The currently active round modifier, if any.
    /// </summary>
    public RoundModifierDefinition ActiveModifier => _activeModifier;

    /// <summary>
    /// The current round's data (type, modifier).
    /// </summary>
    public RoundData CurrentRoundData => _currentRoundData;

    /// <summary>
    /// Returns the score multiplier from the active modifier (1.0 if no modifier).
    /// </summary>
    public float GetModifierScoreMultiplier() => _activeModifier?.scoreMultiplier ?? 1f;

    /// <summary>
    /// Returns the coin multiplier from the active modifier (1.0 if no modifier).
    /// </summary>
    public float GetModifierCoinMultiplier() => _activeModifier?.coinMultiplier ?? 1f;

    /// <summary>
    /// Returns true if the multiplier is disabled by the active modifier.
    /// </summary>
    public bool IsMultiplierDisabled() => _activeModifier?.disableMultiplier ?? false;

    /// <summary>
    /// Remaining flipper/paddle uses this round. -1 when there is no limit.
    /// </summary>
    public int RemainingFlipperUses => _flipperUsesRemaining;

    /// <summary>
    /// True when the active modifier limits flipper uses this round.
    /// </summary>
    public bool HasFlipperLimit => _flipperUsesRemaining >= 0;

    /// <summary>
    /// Call when the player activates a flipper. Returns true if the flipper may activate;
    /// false if the limit was exceeded (round is lost and flipper should not activate).
    /// </summary>
    public bool TryConsumeFlipperUse()
    {
        if (_flipperUsesRemaining < 0)
            return true;
        if (_flipperUsesRemaining > 0)
        {
            _flipperUsesRemaining--;
            return true;
        }
        TriggerRoundFailed();
        return false;
    }

    /// <summary>
    /// Immediately end the round as failed (e.g. when flipper limit exceeded).
    /// </summary>
    public void TriggerRoundFailed()
    {
        ShowRoundFailed();
    }

    private void ResolveBallSpawner(bool logIfMissing)
    {
        if (ballSpawner != null)
            return;

        // In additive scene setups, the BallSpawner may exist in a different loaded scene than this object.
        // Always resolve by searching across all loaded scenes.
        BallSpawner[] found;

        found = FindObjectsByType<BallSpawner>(FindObjectsSortMode.None);


        if (found == null || found.Length == 0)
        {
            if (logIfMissing)
                Debug.LogError($"{nameof(GameRulesManager)} could not find any {nameof(BallSpawner)} in loaded scenes.", this);
            return;
        }

        if (found.Length == 1)
        {
            ballSpawner = found[0];
            return;
        }

        // Prefer a spawner in the same scene (usually GameplayCore), otherwise take the first.
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].gameObject.scene == gameObject.scene)
            {
                ballSpawner = found[i];
                break;
            }
        }
        if (ballSpawner == null)
            ballSpawner = found[0];

        Debug.LogWarning($"{nameof(GameRulesManager)} found multiple {nameof(BallSpawner)} instances; using '{ballSpawner.name}'. Remove duplicates for a single source of truth.", ballSpawner);
    }

    private void ResolveScoreManager(bool logIfMissing)
    {
        if (scoreManager != null)
            return;

        ScoreManager[] found;
#if UNITY_2022_2_OR_NEWER
        found = FindObjectsByType<ScoreManager>(FindObjectsSortMode.None);
#else
        found = FindObjectsOfType<ScoreManager>(includeInactive: false);
#endif

        if (found == null || found.Length == 0)
        {
            if (logIfMissing)
                Debug.LogError($"{nameof(GameRulesManager)} could not find any {nameof(ScoreManager)} in loaded scenes.", this);
            return;
        }

        scoreManager = found[0];

        if (found.Length > 1)
            Debug.LogWarning($"{nameof(GameRulesManager)} found multiple {nameof(ScoreManager)} instances; using '{scoreManager.name}'. Remove duplicates for a single source of truth.", scoreManager);
    }

    private void ResolveScoreTallyAnimator(bool logIfMissing)
    {
        if (scoreTallyAnimator != null)
            return;

        ScoreTallyAnimator[] found;
        found = FindObjectsByType<ScoreTallyAnimator>(FindObjectsSortMode.None);


        if (found == null || found.Length == 0)
        {
            if (logIfMissing)
                Debug.LogError($"{nameof(GameRulesManager)} could not find any {nameof(ScoreTallyAnimator)} in loaded scenes.", this);
            return;
        }

        scoreTallyAnimator = found[0];

        if (found.Length > 1)
            Debug.LogWarning($"{nameof(GameRulesManager)} found multiple {nameof(ScoreTallyAnimator)} instances; using '{scoreTallyAnimator.name}'. Remove duplicates for a single source of truth.", scoreTallyAnimator);
    }

    private void ResolveFloatingTextSpawner(bool logIfMissing)
    {
        if (floatingTextSpawner != null)
        {
            return;
        }

        FloatingTextSpawner[] found;
        found = FindObjectsByType<FloatingTextSpawner>(FindObjectsSortMode.None);

        if (found == null || found.Length == 0)
        {
            if (logIfMissing)
            {
                Debug.LogError($"{nameof(GameRulesManager)} could not find any {nameof(FloatingTextSpawner)} in loaded scenes.", this);
            }

            return;
        }

        if (found.Length == 1)
        {
            floatingTextSpawner = found[0];
            return;
        }

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].gameObject.scene == gameObject.scene)
            {
                floatingTextSpawner = found[i];
                break;
            }
        }

        if (floatingTextSpawner == null)
        {
            floatingTextSpawner = found[0];
        }

        Debug.LogWarning($"{nameof(GameRulesManager)} found multiple {nameof(FloatingTextSpawner)} instances; using '{floatingTextSpawner.name}'. Remove duplicates for a single source of truth.", floatingTextSpawner);
    }

    /// <summary>
    /// Returns a snapshot copy of the current ball loadout (one prefab per hand slot).
    /// Safe to enumerate without risking external mutation.
    /// </summary>
    public List<BallDefinition> GetBallLoadoutSnapshot()
    {
        EnsureLoadoutWithinCapacity();
        return new List<BallDefinition>(_ballLoadout);
    }

    private List<GameObject> GetBallLoadoutPrefabSnapshot()
    {
        EnsureLoadoutWithinCapacity();

        var prefabs = new List<GameObject>(_ballLoadout.Count);
        for (int i = 0; i < _ballLoadout.Count; i++)
        {
            BallDefinition def = _ballLoadout[i];
            prefabs.Add(def != null ? def.Prefab : null);
        }

        return prefabs;
    }

    private void Awake()
    {
        ResolveScoreManager(logIfMissing: false);
        ResolveScoreTallyAnimator(logIfMissing: false);
        ResolveFloatingTextSpawner(logIfMissing: false);

        if (goalByRound == null || goalByRound.Count == 0)
        {
            goalByRound = new List<float> { 500f, 800f, 1200f, 1700f, 2300f, 3000f, 4000f };
        }

        ResolveBallSpawner(logIfMissing: false);

        if (shopTransitionController == null)
        {
            shopTransitionController = FindFirstObjectByType<ShopTransitionController>();

        }
    }

    private void Start()
    {
        ResolveScoreManager(logIfMissing: false);
        ResolveScoreTallyAnimator(logIfMissing: false);
        ResolveFloatingTextSpawner(logIfMissing: false);

        // In the additive-board architecture, the board (containing the BallSpawner) is loaded 
        // after GameplayCore, then StartRun() is called by RunFlowController.
        // Don't log an error here since the board scene may not be loaded yet.
        ResolveBallSpawner(logIfMissing: false);

        if (autoStartOnPlay)
        {
            StartRun();
        }
    }

    public void StartRun()
    {
        ResolveBallSpawner(logIfMissing: true);
        if (ballSpawner == null)
            return;

        runActive = true;
        shopOpen = false;
        _drainProcessing = false;
        _levelUpProcessing = false;
        _shopBallSaveAvailable = false;
        _activeBallSlotIndex = -1;
        roundIndex = 0;
        coins = 0;
        maxBalls = Mathf.Max(1, startingMaxBalls);
        InitializeLoadoutForNewRun();

        ResolveScoreManager(logIfMissing: false);
        ResolveScoreTallyAnimator(logIfMissing: false);

        roundTotal = 0f;
        ballsRemaining = BallLoadoutCount;

        InitLevelModifierRolling();
        ApplyLevelModifier();

        if (scoreManager != null)
        {
            scoreManager.ResetForNewRun();
            scoreManager.SetRoundIndex(roundIndex);
            scoreManager.SetGoal(CurrentGoal);
            scoreManager.SetBallsRemaining(ballsRemaining);
            scoreManager.SetCoins(coins);

            scoreManager.ScoreChanged -= OnScoreChanged;
            scoreManager.ScoreChanged += OnScoreChanged;
        }

        StartRound();
    }

    public void StartRound()
    {
        ResolveBallSpawner(logIfMissing: true);
        if (ballSpawner == null)
            return;

        ResolveScoreManager(logIfMissing: false);
        ResolveScoreTallyAnimator(logIfMissing: false);

        shopOpen = false;
        SetShopOpen(false);
        SetRoundFailedOpen(false);

        EnsureLoadoutWithinCapacity();
        ballsRemaining = BallLoadoutCount;

        if (scoreManager != null)
        {
            // Do NOT reset totals here; levels are cumulative.
            scoreManager.SetRoundIndex(roundIndex);
            scoreManager.SetGoal(CurrentGoal);
            scoreManager.SetBallsRemaining(ballsRemaining);
            scoreManager.SetCoins(coins);
        }

        RoundStarted?.Invoke();

        ballSpawner.ClearAll();
        ballSpawner.BuildHandFromPrefabs(GetBallLoadoutPrefabSnapshot());

        if (ballsRemaining > 0)
        {
            _activeBallSlotIndex = -1;
            SpawnBall();
            return;
        }

        // If the player has no balls to start the round, they immediately lose the round.
        ShowRoundFailed();
    }

    private void InitLevelModifierRolling()
    {
        var session = GameSession.Instance;
        int seed = session != null ? session.Seed : Environment.TickCount;
        _levelModifierRng = new System.Random(seed);

        _guaranteedTypeBag.Clear();
        _guaranteedTypeBagPos = 0;
    }

    private void ApplyLevelModifier()
    {
        _activeModifier = null;

        // Reset modifier multipliers to 1 when the previous modifier ends (peer rule: reset when modifier wears off).
        ResolveScoreManager(logIfMissing: false);
        if (scoreManager != null)
        {
            scoreManager.pointsModifierMultiplier = 1f;
            scoreManager.multModifierMultiplier = 1f;
            scoreManager.coinModifierMultiplier = 1f;
            scoreManager.modifierTimeScaleMultiplier = 1f;
        }

        RoundType type = RoundType.Normal;
        RoundModifierDefinition modifier = null;

        var session = GameSession.Instance;
        ChallengeModeDefinition challenge = session != null ? session.ActiveChallenge : null;
        if (challenge != null && challenge.HasModifierPools)
        {
            type = RollModifierType(challenge);
            if (type == RoundType.Normal)
            {
                if (_levelModifierRng == null)
                    _levelModifierRng = new System.Random(Environment.TickCount);
                type = _levelModifierRng.NextDouble() < 0.5 ? RoundType.Angel : RoundType.Devil;
            }
            modifier = RollModifierFromPool(challenge, type);
            if (modifier == null && type == RoundType.Angel && challenge.devilPool != null && challenge.devilPool.ValidCount > 0)
                modifier = challenge.devilPool.GetRandomModifier(_levelModifierRng);
            if (modifier == null && type == RoundType.Devil && challenge.angelPool != null && challenge.angelPool.ValidCount > 0)
                modifier = challenge.angelPool.GetRandomModifier(_levelModifierRng);
        }
        else
        {
            RoundModifierPool angelPool = defaultAngelPool;
            RoundModifierPool devilPool = defaultDevilPool;
            bool hasAngel = angelPool != null && angelPool.ValidCount > 0;
            bool hasDevil = devilPool != null && devilPool.ValidCount > 0;
            if (hasAngel || hasDevil)
            {
                if (_levelModifierRng == null)
                    _levelModifierRng = new System.Random(Environment.TickCount);
                type = (_levelModifierRng.NextDouble() < 0.5) ? RoundType.Angel : RoundType.Devil;
                if (type == RoundType.Angel && hasAngel)
                    modifier = angelPool.GetRandomModifier(_levelModifierRng);
                else if (type == RoundType.Devil && hasDevil)
                    modifier = devilPool.GetRandomModifier(_levelModifierRng);
                if (modifier == null && hasAngel)
                    modifier = angelPool.GetRandomModifier(_levelModifierRng);
                if (modifier == null && hasDevil)
                    modifier = devilPool.GetRandomModifier(_levelModifierRng);
            }
        }

        _activeModifier = modifier;
        _currentRoundData = new RoundData(roundIndex, type, modifier);
        _flipperUsesRemaining = (_activeModifier != null && _activeModifier.flipperUseLimit > 0)
            ? _activeModifier.flipperUseLimit
            : -1;

        // Push active modifier into ScoreManager so ball→AddScore() uses the right multipliers (peer rule).
        _effectiveGoalModifierForRound = 0f;
        _unluckyDayActiveModifiers = null;
        if (scoreManager != null && _activeModifier != null)
        {
            if (_activeModifier.applyTwoRandomDevilModifiers)
            {
                RoundModifierPool devilPool = (session != null && session.ActiveChallenge != null && session.ActiveChallenge.devilPool != null)
                    ? session.ActiveChallenge.devilPool
                    : defaultDevilPool;
                if (devilPool != null)
                {
                    var two = devilPool.GetTwoRandomModifiersExcluding(_levelModifierRng, _activeModifier);
                    _unluckyDayActiveModifiers = two;
                    float scoreMult = 1f;
                    float goalMod = 0f;
                    float coinMult = 1f;
                    bool disableMult = false;
                    int ballMod = 0;
                    float timeScaleMult = 1f;
                    foreach (var m in two)
                    {
                        if (m == null) continue;
                        scoreMult *= m.scoreMultiplier;
                        goalMod += m.goalModifier;
                        coinMult *= m.coinMultiplier;
                        disableMult = disableMult || m.disableMultiplier;
                        ballMod += m.ballModifier;
                        timeScaleMult *= m.timeScaleMultiplier;
                    }
                    scoreManager.pointsModifierMultiplier = Mathf.Max(0f, scoreMult);
                    scoreManager.multModifierMultiplier = disableMult ? 0f : 1f;
                    scoreManager.coinModifierMultiplier = Mathf.Max(0f, coinMult);
                    scoreManager.modifierTimeScaleMultiplier = Mathf.Max(0.1f, timeScaleMult);
                    _effectiveGoalModifierForRound = goalMod;
                    ApplyBallModifierFromActiveModifier(ballMod);
                }
                else
                {
                    scoreManager.pointsModifierMultiplier = Mathf.Max(0f, _activeModifier.scoreMultiplier);
                    scoreManager.multModifierMultiplier = _activeModifier.disableMultiplier ? 0f : 1f;
                    scoreManager.coinModifierMultiplier = Mathf.Max(0f, _activeModifier.coinMultiplier);
                    ApplyBallModifierFromActiveModifier();
                }
            }
            else
            {
                scoreManager.pointsModifierMultiplier = Mathf.Max(0f, _activeModifier.scoreMultiplier);
                scoreManager.multModifierMultiplier = _activeModifier.disableMultiplier ? 0f : 1f; // Cursed Multiplier: mult cannot increase (locked at 1×)
                scoreManager.coinModifierMultiplier = Mathf.Max(0f, _activeModifier.coinMultiplier);
                scoreManager.modifierTimeScaleMultiplier = Mathf.Max(0.1f, _activeModifier.timeScaleMultiplier);
                ApplyBallModifierFromActiveModifier();
            }
        }

        // Show modifier name for ~3 seconds so you can confirm it's active (Unlucky Day = two names in orange).
        if (_activeModifier != null)
        {
            ResolveFloatingTextSpawner(logIfMissing: false);
            if (_activeModifier.applyTwoRandomDevilModifiers && _unluckyDayActiveModifiers != null && _unluckyDayActiveModifiers.Count > 0)
            {
                var names = new List<string>();
                foreach (var m in _unluckyDayActiveModifiers)
                {
                    if (m != null && !string.IsNullOrEmpty(m.displayName))
                        names.Add(m.displayName);
                }
                string text = names.Count > 0 ? string.Join("\n", names) : _activeModifier.displayName;
                floatingTextSpawner?.SpawnModifierPopup(text, 3f, Color.orange);
            }
            else
            {
                floatingTextSpawner?.SpawnModifierPopup(_activeModifier.displayName, 3f);
            }
        }

        LevelChanged?.Invoke();
    }

    private RoundType RollModifierType(ChallengeModeDefinition challenge)
    {
        if (challenge == null)
        {
            return RoundType.Normal;
        }

        if (challenge.distributionMode == RoundDistributionMode.Guaranteed)
        {
            EnsureGuaranteedTypeBag(challenge);
            if (_guaranteedTypeBag.Count == 0)
            {
                return RoundType.Normal;
            }

            int i = Mathf.Clamp(_guaranteedTypeBagPos, 0, _guaranteedTypeBag.Count - 1);
            RoundType t = _guaranteedTypeBag[i];
            _guaranteedTypeBagPos = Mathf.Max(0, _guaranteedTypeBagPos + 1);
            return t;
        }

        if (_levelModifierRng == null)
        {
            _levelModifierRng = new System.Random(Environment.TickCount);
        }

        float angelChance = Mathf.Clamp01(challenge.angelChance);
        float devilChance = Mathf.Clamp01(challenge.devilChance);
        double roll = _levelModifierRng.NextDouble();

        if (roll < angelChance)
        {
            return RoundType.Angel;
        }

        if (roll < (angelChance + devilChance))
        {
            return RoundType.Devil;
        }

        return RoundType.Normal;
    }

    private RoundModifierDefinition RollModifierFromPool(ChallengeModeDefinition challenge, RoundType type)
    {
        if (challenge == null)
        {
            return null;
        }

        if (_levelModifierRng == null)
        {
            _levelModifierRng = new System.Random(Environment.TickCount);
        }

        switch (type)
        {
            case RoundType.Angel:
                return challenge.angelPool != null ? challenge.angelPool.GetRandomModifier(_levelModifierRng) : null;
            case RoundType.Devil:
                return challenge.devilPool != null ? challenge.devilPool.GetRandomModifier(_levelModifierRng) : null;
            default:
                return null;
        }
    }

    private void EnsureGuaranteedTypeBag(ChallengeModeDefinition challenge)
    {
        if (challenge == null)
        {
            return;
        }

        if (_levelModifierRng == null)
        {
            _levelModifierRng = new System.Random(Environment.TickCount);
        }

        if (_guaranteedTypeBagPos < _guaranteedTypeBag.Count)
        {
            return;
        }

        _guaranteedTypeBag.Clear();
        _guaranteedTypeBagPos = 0;

        int bagSize = challenge.totalRounds > 0 ? challenge.totalRounds : guaranteedBagSizeFallback;
        bagSize = Mathf.Max(1, bagSize);

        int angels = Mathf.Clamp(challenge.guaranteedAngels, 0, bagSize);
        int devils = Mathf.Clamp(challenge.guaranteedDevils, 0, bagSize - angels);

        for (int i = 0; i < bagSize; i++)
        {
            _guaranteedTypeBag.Add(RoundType.Normal);
        }

        for (int i = 0; i < angels; i++)
        {
            _guaranteedTypeBag[i] = RoundType.Angel;
        }

        for (int i = 0; i < devils; i++)
        {
            _guaranteedTypeBag[angels + i] = RoundType.Devil;
        }

        for (int i = _guaranteedTypeBag.Count - 1; i > 0; i--)
        {
            int j = _levelModifierRng.Next(i + 1);
            (_guaranteedTypeBag[i], _guaranteedTypeBag[j]) = (_guaranteedTypeBag[j], _guaranteedTypeBag[i]);
        }
    }

    private void ApplyBallModifierFromActiveModifier()
    {
        int delta = _activeModifier != null ? _activeModifier.ballModifier : 0;
        ApplyBallModifierFromActiveModifier(delta);
    }

    private void ApplyBallModifierFromActiveModifier(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        ResolveBallSpawner(logIfMissing: false);

        if (delta > 0)
        {
            TryAddFallbackBallsToLoadout(delta);
        }
        else
        {
            RemoveBallsFromLoadout(-delta);
        }

        ballsRemaining = BallLoadoutCount;
        if (scoreManager != null)
        {
            scoreManager.SetBallsRemaining(ballsRemaining);
        }
    }

    private void TryAddFallbackBallsToLoadout(int count)
    {
        if (count <= 0)
        {
            return;
        }

        GameObject fallbackPrefab = ballSpawner != null ? ballSpawner.DefaultBallPrefab : null;
        if (fallbackPrefab == null)
        {
            return;
        }

        BallDefinition def = BallDefinitionUtilities.TryGetDefinitionFromPrefab(fallbackPrefab);
        if (def == null)
        {
            def = BallDefinition.CreateRuntime(
                runtimeId: fallbackPrefab.name,
                runtimeDisplayName: fallbackPrefab.name,
                runtimeDescription: "",
                runtimeRarity: BallRarity.Common,
                runtimeIcon: BallDefinitionUtilities.TryGetPrefabSpriteIcon(fallbackPrefab),
                runtimePrefab: fallbackPrefab,
                runtimePrice: 0);
        }

        int remaining = count;
        for (int i = 0; i < _ballLoadout.Count && remaining > 0; i++)
        {
            if (_ballLoadout[i] == null || _ballLoadout[i].Prefab == null)
            {
                _ballLoadout[i] = def;
                remaining--;
            }
        }
    }

    private void RemoveBallsFromLoadout(int count)
    {
        if (count <= 0)
        {
            return;
        }

        EnsureLoadoutWithinCapacity();

        int remaining = count;
        for (int i = _ballLoadout.Count - 1; i >= 0 && remaining > 0; i--)
        {
            BallDefinition def = _ballLoadout[i];
            if (def != null && def.Prefab != null)
            {
                _ballLoadout[i] = null;
                remaining--;
            }
        }
    }

    private void OnDisable()
    {
        if (scoreManager != null)
        {
            scoreManager.ScoreChanged -= OnScoreChanged;
        }
    }

    private void OnScoreChanged()
    {
        if (!runActive || shopOpen || _drainProcessing)
        {
            return;
        }

        TryProcessLevelUps();
    }

    private void TryProcessLevelUps()
    {
        if (_levelUpProcessing)
        {
            return;
        }

        _levelUpProcessing = true;
        try
        {
            ResolveScoreManager(logIfMissing: false);
            if (scoreManager == null)
            {
                return;
            }

            int safety = 0;
            while (CurrentGoal > 0f && scoreManager.LiveLevelProgress >= CurrentGoal)
            {
                float prevGoal = CurrentGoal;

                int coinsAwarded = AddCoinsScaled(coinsPerLevelUp);

                if (showLevelUpCoinsPopup)
                {
                    ResolveFloatingTextSpawner(logIfMissing: false);
                    floatingTextSpawner?.SpawnLevelUpCoinsPopup(coinsAwarded);
                }

                scoreManager.ConsumeLevelProgress(prevGoal);

                roundIndex = Mathf.Max(0, roundIndex + 1);
                ApplyLevelModifier();

                scoreManager.SetRoundIndex(roundIndex);
                scoreManager.OnNewRound(roundIndex); 
                scoreManager.SetGoal(CurrentGoal);

                _shopBallSaveAvailable = true;

                safety++;
                if (safety > 100)
                {
                    Debug.LogWarning($"{nameof(GameRulesManager)}: Level-up loop safety break.", this);
                    break;
                }
            }
        }
        finally
        {
            _levelUpProcessing = false;
        }
    }

    public void OnBallDrained(GameObject ball)
    {
        OnBallDrained(ball, 1f, showHomeRunPopup: false);
    }

    public void OnBallDrained(GameObject ball, float bankMultiplier, bool showHomeRunPopup)
    {
        if (_drainProcessing)
        {
            DespawnBall(ball);
            return;
        }

        StartCoroutine(OnBallDrainedRoutine(ball, bankMultiplier, showHomeRunPopup));
    }

    private System.Collections.IEnumerator OnBallDrainedRoutine(GameObject ball, float bankMultiplier, bool showHomeRunPopup)
    {
        if (ActiveBalls.Count > 1)
        {
            DespawnBall(ball);
            yield break;
        }
        _drainProcessing = true;

        if (!runActive || shopOpen)
        {
            DespawnBall(ball);
            _drainProcessing = false;
            yield break;
        }

        ResolveScoreManager(logIfMissing: false);
        ResolveScoreTallyAnimator(logIfMissing: false);

        // Capture the banked score for this drain so we can persist totalPointsScored.
        // For the animated path, the coroutine itself commits the bank and resets points/mult.
        // For the instant path, BankCurrentBallIntoRoundTotal returns the banked amount.
        double bankedPoints = 0d;
        if (scoreManager != null)
        {
            float m = bankMultiplier;
            if (m <= 0f)
            {
                m = 1f;
            }

            bankedPoints = scoreManager.points * scoreManager.mult * m;
        }

        Vector3 drainedBallWorldPos = ball != null ? ball.transform.position : Vector3.zero;
        DespawnBall(ball);

        if (showHomeRunPopup)
        {
            ShowHomeRunPopup();
        }

        // Play animated tally if configured; otherwise instant-bank.
        if (scoreTallyAnimator != null && scoreManager != null)
        {
            yield return scoreTallyAnimator.PlayTally(scoreManager, bankMultiplier, drainedBallWorldPos);
            roundTotal = scoreManager.roundTotal;
        }
        else
        {
            bankedPoints = BankCurrentBallIntoRoundTotal(bankMultiplier);
        }

        ProfileService.AddBankedPoints(bankedPoints);

        // Ensure immediate level-ups are processed even if scoring was deferred until banking.
        TryProcessLevelUps();

        if (_shopBallSaveAvailable)
        {
            // A level-up has happened since the last shop; this drain triggers the shop and the ball is saved.
            _shopBallSaveAvailable = false;

            ballsRemaining = BallLoadoutCount;
            if (scoreManager != null)
            {
                scoreManager.SetBallsRemaining(ballsRemaining);
            }

            _activeBallSlotIndex = -1;
            OpenShop();
            _drainProcessing = false;
            yield break;
        }

        // No level-up since the last shop: consume the ball permanently and continue playing.
        ConsumeActiveBallFromLoadout();

        ballsRemaining = BallLoadoutCount;
        if (scoreManager != null)
        {
            scoreManager.SetBallsRemaining(ballsRemaining);
        }

        if (ballsRemaining > 0)
        {
            _activeBallSlotIndex = -1;
            SpawnBall();
            _drainProcessing = false;
            yield break;
        }

        ShowRoundFailed();
        _drainProcessing = false;
        yield break;
    }

    private void ConsumeActiveBallFromLoadout()
    {
        EnsureLoadoutWithinCapacity();

        int slotIndex = _activeBallSlotIndex;
        if (slotIndex >= 0 && slotIndex < _ballLoadout.Count)
        {
            TryRemoveBallFromLoadoutAt(slotIndex, out _);
            return;
        }

        for (int i = 0; i < _ballLoadout.Count; i++)
        {
            BallDefinition def = _ballLoadout[i];
            if (def != null && def.Prefab != null)
            {
                TryRemoveBallFromLoadoutAt(i, out _);
                return;
            }
        }
    }

    /// <summary>
    /// Called by the Shop UI when the player is done shopping.
    /// </summary>
    public void OnShopClosed()
    {
        if (!runActive)
        {
            return;
        }

        CloseShopAndAdvanceIndexOnly();
        StartRound();
    }

    /// <summary>
    /// Close the shop UI and advance the round index, but do not start the next round.
    /// Used by RunFlowController so it can load/unload boards before starting the next round.
    /// </summary>
    public void CloseShopAndAdvanceIndexOnly()
    {
        if (!runActive)
        {
            return;
        }

        SetShopOpen(false);
        shopOpen = false;
        ShopClosed?.Invoke();
    }

    /// <summary>
    /// Allows a shop purchase to increase balls for future rounds.
    /// </summary>
    public void AddMaxBalls(int delta)
    {
        // Slot count is fixed; interpret this as adding/removing balls within the fixed slots.
        ResolveBallSpawner(logIfMissing: false);

        if (delta > 0)
        {
            TryAddFallbackBallsToLoadout(delta);
        }
        else if (delta < 0)
        {
            RemoveBallsFromLoadout(-delta);
        }

        ballsRemaining = BallLoadoutCount;
        scoreManager?.SetBallsRemaining(ballsRemaining);
    }

    /// <summary>
    /// Adds a new ball definition into the loadout if there is an open slot (i.e. loadoutCount &lt; MaxBalls).
    /// Returns true if added.
    /// </summary>
    public bool AddBallToLoadout(BallDefinition def)
    {
        if (def == null || def.Prefab == null) return false;
        EnsureLoadoutWithinCapacity();

        for (int i = 0; i < _ballLoadout.Count; i++)
        {
            if (_ballLoadout[i] == null || _ballLoadout[i].Prefab == null)
            {
                _ballLoadout[i] = def;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Legacy helper: adds a ball prefab by mapping it to a BallDefinitionLink if present.
    /// </summary>
    public bool AddBallToLoadout(GameObject prefab)
    {
        if (prefab == null) return false;

        BallDefinition def = BallDefinitionUtilities.TryGetDefinitionFromPrefab(prefab);
        if (def == null)
        {
            def = BallDefinition.CreateRuntime(
                runtimeId: prefab.name,
                runtimeDisplayName: prefab.name,
                runtimeDescription: "",
                runtimeRarity: BallRarity.Common,
                runtimeIcon: BallDefinitionUtilities.TryGetPrefabSpriteIcon(prefab),
                runtimePrefab: prefab,
                runtimePrice: 0);
        }

        return AddBallToLoadout(def);
    }

    /// <summary>
    /// Replaces a ball in the loadout at <paramref name="slotIndex"/> with <paramref name="newDef"/>.
    /// Returns true if successful.
    /// </summary>
    public bool ReplaceBallInLoadout(int slotIndex, BallDefinition newDef)
    {
        if (newDef == null || newDef.Prefab == null) return false;
        EnsureLoadoutWithinCapacity();
        if (slotIndex < 0 || slotIndex >= _ballLoadout.Count) return false;
        _ballLoadout[slotIndex] = newDef;
        return true;
    }

    /// <summary>
    /// Removes a ball from the loadout at <paramref name="slotIndex"/>.
    /// Returns true if a ball was removed.
    /// </summary>
    public bool TryRemoveBallFromLoadoutAt(int slotIndex, out BallDefinition removed)
    {
        removed = null;
        EnsureLoadoutWithinCapacity();

        if (slotIndex < 0 || slotIndex >= _ballLoadout.Count)
        {
            return false;
        }

        removed = _ballLoadout[slotIndex];
        if (removed == null || removed.Prefab == null)
        {
            removed = null;
            return false;
        }

        // Clear the ball but keep the slot.
        _ballLoadout[slotIndex] = null;

        // Keep state consistent in case callers query ballsRemaining while the shop is open.
        ballsRemaining = Mathf.Max(0, Mathf.Min(ballsRemaining, BallLoadoutCount));
        if (scoreManager != null)
        {
            scoreManager.SetBallsRemaining(ballsRemaining);
        }

        return removed != null;
    }

    /// <summary>
    /// Legacy helper: replaces a prefab by mapping it to a BallDefinitionLink if present.
    /// </summary>
    public bool ReplaceBallInLoadout(int slotIndex, GameObject newPrefab)
    {
        if (newPrefab == null) return false;

        BallDefinition def = BallDefinitionUtilities.TryGetDefinitionFromPrefab(newPrefab);
        if (def == null)
        {
            def = BallDefinition.CreateRuntime(
                runtimeId: newPrefab.name,
                runtimeDisplayName: newPrefab.name,
                runtimeDescription: "",
                runtimeRarity: BallRarity.Common,
                runtimeIcon: BallDefinitionUtilities.TryGetPrefabSpriteIcon(newPrefab),
                runtimePrefab: newPrefab,
                runtimePrice: 0);
        }

        return ReplaceBallInLoadout(slotIndex, def);
    }

    public bool SwapBallLoadoutSlots(int a, int b)
    {
        EnsureLoadoutWithinCapacity();

        if (a < 0 || b < 0 || a >= _ballLoadout.Count || b >= _ballLoadout.Count)
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        (_ballLoadout[a], _ballLoadout[b]) = (_ballLoadout[b], _ballLoadout[a]);
        return true;
    }

    /// <summary>
    /// Allows a shop purchase to spend coins.
    /// </summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (coins < amount)
        {
            return false;
        }

        coins -= amount;
        if (scoreManager != null)
        {
            scoreManager.SetCoins(coins);
        }
        return true;
    }

    /// <summary>
    /// Adds coins to the player WITHOUT applying the active modifier's coin multiplier.
    /// Intended for shop transactions like selling.
    /// </summary>
    public void AddCoinsUnscaled(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        coins += amount;
        if (scoreManager != null)
        {
            scoreManager.SetCoins(coins);
        }
    }

    /// <summary>
    /// Adds coins to the player (e.g. from board components like CoinAdder on ball hit).
    /// </summary>
    public void AddCoins(int amount)
    {
        AddCoinsScaled(amount);
    }

    /// <summary>
    /// Adds coins and returns the APPLIED amount after the active modifier's coin multiplier.
    /// Intended for coin drops/popups so the text matches what the player actually received.
    /// </summary>
    public int AddCoinsScaled(int amount)
    {
        int applied = amount;
        if (amount > 0)
        {
            float coinMultiplier = GetModifierCoinMultiplier();
            if (!Mathf.Approximately(coinMultiplier, 1f))
            {
                applied = Mathf.FloorToInt(applied * coinMultiplier);
            }
        }

        coins += applied;
        if (scoreManager != null)
        {
            scoreManager.SetCoins(coins);
        }

        return applied;
    }

    public int AddCoinsScaledDeferredUi(int amount)
    {
        ResolveScoreManager(logIfMissing: false);

        int applied = amount;
        if (amount > 0)
        {
            float coinMultiplier = GetModifierCoinMultiplier();
            if (!Mathf.Approximately(coinMultiplier, 1f))
            {
                applied = Mathf.FloorToInt(applied * coinMultiplier);
            }
        }

        coins += applied;
        return applied;
    }

    public void ApplyDeferredCoinsUi(int applied)
    {
        if (scoreManager == null)
        {
            ResolveScoreManager(logIfMissing: false);
        }

        scoreManager?.ApplyDeferredCoinsUi(applied);
    }

    public void RetryRound()
    {
        if (!runActive)
        {
            return;
        }

        StartRun();
    }

    private float GetGoalForRound(int index)
    {
        if (index < 0)
        {
            index = 0;
        }

        float baseGoalForRound = GetBaseGoalForRound(index);

        // Apply goal modifier from active modifier (or combined when Unlucky Day applies two devils)
        if (_activeModifier != null)
        {
            float goalMod = _activeModifier.applyTwoRandomDevilModifiers ? _effectiveGoalModifierForRound : _activeModifier.goalModifier;
            if (!Mathf.Approximately(goalMod, 0f))
            {
                baseGoalForRound = Mathf.Max(0f, baseGoalForRound + goalMod);
            }
        }

        return baseGoalForRound;
    }

    private float GetBaseGoalForRound(int index)
    {
        switch (goalScalingMode)
        {
            case GoalScalingMode.Exponential:
                return GetExponentialGoalForRound(index);
            case GoalScalingMode.LegacyList:
            default:
                return GetLegacyListGoalForRound(index);
        }
    }

    private float GetExponentialGoalForRound(int index)
    {
        float goal = Mathf.Max(0f, baseGoal);
        float growth = Mathf.Max(1f, goalGrowthPerLevel);

        goal *= Mathf.Pow(growth, index);

        goal = RoundToStep(goal, goalRoundingStep);

        return Mathf.Max(0f, goal);
    }

    private float GetLegacyListGoalForRound(int index)
    {
        if (goalByRound == null || goalByRound.Count == 0)
        {
            return 0f;
        }

        if (index >= goalByRound.Count)
        {
            return goalByRound[goalByRound.Count - 1];
        }

        return goalByRound[index];
    }

    private static float RoundToStep(float value, float step)
    {
        if (step <= 0f)
        {
            return value;
        }

        if (Mathf.Approximately(step, 1f))
        {
            return Mathf.Round(value);
        }

        float scaled = value / step;
        return Mathf.Round(scaled) * step;
    }

    private float BankCurrentBallIntoRoundTotal()
    {
        return BankCurrentBallIntoRoundTotal(1f);
    }

    private float BankCurrentBallIntoRoundTotal(float bankMultiplier)
    {
        ResolveScoreManager(logIfMissing: false);
        if (scoreManager == null)
        {
            return 0f;
        }

        // Prefer ScoreManager API so its internal roundTotal + TMP labels stay in sync.
        float banked = scoreManager.BankCurrentBallScore(bankMultiplier);
        roundTotal = scoreManager.roundTotal;
        return banked;
    }

    private void ShowHomeRunPopup()
    {
        if (homeRunUIRoot != null)
        {
            homeRunUIRoot.SetActive(true);
        }
    }

    public void CloseHomeRunPopup()
    {
        if (homeRunUIRoot != null)
        {
            homeRunUIRoot.SetActive(false);
        }
    }

    private void AwardCoinsFromRoundTotal()
    {
        if (pointsPerCoin <= 0f)
        {
            return;
        }

        int award = Mathf.FloorToInt(roundTotal / pointsPerCoin);

        // Apply coin multiplier from active modifier
        float coinMultiplier = GetModifierCoinMultiplier();
        if (!Mathf.Approximately(coinMultiplier, 1f))
        {
            award = Mathf.FloorToInt(award * coinMultiplier);
        }

        if (maxCoinsFromRoundTotal > 0)
        {
            award = Mathf.Min(award, maxCoinsFromRoundTotal);
        }

        if (award > 0)
        {
            coins += award;
            if (scoreManager != null)
            {
                scoreManager.SetCoins(coins);
            }
        }
    }

    private void OpenShop()
    {
        shopOpen = true;
        ClearAllBalls();
        ShopOpened?.Invoke();

        // Prefer the animated transition controller if present.
        if (shopTransitionController != null)
        {
            shopTransitionController.OpenShop();
            return;
        }

        SetShopOpen(true);
    }

    private void ShowRoundFailed()
    {
        ClearAllBalls();
        SetRoundFailedOpen(true);
    }

    private void SetShopOpen(bool open)
    {
        if (shopCanvasRoot != null)
        {
            shopCanvasRoot.SetActive(open);
        }
    }

    private void SetRoundFailedOpen(bool open)
    {
        if (roundFailedUIRoot != null)
        {
            roundFailedUIRoot.SetActive(open);
        }
    }

    private GameObject SpawnBall()
    {
        ResolveBallSpawner(logIfMissing: false);
        if (ballSpawner == null)
            return null;

        GameObject ball = ballSpawner.ActivateNextBall();
        _activeBallSlotIndex = -1;
        if (ball != null)
        {
            var marker = ball.GetComponent<BallHandSlotMarker>();
            if (marker != null)
            {
                _activeBallSlotIndex = marker.SlotIndex;
            }
        }

        return ball;
    }

    private void DespawnBall(GameObject ball)
    {
        if (ball == null)
        {
            return;
        }

        if (ballSpawner != null)
            ballSpawner.DespawnBall(ball);
    }

    private void ClearAllBalls()
    {
        ResolveBallSpawner(logIfMissing: false);
        if (ballSpawner != null)
        {
            ballSpawner.ClearAll();
        }
    }

    private void EnsureLoadoutWithinCapacity()
    {
        int cap = Mathf.Max(1, startingMaxBalls);
        maxBalls = cap;

        // Normalize size to fixed "slot count" == cap.
        if (_ballLoadout.Count > cap)
        {
            _ballLoadout.RemoveRange(cap, _ballLoadout.Count - cap);
        }
        while (_ballLoadout.Count < cap)
        {
            _ballLoadout.Add(null);
        }

        // Sanitize invalid entries to empty slots.
        for (int i = 0; i < _ballLoadout.Count; i++)
        {
            BallDefinition def = _ballLoadout[i];
            if (def == null || def.Prefab == null)
            {
                _ballLoadout[i] = null;
            }
        }
    }

    private void InitializeLoadoutForNewRun()
    {
        ResolveBallSpawner(logIfMissing: false);

        _ballLoadout.Clear();

        int cap = Mathf.Max(1, maxBalls);
        for (int i = 0; i < cap; i++)
        {
            _ballLoadout.Add(null);
        }

        if (startingBallLoadoutDefinitions != null && startingBallLoadoutDefinitions.Count > 0)
        {
            for (int i = 0; i < startingBallLoadoutDefinitions.Count && i < cap; i++)
            {
                BallDefinition def = startingBallLoadoutDefinitions[i];
                if (def != null && def.Prefab != null)
                {
                    _ballLoadout[i] = def;
                }
            }
        }
        else if (startingBallLoadout != null && startingBallLoadout.Count > 0)
        {
            for (int i = 0; i < startingBallLoadout.Count && i < cap; i++)
            {
                GameObject prefab = startingBallLoadout[i];
                if (prefab == null)
                {
                    continue;
                }

                BallDefinition def = BallDefinitionUtilities.TryGetDefinitionFromPrefab(prefab);
                if (def == null)
                {
                    def = BallDefinition.CreateRuntime(
                        runtimeId: prefab.name,
                        runtimeDisplayName: prefab.name,
                        runtimeDescription: "",
                        runtimeRarity: BallRarity.Common,
                        runtimeIcon: BallDefinitionUtilities.TryGetPrefabSpriteIcon(prefab),
                        runtimePrefab: prefab,
                        runtimePrice: 0);
                }

                _ballLoadout[i] = def;
            }
        }

        // If the starting list doesn't fill the hand, fill the remainder with the spawner's default prefab
        // so you start with a full hand by default.
        GameObject fallback = ballSpawner != null ? ballSpawner.DefaultBallPrefab : null;
        if (fallback != null)
        {
            for (int i = 0; i < _ballLoadout.Count; i++)
            {
                if (_ballLoadout[i] != null && _ballLoadout[i].Prefab != null)
                {
                    continue;
                }

                BallDefinition def = BallDefinitionUtilities.TryGetDefinitionFromPrefab(fallback);
                if (def == null)
                {
                    def = BallDefinition.CreateRuntime(
                        runtimeId: fallback.name,
                        runtimeDisplayName: fallback.name,
                        runtimeDescription: "",
                        runtimeRarity: BallRarity.Common,
                        runtimeIcon: BallDefinitionUtilities.TryGetPrefabSpriteIcon(fallback),
                        runtimePrefab: fallback,
                        runtimePrice: 0);
                }

                _ballLoadout[i] = def;
            }
        }

        EnsureLoadoutWithinCapacity();
    }
}

