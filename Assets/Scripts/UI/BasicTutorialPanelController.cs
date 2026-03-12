// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class BasicTutorialPanelController : MonoBehaviour
{
    private const int tutorialPanelCount = 8;
    private const int ignoreInputFramesOnOpen = 2;

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

    private int currentPanelIndex = -1;
    private int ignoreInputFrames;
    private bool hasResolvedStepPanels;
    private bool isStepperActive;
    private GameObject[] stepPanels;

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

    private void Update()
    {
        if (!isStepperActive || tutorialRoot == null || !tutorialRoot.activeInHierarchy)
        {
            return;
        }

        if (ignoreInputFrames > 0)
        {
            ignoreInputFrames--;
            return;
        }

        if (!WasAdvanceInputPressedThisFrame())
        {
            return;
        }

        AdvancePanel();
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

        if (rulesManager.LevelIndex != 0)
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

        StartTutorialStepper();
    }

    private void HideTutorial()
    {
        if (tutorialRoot != null)
        {
            tutorialRoot.SetActive(false);
        }

        isStepperActive = false;
        currentPanelIndex = -1;
        ignoreInputFrames = 0;

        HideAllStepPanels();
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

    private void StartTutorialStepper()
    {
        if (tutorialRoot == null)
        {
            return;
        }

        ResolveStepPanelsIfNeeded();
        if (stepPanels == null)
        {
            return;
        }

        HideLegacyTutorialChildren();

        isStepperActive = true;
        ignoreInputFrames = ignoreInputFramesOnOpen;
        currentPanelIndex = 0;
        ShowPanel(currentPanelIndex);
    }

    private void ResolveStepPanelsIfNeeded()
    {
        if (hasResolvedStepPanels)
        {
            return;
        }

        hasResolvedStepPanels = true;
        if (tutorialRoot == null)
        {
            return;
        }

        stepPanels = new GameObject[tutorialPanelCount];
        for (int i = 0; i < tutorialPanelCount; i++)
        {
            string panelName = (i + 1).ToString();
            Transform t = tutorialRoot.transform.Find(panelName);
            if (t == null)
            {
                Debug.LogWarning(
                    $"{nameof(BasicTutorialPanelController)}: Missing tutorial panel '{panelName}' under " +
                    $"'{tutorialRoot.name}'.");
                stepPanels = null;
                return;
            }

            stepPanels[i] = t.gameObject;
        }
    }

    private void HideLegacyTutorialChildren()
    {
        if (tutorialRoot == null || stepPanels == null)
        {
            return;
        }

        Transform root = tutorialRoot.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (IsStepPanel(child.gameObject))
            {
                continue;
            }

            child.gameObject.SetActive(false);
        }
    }

    private bool IsStepPanel(GameObject go)
    {
        if (go == null || stepPanels == null)
        {
            return false;
        }

        for (int i = 0; i < stepPanels.Length; i++)
        {
            if (stepPanels[i] == go)
            {
                return true;
            }
        }

        return false;
    }

    private void HideAllStepPanels()
    {
        if (stepPanels == null)
        {
            return;
        }

        for (int i = 0; i < stepPanels.Length; i++)
        {
            if (stepPanels[i] != null)
            {
                stepPanels[i].SetActive(false);
            }
        }
    }

    private void ShowPanel(int index)
    {
        if (stepPanels == null)
        {
            return;
        }

        for (int i = 0; i < stepPanels.Length; i++)
        {
            GameObject panel = stepPanels[i];
            if (panel == null)
            {
                continue;
            }

            panel.SetActive(i == index);
        }
    }

    private void AdvancePanel()
    {
        if (stepPanels == null)
        {
            return;
        }

        AudioManager.Instance.PlayTutorialNext();
        
        if (currentPanelIndex < 0)
        {
            currentPanelIndex = 0;
            ShowPanel(currentPanelIndex);
            return;
        }

        if (currentPanelIndex >= stepPanels.Length - 1)
        {
            HideTutorial();
            return;
        }

        currentPanelIndex++;
        ShowPanel(currentPanelIndex);
    }

    private static bool WasAdvanceInputPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame))
        {
            return true;
        }

        if (Gamepad.current != null &&
            (Gamepad.current.aButton.wasPressedThisFrame ||
             Gamepad.current.bButton.wasPressedThisFrame ||
             Gamepad.current.xButton.wasPressedThisFrame ||
             Gamepad.current.yButton.wasPressedThisFrame ||
             Gamepad.current.startButton.wasPressedThisFrame ||
             Gamepad.current.selectButton.wasPressedThisFrame))
        {
            return true;
        }

        return false;
#else
        return Input.anyKeyDown;
#endif
    }
}

