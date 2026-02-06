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
    [SerializeField] private RoundPreviewPanel roundPreviewPanel;

    [Header("Scene names")]
    [SerializeField] private string gameplayCoreSceneName = "GameplayCore";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Round Preview")]
    [Tooltip("If true, shows the round preview panel before starting the run.")]
    [SerializeField] private bool showRoundPreview = true;
    [Tooltip("Default number of rounds if not specified by the challenge.")]
    [SerializeField] private int defaultTotalRounds = 7;

    [Header("Testing: Force Round Modifiers")]
    [Tooltip("If set, round 0 will always use this modifier. Card color matches modifier type (Angel=green, Devil=red).")]
    [SerializeField] private RoundModifierDefinition forceFirstRoundModifier;
    [Tooltip("If set, round 1 (second round) will always use this modifier. Card color matches modifier type.")]
    [SerializeField] private RoundModifierDefinition forceSecondRoundModifier;
    [Tooltip("If true, round 0 will have no modifier (normal round). Overrides Force First Round Modifier.")]
    [SerializeField] private bool forceFirstRoundNormal;

    [Header("Testing: Force First Board")]
    [Tooltip("If set, this board is used for the run (overrides session selection). Use when playing directly from GameplayCore or to test a specific board. Clear when done testing.")]
    [SerializeField] private BoardDefinition forceFirstBoard;

    [Header("Runtime (debug)")]
    [SerializeField] private bool hasStartedRun;

    private bool _waitingForPreviewContinue;

    private void Awake()
    {
        if (rulesManager == null)
        {
#if UNITY_2022_2_OR_NEWER
            rulesManager = FindFirstObjectByType<GameRulesManager>();
#else
            rulesManager = FindObjectOfType<GameRulesManager>();
#endif
        }

        if (boardLoader == null)
        {
#if UNITY_2022_2_OR_NEWER
            boardLoader = FindFirstObjectByType<BoardLoader>();
#else
            boardLoader = FindObjectOfType<BoardLoader>();
#endif
        }

        if (shopTransitionController == null)
        {
#if UNITY_2022_2_OR_NEWER
            shopTransitionController = FindFirstObjectByType<ShopTransitionController>();
#else
            shopTransitionController = FindObjectOfType<ShopTransitionController>();
#endif
        }

        if (roundPreviewPanel == null)
        {
#if UNITY_2022_2_OR_NEWER
            roundPreviewPanel = FindFirstObjectByType<RoundPreviewPanel>(FindObjectsInactive.Include);
#else
            roundPreviewPanel = FindObjectOfType<RoundPreviewPanel>(includeInactive: true);
#endif
        }
    }

    private void Start()
    {
        // GameplayCore scene should start the run once it loads.
        // If this controller exists in other scenes (eg. due to prefab reuse), don't start a run there.
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

        BoardDefinition first = forceFirstBoard != null ? forceFirstBoard : session.GetCurrentBoard();
        if (first == null)
        {
            Debug.LogWarning($"{nameof(RunFlowController)}: No board selected; returning to menu.", this);
            SceneManager.LoadScene(mainMenuSceneName);
            yield break;
        }

        // When forcing a board for testing, ensure session has a run plan so rounds work
        if (forceFirstBoard != null && (session.Boards == null || session.Boards.Count == 0))
        {
            session.ConfigureQuickRun(new[] { forceFirstBoard }, session.Seed > 0 ? session.Seed : UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        }

        // Generate rounds if not already generated
        if (!session.HasGeneratedRounds)
        {
            session.SetForceFirstRoundNormal(forceFirstRoundNormal);
            if (!forceFirstRoundNormal && forceFirstRoundModifier != null)
                session.SetForceFirstRoundModifier(forceFirstRoundModifier);
            if (forceSecondRoundModifier != null)
                session.SetForceSecondRoundModifier(forceSecondRoundModifier);

            int totalRounds = defaultTotalRounds;
            if (session.ActiveChallenge != null)
            {
                totalRounds = session.ActiveChallenge.GetTotalRounds(defaultTotalRounds);
            }
            session.GenerateRounds(totalRounds);
        }

        // Show round preview panel if enabled and rounds were generated
        if (showRoundPreview && session.HasGeneratedRounds)
        {
            yield return StartCoroutine(ShowRoundPreviewAndWait(session));
        }

        if (boardLoader != null)
        {
            yield return StartCoroutine(boardLoader.LoadBoard(first));
        }

        if (rulesManager != null)
        {
            // IMPORTANT: in GameplayCore, set GameRulesManager.autoStartOnPlay = false in the Inspector.
            rulesManager.StartRun();
        }

        // Ensure input is unlocked at the beginning of gameplay (in case we returned from a shop or scene reload).
        if (shopTransitionController != null)
        {
            shopTransitionController.ResumeGameplayInput();
        }
    }

    private IEnumerator ShowRoundPreviewAndWait(GameSession session, int focusRoundIndex = 0)
    {
        if (roundPreviewPanel == null)
        {
            Debug.LogWarning($"{nameof(RunFlowController)}: RoundPreviewPanel not assigned.", this);
            yield break;
        }

        _waitingForPreviewContinue = true;

        roundPreviewPanel.Show(session.GeneratedRounds, focusRoundIndex, OnPreviewContinue);

        while (_waitingForPreviewContinue)
        {
            yield return null;
        }
    }

    private void OnPreviewContinue()
    {
        _waitingForPreviewContinue = false;
    }

    /// <summary>
    /// Shows the round preview panel as an overlay (e.g., from shop button).
    /// Does not block - just shows the panel with a close callback.
    /// </summary>
    public void ShowRoundPreviewOverlay()
    {
        var session = GameSession.Instance;
        if (session == null || !session.HasGeneratedRounds)
            return;

        if (roundPreviewPanel == null)
            return;

        int currentRound = rulesManager != null ? rulesManager.RoundIndex : 0;
        roundPreviewPanel.Show(session.GeneratedRounds, currentRound, null);
    }

    /// <summary>
    /// Called by ShopUIController when the player clicks Continue.
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
        // Preview the NEXT round (current + 1) while still in shop
        int nextRoundIndex = rulesManager.RoundIndex + 1;

        // Show round preview OVER the shop (before closing)
        if (showRoundPreview && session != null && session.HasGeneratedRounds)
        {
            yield return StartCoroutine(ShowRoundPreviewAndWait(session, nextRoundIndex));
        }

        // NOW close shop and advance round index
        rulesManager.CloseShopAndAdvanceIndexOnly();

        bool boardCleared = boardLoader != null && boardLoader.CurrentBoardRoot != null && boardLoader.CurrentBoardRoot.IsCleared(rulesManager);
        if (!boardCleared)
        {
            // Close shop transition (camera pans back to board)
            yield return StartCoroutine(CloseShopTransitionAndWait());

            // Show board UI after camera returns
            if (shopTransitionController != null)
            {
                shopTransitionController.ShowBoardUI();
            }

            rulesManager.StartRound();
            if (shopTransitionController != null)
            {
                shopTransitionController.ResumeGameplayInput();
            }
            yield break;
        }

        if (session == null)
        {
            yield return StartCoroutine(CloseShopTransitionAndWait());

            if (shopTransitionController != null)
            {
                shopTransitionController.ShowBoardUI();
            }

            rulesManager.StartRound();
            if (shopTransitionController != null)
            {
                shopTransitionController.ResumeGameplayInput();
            }
            yield break;
        }

        BoardDefinition next = session.GetNextBoard();
        if (next == null)
        {
            // Run complete. Return to main menu.
            session.ResetSession();
            SceneManager.LoadScene(mainMenuSceneName);
            yield break;
        }

        session.AdvanceToNextBoard();

        // Close shop transition before loading new board
        yield return StartCoroutine(CloseShopTransitionAndWait());

        if (boardLoader != null)
        {
            yield return StartCoroutine(boardLoader.LoadBoard(next));
        }

        // Show board UI after new board loads
        if (shopTransitionController != null)
        {
            shopTransitionController.ShowBoardUI();
        }

        rulesManager.StartRound();
        if (shopTransitionController != null)
        {
            shopTransitionController.ResumeGameplayInput();
        }
    }

    private bool _waitingForShopClose;

    private IEnumerator CloseShopTransitionAndWait()
    {
        if (shopTransitionController == null)
        {
            yield break;
        }

        _waitingForShopClose = true;
        shopTransitionController.CloseShopThen(() => _waitingForShopClose = false);

        while (_waitingForShopClose)
        {
            yield return null;
        }
    }
}

