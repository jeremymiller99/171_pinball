using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameRulesManager : MonoBehaviour
{
    [Header("Scoring")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ScoreTallyAnimator scoreTallyAnimator;
    [SerializeField] private List<float> goalByRound = new List<float> { 500f, 800f, 1200f, 1700f, 2300f, 3000f, 4000f };
    [SerializeField] private float pointsPerCoin = 100f;

    // Generated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-17.
    [Header("Coins (score conversion cap)")]
    [Tooltip("Hard cap on coins awarded from round score when clearing the goal.\n" +
             "0 or less means no cap.")]
    [Min(0)]
    [SerializeField] private int maxCoinsFromRoundTotal = 20;

    [Header("Balls / Rounds")]
    [SerializeField] private int startingMaxBalls = 5;
    [SerializeField] private bool autoStartOnPlay = true;

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

    // Which definitions will be used for the next round's hand (size == maxBalls).
    private readonly List<BallDefinition> _ballLoadout = new List<BallDefinition>();

    // Active round modifier (from GameSession's generated rounds)
    private RoundModifierDefinition _activeModifier;
    private RoundData _currentRoundData;

    /// <summary>
    /// Fired whenever a new round is started (after goal/round UI is reset).
    /// Useful for per-round systems like Frenzy.
    /// </summary>
    public event Action RoundStarted;

    /// <summary>
    /// Fired when the shop is opened for the current round.
    /// </summary>
    public event Action ShopOpened;

    /// <summary>
    /// Fired when the shop is closed.
    /// </summary>
    public event Action ShopClosed;

    public int RoundIndex => roundIndex;
    public int MaxBalls => maxBalls;
    public int BallsRemaining => ballsRemaining;
    public int Coins => coins;
    public float RoundTotal => roundTotal;
    public float CurrentGoal => GetGoalForRound(roundIndex);
    public int BallLoadoutCount => _ballLoadout.Count;
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
            if (def != null && def.Prefab != null)
            {
                prefabs.Add(def.Prefab);
            }
        }

        return prefabs;
    }

    private void Awake()
    {
        ResolveScoreManager(logIfMissing: false);
        ResolveScoreTallyAnimator(logIfMissing: false);

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
        roundIndex = 0;
        coins = 0;
        maxBalls = Mathf.Max(1, startingMaxBalls);
        InitializeLoadoutForNewRun();
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

        // Apply round modifier from GameSession
        ApplyRoundModifier();

        roundTotal = 0f;
        EnsureLoadoutWithinCapacity();
        ballsRemaining = _ballLoadout.Count;

        // Apply ball modifier from active modifier
        if (_activeModifier != null && _activeModifier.ballModifier != 0)
        {
            ballsRemaining = Mathf.Max(1, ballsRemaining + _activeModifier.ballModifier);
        }

        if (scoreManager != null)
        {
            // Keep the ScoreManager's UI and state aligned with the rules state for the new round.
            scoreManager.ResetForNewRound();
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
            SpawnBall();
        }
    }

    /// <summary>
    /// Applies the round modifier from GameSession for the current round.
    /// </summary>
    private void ApplyRoundModifier()
    {
        _activeModifier = null;
        _currentRoundData = null;

        var session = GameSession.Instance;
        if (session == null || !session.HasGeneratedRounds)
        {
            return;
        }

        _currentRoundData = session.GetRoundData(roundIndex);
        if (_currentRoundData != null)
        {
            _activeModifier = _currentRoundData.modifier;
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

        ballsRemaining = Mathf.Max(0, ballsRemaining - 1);
        if (scoreManager != null)
        {
            scoreManager.SetBallsRemaining(ballsRemaining);
        }

        if (roundTotal >= CurrentGoal)
        {
            AwardCoinsFromRoundTotal();
            OpenShop();
            _drainProcessing = false;
            yield break;
        }

        if (ballsRemaining > 0)
        {
            SpawnBall();
            _drainProcessing = false;
            yield break;
        }

        ShowRoundFailed();
        _drainProcessing = false;
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
        roundIndex = Mathf.Max(0, roundIndex + 1);
    }

    /// <summary>
    /// Allows a shop purchase to increase balls for future rounds.
    /// </summary>
    public void AddMaxBalls(int delta)
    {
        maxBalls = Mathf.Max(1, maxBalls + delta);
        ballsRemaining = Mathf.Max(0, Mathf.Min(ballsRemaining, _ballLoadout.Count));
        EnsureLoadoutWithinCapacity();
        if (scoreManager != null)
        {
            scoreManager.SetBallsRemaining(ballsRemaining);
        }
    }

    /// <summary>
    /// Adds a new ball definition into the loadout if there is an open slot (i.e. loadoutCount &lt; MaxBalls).
    /// Returns true if added.
    /// </summary>
    public bool AddBallToLoadout(BallDefinition def)
    {
        if (def == null || def.Prefab == null) return false;
        EnsureLoadoutWithinCapacity();
        if (_ballLoadout.Count >= maxBalls)
        {
            return false;
        }
        _ballLoadout.Add(def);
        return true;
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

    public int AddCoinsScaledDeferredUi(int amount, out int uiToken)
    {
        ResolveScoreManager(logIfMissing: false);
        uiToken = scoreManager != null ? scoreManager.CoinsUiToken : 0;

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

    public void ApplyDeferredCoinsUi(int applied, int token)
    {
        if (scoreManager == null)
        {
            ResolveScoreManager(logIfMissing: false);
        }

        scoreManager?.ApplyDeferredCoinsUi(applied, token);
    }

    public void RetryRound()
    {
        if (!runActive)
        {
            return;
        }

        StartRound();
    }

    private float GetGoalForRound(int index)
    {
        if (goalByRound == null || goalByRound.Count == 0)
        {
            return 0f;
        }

        if (index < 0)
        {
            index = 0;
        }

        float baseGoal;
        if (index >= goalByRound.Count)
        {
            baseGoal = goalByRound[goalByRound.Count - 1];
        }
        else
        {
            baseGoal = goalByRound[index];
        }

        // Apply goal modifier from active modifier
        if (_activeModifier != null && !Mathf.Approximately(_activeModifier.goalModifier, 0f))
        {
            baseGoal = Mathf.Max(0f, baseGoal + _activeModifier.goalModifier);
        }

        return baseGoal;
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

    private void ResetCurrentBallScoreVisuals()
    {
        if (scoreManager == null)
        {
            return;
        }

        // Legacy fallback: prefer BankCurrentBallScore/ResetForNewRound, but keep this safe no-op reset.
        scoreManager.AddPoints(-scoreManager.points);
        scoreManager.AddMult(1f - scoreManager.mult);
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

        return ballSpawner.ActivateNextBall();
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
        int cap = Mathf.Max(1, maxBalls);

        // Remove nulls/invalids (treat them as "no ball in inventory").
        for (int i = _ballLoadout.Count - 1; i >= 0; i--)
        {
            if (_ballLoadout[i] == null || _ballLoadout[i].Prefab == null)
            {
                _ballLoadout.RemoveAt(i);
            }
        }

        // Trim to capacity.
        while (_ballLoadout.Count > cap)
        {
            _ballLoadout.RemoveAt(_ballLoadout.Count - 1);
        }
    }

    private void InitializeLoadoutForNewRun()
    {
        ResolveBallSpawner(logIfMissing: false);

        _ballLoadout.Clear();

        int cap = Mathf.Max(1, maxBalls);

        if (startingBallLoadoutDefinitions != null && startingBallLoadoutDefinitions.Count > 0)
        {
            for (int i = 0; i < startingBallLoadoutDefinitions.Count && _ballLoadout.Count < cap; i++)
            {
                BallDefinition def = startingBallLoadoutDefinitions[i];
                if (def != null && def.Prefab != null)
                {
                    _ballLoadout.Add(def);
                }
            }
        }
        else if (startingBallLoadout != null && startingBallLoadout.Count > 0)
        {
            for (int i = 0; i < startingBallLoadout.Count && _ballLoadout.Count < cap; i++)
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

                _ballLoadout.Add(def);
            }
        }

        // If the starting list doesn't fill the hand, fill the remainder with the spawner's default prefab
        // so you start with a full hand by default.
        GameObject fallback = ballSpawner != null ? ballSpawner.DefaultBallPrefab : null;
        while (_ballLoadout.Count < cap && fallback != null)
        {
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

            _ballLoadout.Add(def);
        }

        EnsureLoadoutWithinCapacity();
    }
}

