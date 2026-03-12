// Generated with Cursor (claude-4.6-opus-high-thinking) by jjmil on 2026-02-26.
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class ShopTutorialPanelController : MonoBehaviour
{
    private const int shopTutorialPanelCount = 4;
    private const int ignoreInputFramesOnOpen = 2;

    [Header("Optional refs (auto-resolved if blank)")]
    [SerializeField] private ShopTransitionController shopTransitionController;

    [Header("Shop Tutorial Root (optional)")]
    [Tooltip("Root object to show/hide for the shop tutorial. If omitted, uses this GameObject.")]
    [SerializeField] private GameObject shopTutorialRoot;

    private int currentPanelIndex = -1;
    private int ignoreInputFrames;
    private bool hasResolvedStepPanels;
    private bool isStepperActive;
    private GameObject[] stepPanels;

    private void Awake()
    {
        AutoResolveShopTransitionController();

        if (shopTutorialRoot == null)
        {
            shopTutorialRoot = gameObject;
        }

        shopTutorialRoot.SetActive(false);
    }

    private void Update()
    {
        if (!isStepperActive)
        {
            return;
        }

        if (shopTutorialRoot == null || !shopTutorialRoot.activeInHierarchy)
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
        AutoResolveShopTransitionController();

        if (shopTransitionController != null)
        {
            shopTransitionController.OpenTransitionFinished -= HandleOpenTransitionFinished;
            shopTransitionController.OpenTransitionFinished += HandleOpenTransitionFinished;
        }
    }

    private void OnDisable()
    {
        if (shopTransitionController != null)
        {
            shopTransitionController.OpenTransitionFinished -= HandleOpenTransitionFinished;
        }
    }

    private void AutoResolveShopTransitionController()
    {
        if (shopTransitionController != null)
        {
            return;
        }

#if UNITY_2022_2_OR_NEWER
        shopTransitionController = FindFirstObjectByType<ShopTransitionController>();
#else
        shopTransitionController = FindObjectOfType<ShopTransitionController>();
#endif
    }

    private void HandleOpenTransitionFinished()
    {
        if (ProfileService.HasSeenShopTutorial())
        {
            return;
        }
        return;

        ShowTutorial();
    }

    private void ShowTutorial()
    {
        if (shopTutorialRoot != null)
        {
            shopTutorialRoot.SetActive(true);
        }

        StartTutorialStepper();
    }

    private void HideTutorial()
    {
        ProfileService.RecordShopTutorialSeen();

        isStepperActive = false;
        currentPanelIndex = -1;
        ignoreInputFrames = 0;

        HideAllStepPanels();

        if (shopTutorialRoot != null)
        {
            shopTutorialRoot.SetActive(false);
        }
    }

    private void StartTutorialStepper()
    {
        if (shopTutorialRoot == null)
        {
            return;
        }

        ResolveStepPanelsIfNeeded();

        if (stepPanels == null)
        {
            return;
        }

        HideNonStepChildren();

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

        if (shopTutorialRoot == null)
        {
            return;
        }

        stepPanels = new GameObject[shopTutorialPanelCount];
        for (int i = 0; i < shopTutorialPanelCount; i++)
        {
            string panelName = (i + 1).ToString();
            Transform t = shopTutorialRoot.transform.Find(panelName);
            if (t == null)
            {
                Debug.LogWarning(
                    $"{nameof(ShopTutorialPanelController)}: " +
                    $"Missing shop tutorial panel '{panelName}' " +
                    $"under '{shopTutorialRoot.name}'.");
                stepPanels = null;
                return;
            }

            stepPanels[i] = t.gameObject;
        }
    }

    private void HideNonStepChildren()
    {
        if (shopTutorialRoot == null || stepPanels == null)
        {
            return;
        }

        Transform root = shopTutorialRoot.transform;
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
        if (Keyboard.current != null &&
            Keyboard.current.anyKey.wasPressedThisFrame)
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

        return false;
#else
        return Input.anyKeyDown;
#endif
    }
}
