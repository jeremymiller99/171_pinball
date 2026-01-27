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
    }

    private void Start()
    {
        // GameplayCore scene should start the run once it loads.
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
            // IMPORTANT: in GameplayCore, set GameRulesManager.autoStartOnPlay = false in the Inspector.
            rulesManager.StartRun();
        }

        // Ensure input is unlocked at the beginning of gameplay (in case we returned from a shop or scene reload).
        if (shopTransitionController != null)
        {
            shopTransitionController.ResumeGameplayInput();
        }
    }

    /// <summary>
    /// Called by ShopUIController when the player clicks Continue.
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

        // Close shop, advance round index, but DO NOT start the next round yet.
        rulesManager.CloseShopAndAdvanceIndexOnly();

        bool boardCleared = boardLoader != null && boardLoader.CurrentBoardRoot != null && boardLoader.CurrentBoardRoot.IsCleared(rulesManager);
        if (!boardCleared)
        {
            rulesManager.StartRound();
            if (shopTransitionController != null)
            {
                shopTransitionController.ResumeGameplayInput();
            }
            yield break;
        }

        var session = GameSession.Instance;
        if (session == null)
        {
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

        if (boardLoader != null)
        {
            yield return StartCoroutine(boardLoader.LoadBoard(next));
        }

        rulesManager.StartRound();
        if (shopTransitionController != null)
        {
            shopTransitionController.ResumeGameplayInput();
        }
    }
}

