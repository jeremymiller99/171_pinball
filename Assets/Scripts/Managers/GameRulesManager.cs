// Updated with Cursor (Composer) by assistant on 2026-03-31 (ResolveServices in StartRun/StartRound for additive board scenes).
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GameRulesManager : MonoBehaviour
{
    [Header("Scoring")]
    [SerializeField] private ScoreManager scoreManager;

    [Header("Popups (optional)")]
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;
    [SerializeField] private bool showLevelUpCoinsPopup = true;

    [Header("Goal Scaling")]
    [SerializeField] private GoalScaler goalScaler;

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
    [Header("Transitions (optional)")]
    [SerializeField] private ShopTransitionController shopTransitionController;

    [Header("Debug")]
    [SerializeField] private int roundIndex;
    [SerializeField] private int ballsRemaining;
    [SerializeField] private float roundTotal;

    [Header("Drain")]
    [SerializeField] private DrainHandler drainHandler;

    private bool _runActive;
    private bool _shopOpen;
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
    public int Coins => ServiceLocator.Get<CoinController>()?.Coins ?? 0;
    public float RoundTotal => roundTotal;
    public float CurrentGoal => goalScaler != null
        ? goalScaler.GetGoal(roundIndex, ActiveModifier, ModifierController)
        : 0f;
    public bool IsShopOpen => _shopOpen;
    public List<GameObject> ActiveBalls => ballSpawner != null ? ballSpawner.ActiveBalls : null;

    private RoundModifierController ModifierController => ServiceLocator.Get<RoundModifierController>();
    private BallLoadoutController LoadoutController => ServiceLocator.Get<BallLoadoutController>();

    private int BallLoadoutCount => LoadoutController?.BallLoadoutCount ?? 0;
    private RoundModifierDefinition ActiveModifier => ModifierController?.ActiveModifier;

    public bool RunActive => _runActive;

    private void Awake()
    {
        ServiceLocator.Register<GameRulesManager>(this);

        ResolveServices();
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
        if (drainHandler != null) drainHandler.DrainBankCompleted -= TryProcessLevelUps;
    }

    private void ResolveServices()
    {
        if (ballSpawner == null) ballSpawner = ServiceLocator.Get<BallSpawner>();
        if (scoreManager == null) scoreManager = ServiceLocator.Get<ScoreManager>();
        if (floatingTextSpawner == null) floatingTextSpawner = ServiceLocator.Get<FloatingTextSpawner>();
        if (boardLoader == null) boardLoader = ServiceLocator.Get<BoardLoader>();
        if (shopTransitionController == null) shopTransitionController = ServiceLocator.Get<ShopTransitionController>();
        if (goalScaler == null) goalScaler = ServiceLocator.Get<GoalScaler>();
        if (goalScaler == null) goalScaler = GetComponent<GoalScaler>();
        if (goalScaler == null) goalScaler = gameObject.AddComponent<GoalScaler>();
        if (drainHandler == null) drainHandler = ServiceLocator.Get<DrainHandler>();
    }

    public void StartRun()
    {
        ResolveServices();
        if (ballSpawner == null) return;

        _runActive = true;
        _shopOpen = false;
        drainHandler?.ResetState();
        _levelUpProcessing = false;
        _shopBallSaveAvailable = false;
        _runStartTime = Time.unscaledTime;
        roundIndex = 0;
        ServiceLocator.Get<CoinController>()?.SetRunStartingBalance(
            GameSession.Instance.ActiveShip?.startingCoins ?? 0);
        roundTotal = 0f;

        LoadoutController?.InitializeForNewRun();
        ballsRemaining = BallLoadoutCount;

        ModifierController?.InitLevelModifierRolling();
        ModifierController?.ResetAndPrimeRoundWindow(roundIndex);
        ApplyCurrentRoundFromWindow();

        if (scoreManager != null)
        {
            scoreManager.ResetForNewRun();
            scoreManager.SetGoal(CurrentGoal);

            scoreManager.ScoreChanged -= OnScoreChanged;
            scoreManager.ScoreChanged += OnScoreChanged;
        }

        if (drainHandler != null)
        {
            drainHandler.DrainBankCompleted -= TryProcessLevelUps;
            drainHandler.DrainBankCompleted += TryProcessLevelUps;
        }

        ServiceLocator.Get<ScoreUIController>()?.SetRoundIndex(roundIndex);
        ServiceLocator.Get<ScoreUIController>()?.SetBallsRemaining(ballsRemaining);

        StartRound();
    }

    public void StartRound()
    {
        ResolveServices();
        if (ballSpawner == null) return;

        _shopOpen = false;
        SetShopOpen(false);
        SetRoundFailedOpen(false);

        ServiceLocator.Get<AudioManager>()?.SetMusicMuffled(false);

        ballsRemaining = BallLoadoutCount;

        if (scoreManager != null)
        {
            scoreManager.SetGoal(CurrentGoal);
        }


        ServiceLocator.Get<ScoreUIController>()?.SetRoundIndex(roundIndex);
        ServiceLocator.Get<ScoreUIController>()?.SetBallsRemaining(ballsRemaining);
        ServiceLocator.Get<ScoreUIController>()?.SetCoins(Coins);

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
        ServiceLocator.Get<ScoreUIController>()?.SetBallsRemaining(ballsRemaining);
    }

    public void TriggerRoundFailed() => ShowRoundFailed();

    private void OnScoreChanged()
    {
        bool draining = drainHandler != null && drainHandler.IsDrainProcessing;
        if (!_runActive || _shopOpen || draining) return;
        TryProcessLevelUps();
    }

    private void TryProcessLevelUps()
    {
        if (_levelUpProcessing) return;
        _levelUpProcessing = true;

        try
        {
            if (scoreManager == null) return;

            int safety = 0;
            while (CurrentGoal > 0f && scoreManager.LiveLevelProgress >= CurrentGoal)
            {
                float prevGoal = CurrentGoal;
                var cc = ServiceLocator.Get<CoinController>();
                int coinsAwarded = cc?.AddCoinsScaledDeferredUi(coinsPerLevelUp) ?? 0;

                if (showLevelUpCoinsPopup && floatingTextSpawner != null)
                {
                    int awarded = coinsAwarded;
                    floatingTextSpawner.SpawnLevelUpCoinsPopup(coinsAwarded,
                        () => cc?.ApplyDeferredCoinsUi(awarded));
                }
                else if (coinsAwarded > 0)
                {
                    cc?.ApplyDeferredCoinsUi(coinsAwarded);
                }

                scoreManager.ConsumeLevelProgress(prevGoal);
                roundIndex = Mathf.Max(0, roundIndex + 1);
                ApplyCurrentRoundFromWindow();

                ServiceLocator.Get<ScoreUIController>()?.SetRoundIndex(roundIndex);
                scoreManager.SetGoal(CurrentGoal);

                _shopBallSaveAvailable = true;
                ServiceLocator.Get<AudioManager>()?.PlayLevelUp();

                safety++;
                if (safety > 100) break;
            }
        }
        finally
        {
            _levelUpProcessing = false;
        }
    }

    #region DrainHandler Callbacks

    public void SyncRoundTotal(float total)
    {
        roundTotal = total;
    }

    public bool ConsumeShopBallSave()
    {
        if (!_shopBallSaveAvailable) return false;
        _shopBallSaveAvailable = false;
        return true;
    }

    public void RefreshBallsRemaining()
    {
        ballsRemaining = BallLoadoutCount;
        ServiceLocator.Get<ScoreUIController>()?.SetBallsRemaining(ballsRemaining);
    }

    public bool CheckAndCompleteRun()
    {
        if (!ShouldCompleteRunNow()) return false;
        CompleteRunAndShowWinScreen();
        return true;
    }

    private bool ShouldCompleteRunNow()
    {
        var session = GameSession.Instance;
        if (session != null && session.ActiveChallenge != null) return false;

        if (boardLoader == null) return false;
        BoardRoot root = boardLoader.CurrentBoardRoot;
        if (root == null || !root.IsCleared(this) || session == null) return false;

        return session.GetNextBoard() == null;
    }

    private void CompleteRunAndShowWinScreen()
    {
        int levelReached = Mathf.Max(1, LevelIndex + 1);
        long points = (long)Math.Round(Math.Max(0f, TotalScore));

        RunCompletionHelper.RecordProgressAndShowWinScreen(
            levelReached,
            points,
            beforeShowWin: () =>
            {
                _runActive = false;
                _shopOpen = false;
                _shopBallSaveAvailable = false;
                _levelUpProcessing = false;
                drainHandler?.ResetState();

                SetShopOpen(false);
                SetRoundFailedOpen(false);
                if (ballSpawner != null) ballSpawner.ClearAll();

                if (scoreManager != null) scoreManager.SetScoringLocked(true);
            });
    }

    #endregion

    public void OnShopClosed()
    {
        if (!_runActive) return;
        CloseShopAndAdvanceIndexOnly();
        StartRound();
    }

    public void CloseShopAndAdvanceIndexOnly(bool hideUi = true)
    {
        if (!_runActive) return;

        scoreManager?.ResetGameSpeedOnShopReturn();

        if (hideUi) SetShopOpen(false);
        _shopOpen = false;
        ShopClosed?.Invoke();
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0) return true;
        var c = ServiceLocator.Get<CoinController>();
        return c != null && c.TrySpendCoins(amount);
    }

    public void AddCoinsUnscaled(int amount) => ServiceLocator.Get<CoinController>()?.AddCoinsUnscaled(amount);

    public void AddCoins(int amount) => ServiceLocator.Get<CoinController>()?.AddCoins(amount);

    public int AddCoinsScaled(int amount)
    {
        var c = ServiceLocator.Get<CoinController>();
        return c != null ? c.AddCoinsScaled(amount) : 0;
    }

    public int AddCoinsScaledDeferredUi(int amount)
    {
        var c = ServiceLocator.Get<CoinController>();
        return c != null ? c.AddCoinsScaledDeferredUi(amount) : 0;
    }

    public void ApplyDeferredCoinsUi(int applied) => ServiceLocator.Get<CoinController>()?.ApplyDeferredCoinsUi(applied);

    public void RetryRound()
    {
        if (_runActive) StartRun();
    }


    public void ShowHomeRunPopup()
    {
        if (homeRunUIRoot != null) homeRunUIRoot.SetActive(true);
    }

    public void CloseHomeRunPopup()
    {
        if (homeRunUIRoot != null) homeRunUIRoot.SetActive(false);
    }

    #region Shop Bridge

    public void OpenShop()
    {
        _shopOpen = true;
        if (ballSpawner != null) ballSpawner.ClearActiveBalls();
        ShopOpened?.Invoke();

        ServiceLocator.Get<AudioManager>()?.SetMusicMuffled(true);

        if (shopTransitionController != null)
            shopTransitionController.OpenShop();
        else
            SetShopOpen(true);
    }

    public void ShowRoundFailed()
    {
        if (ballSpawner != null) ballSpawner.ClearAll();

        var session = GameSession.Instance;
        var challenge = session?.ActiveChallenge;

        var panel = roundFailedUIRoot != null ? roundFailedUIRoot.GetComponent<RoundFailedPanelController>() : null;
        if (panel != null)
        {
            panel.Show(roundTotal, CurrentGoal, roundIndex, ballsRemaining, Coins, RunElapsedTime, challenge);
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
            if (open) panel.Show(roundTotal, CurrentGoal, roundIndex, ballsRemaining, Coins, RunElapsedTime, GameSession.Instance?.ActiveChallenge);
            else panel.Hide();
            return;
        }

        roundFailedUIRoot.SetActive(open);
    }

    private GameObject SpawnBall()
    {
        if (ballSpawner == null) return null;
        return ballSpawner.ActivateNextBall();
    }

    #endregion
}
