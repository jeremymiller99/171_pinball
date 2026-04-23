// Updated with Cursor (Composer) by assistant on 2026-03-31 (run completion via RunCompletionHelper).
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the run progression flow:
/// - reads GameSession selection
/// - loads boards via BoardLoader
/// - starts rounds
/// - decides whether to advance to next board after shop
/// </summary>
public sealed class RunFlowController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameRulesManager rulesManager;
    [SerializeField] private BoardLoader boardLoader;
    [SerializeField] private ShopTransitionController shopTransitionController;

    [Header("Scene names")]
    [SerializeField] private string gameplayCoreSceneName = "GameplayCore";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Runtime (debug)")]
    [SerializeField] private bool hasStartedRun;

    private void Awake()
    {
        ServiceLocator.Register<RunFlowController>(this);

        if (rulesManager == null)
        {
            rulesManager = ServiceLocator.Get<GameRulesManager>();
        }

        if (boardLoader == null)
        {
            boardLoader = ServiceLocator.Get<BoardLoader>();
        }

        if (shopTransitionController == null)
        {
            shopTransitionController =
                ServiceLocator.Get<ShopTransitionController>();
        }
    }

    private void OnDisable()
    {
        ServiceLocator.Unregister<RunFlowController>();
    }

    private void Start()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrWhiteSpace(gameplayCoreSceneName) && activeSceneName != gameplayCoreSceneName)
        {
            return;
        }

        if (!hasStartedRun)
        {
            StartCoroutine(StartRunFromSession());
        }
    }

    private IEnumerator StartRunFromSession()
    {
        hasStartedRun = true;

        var session = GameSession.Instance;
        if (session == null)
        {
            Debug.LogError($"{nameof(RunFlowController)}: No GameSession found.", this);
            yield break;
        }

        BoardDefinition first = session.GetCurrentBoard();
        if (first == null)
        {
            Debug.LogWarning($"{nameof(RunFlowController)}: No board selected; returning to menu.", this);
            SceneManager.LoadScene(mainMenuSceneName);
            yield break;
        }

        if (boardLoader != null)
        {
            yield return StartCoroutine(boardLoader.LoadBoard(first));
        }

        if (rulesManager != null)
        {
            rulesManager.StartRun();
        }

        // Ensure input is unlocked at the beginning of gameplay (in case we returned from a shop or scene reload).
        if (shopTransitionController != null)
        {
            shopTransitionController.ResumeGameplayInput();
        }

        // Wait one frame so the first gameplay frame renders under the black overlay before we fade in.
        yield return null;
        SceneFader.Instance.FadeIn();
    }

    /// <summary>

    /// Called by UnifiedShopController when the player clicks Continue.
    /// Shows round preview over the shop first, then closes shop and starts round.
    /// </summary>
    public void ContinueAfterShop()
    {
        StartCoroutine(ContinueAfterShopRoutine());
    }

    private IEnumerator ContinueAfterShopRoutine()
    {
        if (rulesManager == null)
        {
            yield break;
        }

        var session = GameSession.Instance;

        // Advance round index but leave shop visible so the transition can animate it closed.
        rulesManager.CloseShopAndAdvanceIndexOnly(hideUi: false);

        bool boardCleared = boardLoader != null
            && boardLoader.CurrentBoardRoot != null
            && boardLoader.CurrentBoardRoot.IsCleared(rulesManager);

        bool isChallenge = session != null && session.ActiveChallenge != null;
        if (isChallenge)
            boardCleared = false;

        // Board not yet cleared, or no session: stay on the current board and start the next round.
        if (!boardCleared || session == null)
        {
            yield return StartCoroutine(CloseShopTransitionAndWait());
            yield return StartCoroutine(ResumeRoundAfterShop());
            yield break;
        }

        BoardDefinition next = session.GetNextBoard();

        if (next == null)
        {
            yield return StartCoroutine(CloseShopTransitionAndWait());

            if (shopTransitionController != null)
            {
                shopTransitionController.ShowBoardUI();
                shopTransitionController.ResumeGameplayInput();
            }

            int levelReached = rulesManager != null ? Mathf.Max(1, rulesManager.LevelIndex + 1) : 1;
            long points = rulesManager != null
                ? (long)Math.Round(Math.Max(0d, rulesManager.TotalScore))
                : 0L;

            RunCompletionHelper.RecordProgressAndShowWinScreen(
                levelReached,
                points,
                afterRecordBeforeUnlocks: () =>
                {
                    if (session.ActiveChallenge != null && rulesManager != null)
                    {
                        long runScore = (long)Math.Round(rulesManager.TotalScore);
                        ProfileService.RecordChallengeBestScore(
                            session.ActiveChallenge.displayName, runScore);
                    }
                });

            yield break;
        }

        session.AdvanceToNextBoard();

        // Close shop transition before loading new board.
        yield return StartCoroutine(CloseShopTransitionAndWait());

        if (boardLoader != null)
        {
            yield return boardLoader.LoadBoard(next);
        }

        yield return StartCoroutine(ResumeRoundAfterShop());
    }

    /// <summary>
    /// Shows board UI, starts the next round, and re-enables gameplay input.
    /// Does NOT close the shop transition — call CloseShopTransitionAndWait() first if needed.
    /// </summary>
    private IEnumerator ResumeRoundAfterShop()
    {
        if (shopTransitionController != null)
            shopTransitionController.ShowBoardUI();

        rulesManager.StartRound();

        if (shopTransitionController != null)
            shopTransitionController.ResumeGameplayInput();

        yield break;
    }

    private IEnumerator CloseShopTransitionAndWait()
    {
        if (shopTransitionController == null)
        {
            yield break;
        }

        bool waiting = true;
        shopTransitionController.CloseShopThen(() => waiting = false);

        while (waiting)
        {
            yield return null;
        }
    }
}

