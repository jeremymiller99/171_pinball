using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class GameRulesManager : MonoBehaviour
{
    [Header("Scoring")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ScoreTallyAnimator scoreTallyAnimator;
    [SerializeField] private float pointsPerCoin = 100f;

    [Header("Popups (optional)")]
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;
    [SerializeField] private bool showLevelUpCoinsPopup = true;

    public enum GoalScalingMode { LegacyList = 0, Exponential = 1 }

    [Header("Level Goal Scaling")]
    [SerializeField] private GoalScalingMode goalScalingMode = GoalScalingMode.Exponential;
    [Min(0f)] [SerializeField] private float baseGoal = 500f;
    [Min(1f)] [SerializeField] private float goalGrowthPerLevel = 1.35f;
    [Tooltip("0 means no rounding. Otherwise, rounds the computed exponential goal to nearest step.")]
    [Min(0f)] [SerializeField] private float goalRoundingStep = 100f;

    [Header("Legacy level goals (optional)")]
    [SerializeField] private List<float> goalByRound = new List<float> { 500f, 800f, 1200f, 1700f, 2300f, 3000f, 4000f };

    [Header("Coins (score conversion cap)")]
    [Min(0)] [SerializeField] private int maxCoinsFromRoundTotal = 20;

    [Header("Runs")]
    [SerializeField] private bool autoStartOnPlay = true;

    [Header("Levels")]
    [Min(0)] [SerializeField] private int coinsPerLevelUp = 10;

    [Header("Ball Spawning")]
    [SerializeField] private BallSpawner ballSpawner;

    [Header("Board loading (optional)")]
    [SerializeField] private BoardLoader boardLoader;

    [Header("UI (optional)")]
    [SerializeField] private GameObject shopCanvasRoot;
    [SerializeField] private GameObject roundFailedUIRoot;
    [SerializeField] private GameObject homeRunUIRoot;
    [SerializeField] private TMP_Text homeRunMessageText;

    [Header("Transitions (optional)")]
    [SerializeField] private ShopTransitionController shopTransitionController;

    [Header("Debug")]
    [SerializeField] private int roundIndex;
    [SerializeField] private int ballsRemaining;
    [SerializeField] private int coins;
    [SerializeField] private float roundTotal;

    private bool runActive;
    private bool shopOpen;
    private bool _drainProcessing;
    private bool _levelUpProcessing;
    private float _runStartTime;
    private bool _shopBallSaveAvailable;

    public event Action RoundStarted;
    public event Action LevelChanged;
    public event Action ShopOpened;
    public event Action ShopClosed;

    public int LevelIndex => roundIndex;
    public float TotalScore => roundTotal;
    public float RunElapsedTime => Time.unscaledTime - _runStartTime;

    public int RoundIndex => roundIndex;
    public int BallsRemaining => ballsRemaining;
    public int Coins => coins;
    public float RoundTotal => roundTotal;
    public float CurrentGoal => GetGoalForRound(roundIndex);
    public bool IsShopOpen => shopOpen;
    public List<GameObject> ActiveBalls => ballSpawner != null ? ballSpawner.ActiveBalls : null;

    // ----- FACADES FOR NEW CONTROLLERS -----
    private RoundModifierController ModifierController => ServiceLocator.Get<RoundModifierController>();
    private BallLoadoutController LoadoutController => ServiceLocator.Get<BallLoadoutController>();

    public int MaxBalls => LoadoutController?.MaxBalls ?? 5;
    public int BallLoadoutCount => LoadoutController?.BallLoadoutCount ?? 0;
    public RoundModifierDefinition ActiveModifier => ModifierController?.ActiveModifier;
    public RoundData CurrentRoundData => ModifierController?.CurrentRoundData;
    public float GetModifierScoreMultiplier() => ModifierController?.GetModifierScoreMultiplier() ?? 1f;
    public float GetModifierCoinMultiplier() => ModifierController?.GetModifierCoinMultiplier() ?? 1f;
    public bool IsMultiplierDisabled() => ModifierController?.IsMultiplierDisabled() ?? false;
    public int RemainingFlipperUses => ModifierController?.RemainingFlipperUses ?? -1;
    public bool HasFlipperLimit => ModifierController?.HasFlipperLimit ?? false;
    
    public bool TryConsumeFlipperUse() => ModifierController?.TryConsumeFlipperUse() ?? true;

    public List<BallDefinition> GetBallLoadoutSnapshot() => LoadoutController?.GetBallLoadoutSnapshot() ?? new List<BallDefinition>();
    public void AddMaxBalls(int delta) => LoadoutController?.AddMaxBalls(delta);
    public bool AddBallToLoadout(BallDefinition def) => LoadoutController?.AddBallToLoadout(def) ?? false;
    public bool ReplaceBallInLoadout(int slotIndex, BallDefinition newDef) => LoadoutController?.ReplaceBallInLoadout(slotIndex, newDef) ?? false;
    public bool SwapBallLoadoutSlots(int a, int b) => LoadoutController?.SwapBallLoadoutSlots(a, b) ?? false;
    public bool TryRemoveBallFromLoadoutAt(int slotIndex, out BallDefinition removed)
    {
        if (LoadoutController != null) return LoadoutController.TryRemoveBallFromLoadoutAt(slotIndex, out removed);
        removed = null;
        return false;
    }

    public bool TryGetRoundType(int absoluteRoundIndex, out RoundType type)
    {
        if (ModifierController != null) return ModifierController.TryGetRoundType(absoluteRoundIndex, runActive, out type);
        type = RoundType.Normal;
        return false;
    }

    // ----------------------------------------

    private void Awake()
    {
        ServiceLocator.Register<GameRulesManager>(this);

        ResolveServices();

        if (goalByRound == null || goalByRound.Count == 0)
        {
            goalByRound = new List<float> { 500f, 800f, 1200f, 1700f, 2300f, 3000f, 4000f };
        }
    }

    private void Start()
    {
        ResolveServices();
        if (autoStartOnPlay) StartRun();
    }

    private void OnDisable()
    {
        ServiceLocator.Unregister<GameRulesManager>();
        if (scoreManager != null) scoreManager.ScoreChanged -= OnScoreChanged;
    }

    private void ResolveServices()
    {
        if (ballSpawner == null) ballSpawner = ServiceLocator.Get<BallSpawner>();
        if (scoreManager == null) scoreManager = ServiceLocator.Get<ScoreManager>();
        if (scoreTallyAnimator == null) scoreTallyAnimator = ServiceLocator.Get<ScoreTallyAnimator>();
        if (floatingTextSpawner == null) floatingTextSpawner = ServiceLocator.Get<FloatingTextSpawner>();
        if (boardLoader == null) boardLoader = ServiceLocator.Get<BoardLoader>();
        if (shopTransitionController == null) shopTransitionController = ServiceLocator.Get<ShopTransitionController>();
    }

    public void StartRun()
    {
        ResolveServices();
        if (ballSpawner == null) return;

        runActive = true;
        shopOpen = false;
        _drainProcessing = false;
        _levelUpProcessing = false;
        _shopBallSaveAvailable = false;
        _runStartTime = Time.unscaledTime;
        roundIndex = 0;
        coins = 0;
        roundTotal = 0f;

        LoadoutController?.InitializeForNewRun();
        ballsRemaining = BallLoadoutCount;

        ModifierController?.InitLevelModifierRolling();
        ModifierController?.ResetAndPrimeRoundWindow(roundIndex);
        ApplyCurrentRoundFromWindow();

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
        ResolveServices();
        if (ballSpawner == null) return;

        shopOpen = false;
        SetShopOpen(false);
        SetRoundFailedOpen(false);

        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicMuffled(false);

        ballsRemaining = BallLoadoutCount;

        if (scoreManager != null)
        {
            scoreManager.SetRoundIndex(roundIndex);
            scoreManager.SetGoal(CurrentGoal);
            scoreManager.SetBallsRemaining(ballsRemaining);
            scoreManager.SetCoins(coins);
        }

        RoundStarted?.Invoke();

        ballSpawner.ClearAll();
        
        List<GameObject> prefabs = LoadoutController?.GetBallLoadoutPrefabSnapshot() ?? new List<GameObject>();
        ballSpawner.BuildHandFromPrefabs(prefabs);

        if (ballsRemaining > 0)
        {
            GameObject firstBall = SpawnBall();
            if (firstBall != null) return;
        }

        ShowRoundFailed();
    }

    private void ApplyCurrentRoundFromWindow()
    {
        ModifierController?.ApplyCurrentRoundFromWindow(roundIndex, ApplyBallModifier);

        // Subbed in for legacy modifier popups
        if (ActiveModifier != null)
        {
            bool hasCard = ServiceLocator.Get<ModifierCardPopupController>() != null || FindFirstObjectByType<ModifierCardPopupController>(FindObjectsInactive.Include) != null;
            if (!hasCard && floatingTextSpawner != null)
            {
                if (ActiveModifier.applyTwoRandomDevilModifiers && ModifierController.UnluckyDayActiveModifiers?.Count > 0)
                {
                    List<string> names = new List<string>();
                    foreach (var m in ModifierController.UnluckyDayActiveModifiers)
                    {
                        if (m != null && !string.IsNullOrEmpty(m.displayName)) names.Add(m.displayName);
                    }
                    string t = names.Count > 0 ? string.Join("\n", names) : ActiveModifier.displayName;
                    floatingTextSpawner.SpawnModifierPopup(t, 3f, Color.orange);
                }
                else
                {
                    floatingTextSpawner.SpawnModifierPopup(ActiveModifier.displayName, 3f);
                }
            }
        }

        LevelChanged?.Invoke();
    }

    private void ApplyBallModifier(int delta)
    {
        if (delta != 0) LoadoutController?.AddMaxBalls(delta);

        ballsRemaining = BallLoadoutCount;
        if (scoreManager != null) scoreManager.SetBallsRemaining(ballsRemaining);
    }

    public void TriggerRoundFailed() => ShowRoundFailed();

    private void OnScoreChanged()
    {
        if (!runActive || shopOpen || _drainProcessing) return;
        TryProcessLevelUps();
    }

    private void TryProcessLevelUps()
    {
        if (_levelUpProcessing) return;
        _levelUpProcessing = true;

        try
        {
            ResolveServices();
            if (scoreManager == null) return;

            int safety = 0;
            while (CurrentGoal > 0f && scoreManager.LiveLevelProgress >= CurrentGoal)
            {
                float prevGoal = CurrentGoal;
                int coinsAwarded = AddCoinsScaled(coinsPerLevelUp);

                if (showLevelUpCoinsPopup && floatingTextSpawner != null)
                {
                    floatingTextSpawner.SpawnLevelUpCoinsPopup(coinsAwarded);
                }

                scoreManager.ConsumeLevelProgress(prevGoal);
                roundIndex = Mathf.Max(0, roundIndex + 1);
                ApplyCurrentRoundFromWindow();

                scoreManager.SetRoundIndex(roundIndex);
                scoreManager.SetGoal(CurrentGoal);

                _shopBallSaveAvailable = true;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayLevelUp();

                safety++;
                if (safety > 100) break;
            }
        }
        finally
        {
            _levelUpProcessing = false;
        }
    }

    public void OnBallDrained(GameObject ball) => OnBallDrained(ball, 1f, false);

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
        if (ActiveBalls != null && ActiveBalls.Count > 1)
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

        ResolveServices();

        int slotHint = -1;
        if (ball != null)
        {
            var marker = ball.GetComponent<BallHandSlotMarker>();
            if (marker != null) slotHint = marker.SlotIndex;
        }

        double bankedPoints = 0d;
        if (scoreManager != null)
        {
            float m = bankMultiplier <= 0f ? 1f : bankMultiplier;
            bankedPoints = scoreManager.points * scoreManager.mult * m;
        }

        Vector3 drainedBallWorldPos = ball != null ? ball.transform.position : Vector3.zero;
        DespawnBall(ball);

        if (showHomeRunPopup) ShowHomeRunPopup();

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

        TryProcessLevelUps();

        if (_shopBallSaveAvailable)
        {
            _shopBallSaveAvailable = false;
            ballsRemaining = BallLoadoutCount;
            if (scoreManager != null) scoreManager.SetBallsRemaining(ballsRemaining);

            if (ShouldCompleteRunNow())
            {
                CompleteRunAndShowWinScreen();
                _drainProcessing = false;
                yield break;
            }

            OpenShop();
            _drainProcessing = false;
            yield break;
        }

        // Consume ball permanently since no level-up occurred.
        LoadoutController?.ConsumeActiveBallFromLoadout(slotHint);
        ballsRemaining = BallLoadoutCount;
        if (scoreManager != null) scoreManager.SetBallsRemaining(ballsRemaining);

        if (ballsRemaining > 0)
        {
            GameObject nextBall = SpawnBall();
            if (nextBall != null)
            {
                _drainProcessing = false;
                yield break;
            }
        }

        ShowRoundFailed();
        _drainProcessing = false;
    }

    private bool ShouldCompleteRunNow()
    {
        var session = GameSession.Instance;
        if (session != null && session.ActiveChallenge != null) return false;

        ResolveServices();
        BoardRoot root = boardLoader != null ? boardLoader.CurrentBoardRoot : null;
        if (root == null || !root.IsCleared(this) || session == null) return false;

        return session.GetNextBoard() == null;
    }

    private void CompleteRunAndShowWinScreen()
    {
        int levelReached = Mathf.Max(1, LevelIndex + 1);
        long points = (long)Math.Round(Math.Max(0f, TotalScore));

        ProfileService.RecordRunCompleted();
        if (ProgressionService.Instance != null) ProgressionService.Instance.CheckAndGrantUnlocks();

        runActive = false;
        shopOpen = false;
        _shopBallSaveAvailable = false;
        _drainProcessing = false;
        _levelUpProcessing = false;

        SetShopOpen(false);
        SetRoundFailedOpen(false);
        if (ballSpawner != null) ballSpawner.ClearAll();

        if (scoreManager != null) scoreManager.SetScoringLocked(true);

        WinScreenController.Show(levelReached, points);
    }

    public void OnShopClosed()
    {
        if (!runActive) return;
        CloseShopAndAdvanceIndexOnly();
        StartRound();
    }

    public void CloseShopAndAdvanceIndexOnly(bool hideUi = true)
    {
        if (!runActive) return;

        scoreManager?.ResetGameSpeedOnShopReturn();

        if (hideUi) SetShopOpen(false);
        shopOpen = false;
        ShopClosed?.Invoke();
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0) return true;
        if (coins < amount)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayFailedPurchase();
            return false;
        }

        coins -= amount;
        if (scoreManager != null) scoreManager.SetCoins(coins);
        return true;
    }

    public void AddCoinsUnscaled(int amount)
    {
        if (amount <= 0) return;
        coins += amount;
        if (scoreManager != null)
        {
            scoreManager.SetCoins(coins);
            scoreManager.PlayStaggeredCoinSounds(amount);
        }
    }

    public void AddCoins(int amount) => AddCoinsScaled(amount);

    public int AddCoinsScaled(int amount)
    {
        int applied = amount;
        if (amount > 0)
        {
            float coinMultiplier = GetModifierCoinMultiplier();
            if (!Mathf.Approximately(coinMultiplier, 1f))
                applied = Mathf.FloorToInt(applied * coinMultiplier);
        }

        coins += applied;
        if (scoreManager != null)
        {
            scoreManager.SetCoins(coins);
            scoreManager.PlayStaggeredCoinSounds(applied);
        }
        return applied;
    }

    public int AddCoinsScaledDeferredUi(int amount)
    {
        ResolveServices();
        int applied = amount;
        if (amount > 0)
        {
            float coinMultiplier = GetModifierCoinMultiplier();
            if (!Mathf.Approximately(coinMultiplier, 1f))
                applied = Mathf.FloorToInt(applied * coinMultiplier);
        }
        coins += applied;
        return applied;
    }

    public void ApplyDeferredCoinsUi(int applied)
    {
        ResolveServices();
        scoreManager?.ApplyDeferredCoinsUi(applied);
    }

    public void RetryRound()
    {
        if (runActive) StartRun();
    }

    private float GetGoalForRound(int index)
    {
        if (index < 0) index = 0;
        
        float baseGoalForRound = goalScalingMode == GoalScalingMode.Exponential 
            ? GetExponentialGoalForRound(index) 
            : GetLegacyListGoalForRound(index);

        if (ActiveModifier != null)
        {
            float goalMod = ActiveModifier.applyTwoRandomDevilModifiers 
                ? (ModifierController?.EffectiveGoalModifierForRound ?? 0f) 
                : ActiveModifier.goalModifier;
                
            if (!Mathf.Approximately(goalMod, 0f))
                baseGoalForRound = Mathf.Max(0f, baseGoalForRound + goalMod);
        }

        return baseGoalForRound;
    }

    private float GetExponentialGoalForRound(int index)
    {
        float goal = Mathf.Max(0f, baseGoal) * Mathf.Pow(Mathf.Max(1f, goalGrowthPerLevel), index);
        if (goalRoundingStep > 0f && !Mathf.Approximately(goalRoundingStep, 1f))
            goal = Mathf.Round(goal / goalRoundingStep) * goalRoundingStep;
        else if (Mathf.Approximately(goalRoundingStep, 1f))
            goal = Mathf.Round(goal);
            
        return Mathf.Max(0f, goal);
    }

    private float GetLegacyListGoalForRound(int index)
    {
        if (goalByRound == null || goalByRound.Count == 0) return 0f;
        if (index >= goalByRound.Count) return goalByRound[goalByRound.Count - 1];
        return goalByRound[index];
    }

    private float BankCurrentBallIntoRoundTotal(float bankMultiplier = 1f)
    {
        ResolveServices();
        if (scoreManager == null) return 0f;

        float banked = scoreManager.BankCurrentBallScore(bankMultiplier);
        roundTotal = scoreManager.roundTotal;
        return banked;
    }

    private void ShowHomeRunPopup()
    {
        if (homeRunUIRoot != null) homeRunUIRoot.SetActive(true);
    }

    public void CloseHomeRunPopup()
    {
        if (homeRunUIRoot != null) homeRunUIRoot.SetActive(false);
    }

    private void OpenShop()
    {
        shopOpen = true;
        if (ballSpawner != null) ballSpawner.ClearActiveBalls();
        ShopOpened?.Invoke();

        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicMuffled(true);

        if (shopTransitionController != null)
            shopTransitionController.OpenShop();
        else
            SetShopOpen(true);
    }

    private void ShowRoundFailed()
    {
        if (ballSpawner != null) ballSpawner.ClearAll();

        var session = GameSession.Instance;
        var challenge = session?.ActiveChallenge;

        var panel = roundFailedUIRoot != null ? roundFailedUIRoot.GetComponent<RoundFailedPanelController>() : null;
        if (panel != null)
        {
            panel.Show(roundTotal, CurrentGoal, roundIndex, ballsRemaining, coins, RunElapsedTime, challenge);
        }
        else
        {
            SetRoundFailedOpen(true);
        }

        if (challenge != null)
        {
            long score = (long)Math.Round(Math.Max(0f, roundTotal));
            ProfileService.RecordChallengeBestScore(challenge.displayName, score);
        }
    }

    private void SetShopOpen(bool open)
    {
        if (shopCanvasRoot != null) shopCanvasRoot.SetActive(open);
    }

    private void SetRoundFailedOpen(bool open)
    {
        if (roundFailedUIRoot == null) return;
        var panel = roundFailedUIRoot.GetComponent<RoundFailedPanelController>();
        if (panel != null)
        {
            if (open) panel.Show(roundTotal, CurrentGoal, roundIndex, ballsRemaining, coins, RunElapsedTime, GameSession.Instance?.ActiveChallenge);
            else panel.Hide();
            return;
        }

        roundFailedUIRoot.SetActive(open);
    }

    private GameObject SpawnBall()
    {
        ResolveServices();
        return ballSpawner?.ActivateNextBall();
    }

    private void DespawnBall(GameObject ball)
    {
        if (ball != null && ballSpawner != null) ballSpawner.DespawnBall(ball);
    }
}
