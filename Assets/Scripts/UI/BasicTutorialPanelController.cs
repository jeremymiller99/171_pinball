// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BasicTutorialPanelController : MonoBehaviour
{
    [Header("Optional refs (auto-resolved if blank)")]
    [SerializeField] private GameRulesManager rulesManager;

    [Header("Overall Root (optional)")]
    [Tooltip("Root object to disable when closing the whole tutorial panel. If omitted, uses this GameObject.")]
    [SerializeField] private GameObject basicTutorialPanelRoot;

    [Header("UI Roots (assign in inspector)")]
    [SerializeField] private GameObject firstTimePromptRoot;
    [SerializeField] private GameObject tutorialRoot;

    [Header("Prompt Buttons (assign in inspector)")]
    [SerializeField] private Button firstTimeYesButton;
    [SerializeField] private Button firstTimeNoButton;

    [Header("Tutorial Close (optional)")]
    [SerializeField] private Button tutorialCloseButton;

    private void Awake()
    {
        AutoResolveRulesManager();
        HookButtons();

        if (basicTutorialPanelRoot == null)
        {
            basicTutorialPanelRoot = gameObject;
        }

        if (firstTimePromptRoot != null)
        {
            firstTimePromptRoot.SetActive(false);
        }

        if (tutorialRoot != null)
        {
            tutorialRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        AutoResolveRulesManager();

        if (rulesManager != null)
        {
            rulesManager.RoundStarted -= HandleRoundStarted;
            rulesManager.RoundStarted += HandleRoundStarted;
        }
    }

    private void OnDisable()
    {
        if (rulesManager != null)
        {
            rulesManager.RoundStarted -= HandleRoundStarted;
        }
    }

    private void AutoResolveRulesManager()
    {
        if (rulesManager != null)
        {
            return;
        }

#if UNITY_2022_2_OR_NEWER
        rulesManager = FindFirstObjectByType<GameRulesManager>();
#else
        rulesManager = FindObjectOfType<GameRulesManager>();
#endif
    }

    private void HookButtons()
    {
        if (firstTimeYesButton != null)
        {
            firstTimeYesButton.onClick.RemoveListener(OnFirstTimeYesPressed);
            firstTimeYesButton.onClick.AddListener(OnFirstTimeYesPressed);
        }

        if (firstTimeNoButton != null)
        {
            firstTimeNoButton.onClick.RemoveListener(OnFirstTimeNoPressed);
            firstTimeNoButton.onClick.AddListener(OnFirstTimeNoPressed);
        }

        if (tutorialCloseButton != null)
        {
            tutorialCloseButton.onClick.RemoveListener(OnTutorialClosePressed);
            tutorialCloseButton.onClick.AddListener(OnTutorialClosePressed);
        }
    }

    private void HandleRoundStarted()
    {
        if (rulesManager == null)
        {
            return;
        }

        if (rulesManager.RoundIndex != 0)
        {
            return;
        }

        if (ProfileService.HasAnsweredFirstTimePlayingPrompt())
        {
            return;
        }

        ShowFirstTimePrompt();
    }

    private void ShowFirstTimePrompt()
    {
        if (firstTimePromptRoot != null)
        {
            firstTimePromptRoot.SetActive(true);
        }
    }

    private void HideFirstTimePrompt()
    {
        if (firstTimePromptRoot != null)
        {
            firstTimePromptRoot.SetActive(false);
        }
    }

    private void ShowTutorial()
    {
        if (tutorialRoot != null)
        {
            tutorialRoot.SetActive(true);
        }
    }

    private void HideTutorial()
    {
        if (tutorialRoot != null)
        {
            tutorialRoot.SetActive(false);
        }
    }

    private void CloseBasicTutorialPanel()
    {
        if (basicTutorialPanelRoot != null)
        {
            basicTutorialPanelRoot.SetActive(false);
        }
    }

    private void OnFirstTimeYesPressed()
    {
        ProfileService.RecordFirstTimePlayingPromptAnswer(true);
        HideFirstTimePrompt();
        CloseBasicTutorialPanel();
    }

    private void OnFirstTimeNoPressed()
    {
        ProfileService.RecordFirstTimePlayingPromptAnswer(false);
        HideFirstTimePrompt();
        ShowTutorial();
    }

    private void OnTutorialClosePressed()
    {
        HideTutorial();
    }
}

