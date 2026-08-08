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

    [Tooltip("Optional: plays a one-shot camera intro pan in sync with the start-of-gameplay fade-in.")]
    [SerializeField] private CameraIntroPan cameraIntroPan;

    [Header("Intro")]
    [Tooltip("Safety net for the intro input hold: releases board input this many unscaled seconds " +
             "after the run starts even if the intro never reports finished. Keep it above the " +
             "longest authored intro. 0 or less falls back to the default.")]
    [SerializeField] private float maxIntroHoldSeconds = DefaultMaxIntroHoldSeconds;

    private const float DefaultMaxIntroHoldSeconds = 10f;

    [Header("Scene names")]
    [SerializeField] private string gameplayCoreSceneName = "GameplayCore";
    [SerializeField] private string mainMenuSceneName = "MainMenu 1";

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

        // Never leave the board locked behind a torn-down controller.
        GameplayInputGate.Unblock(this);
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

        // Shut board input up front: the board load and the intro both span frames, and the
        // plunger is live through all of them otherwise -- the ball can be launched out of the
        // lane while the camera is still panning and the ship is still flying in.
        GameplayInputGate.Block(this);

        var session = GameSession.Instance;
        if (session == null)
        {
            Debug.LogError($"{nameof(RunFlowController)}: No GameSession found.", this);
            GameplayInputGate.Unblock(this);
            yield break;
        }

        BoardDefinition first = session.GetCurrentBoard();
        if (first == null)
        {
            Debug.LogWarning($"{nameof(RunFlowController)}: No board selected; returning to menu.", this);
            GameplayInputGate.Unblock(this);
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

        // Kick off the camera intro pan in sync with the fade-in (snaps to the intro
        // point, then slides into the play pose as the screen reveals).
        if (cameraIntroPan != null)
        {
            cameraIntroPan.Play();
        }

        SceneFader.Instance.FadeIn();

        yield return StartCoroutine(HoldInputUntilIntroFinishes());
    }

    /// <summary>
    /// Keeps board input shut until the camera intro pan has landed and the player ship has flown
    /// its entry path, so the player cannot plunge the ball out of the lane mid-cinematic.
    /// Bounded by <see cref="maxIntroHoldSeconds"/>: a locked-out board is worse than an early one.
    /// </summary>
    private IEnumerator HoldInputUntilIntroFinishes()
    {
        // A scene saved before this field existed deserializes it as 0, which would make the hold
        // expire on its first frame; fall back rather than silently doing nothing.
        float timeout = maxIntroHoldSeconds > 0f ? maxIntroHoldSeconds : DefaultMaxIntroHoldSeconds;

        // Resolved after the board scene is loaded -- the ship spawn point lives in the board.
        // Its Start may not have run yet, which is fine: EntryFlightComplete reads false until it
        // has either flown or been told there is no ship, so "not bound yet" keeps us waiting.
        PlayerShipFlightController shipFlight = FindFirstObjectByType<PlayerShipFlightController>();

        float elapsed = 0f;
        while (elapsed < timeout)
        {
            bool panning = cameraIntroPan != null && cameraIntroPan.IsPlaying;
            bool flying = shipFlight != null && !shipFlight.EntryFlightComplete;

            if (!panning && !flying)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (elapsed >= timeout)
        {
            Debug.LogWarning(
                $"{nameof(RunFlowController)}: intro hold hit its {timeout}s timeout; " +
                "releasing board input anyway.", this);
        }

        GameplayInputGate.Unblock(this);
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

