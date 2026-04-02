// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
// Modified by Cursor AI (claude-4.6-opus) for jjmil on 2026-03-24.
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenuUI : MonoBehaviour
{
    private enum MenuPanel
    {
        MainMenu = 0,
        RunSelector = 1,
        Settings = 2,
        Profile = 3,
        Progression = 4,
        Credits
    }

    [Header("Gameplay scene names")]
    [SerializeField] private string gameplayCoreSceneName = "GameplayCore";

    [Header("Quick run boards (in order)")]
    [Tooltip("Assign BoardDefinition assets in the order you want them played for Quick Run.")]
    [SerializeField] private BoardDefinition[] quickRunBoards;

    [Header("Challenge modes")]
    [Tooltip("Optional: define challenge modes via assets (recommended). One button will be created per entry.")]
    [SerializeField] private ChallengeModeDefinition[] challengeModes;

    [Tooltip("Optional fallback: each board becomes its own single-board challenge mode button.")]
    [SerializeField] private BoardDefinition[] challengeBoards;

    [Header("Menu panels (optional - auto-wired if left blank)")]
    [Tooltip("Default panel shown when the menu scene loads.")]
    [SerializeField] private GameObject mainMenuPanel;

    [Tooltip("Panel that contains run selection buttons (Quick Run / Challenges / etc).")]
    [SerializeField] private GameObject runSelectorPanel;

    [Tooltip("Settings screen panel.")]
    [SerializeField] private GameObject settingsPanel;

    [Tooltip("Profile screen panel.")]
    [SerializeField] private GameObject profilePanel;

    [Tooltip("Progression screen panel.")]
    [SerializeField] private GameObject progressionPanel;
    [SerializeField] private GameObject creditPanel;

    [Header("Mode Info Panel (shows challenge mode details before starting)")]
    [Tooltip("Panel that displays mode info (description, win condition) before starting.")]
    [SerializeField] private GameObject modeInfoPanel;

    [Tooltip("Text element to display the mode's display name/title.")]
    [SerializeField] private TMP_Text modeInfoTitleText;

    [Tooltip("Text element to display the mode's description.")]
    [SerializeField] private TMP_Text modeInfoDescriptionText;

    [Tooltip("Text element to display the mode's win condition.")]
    [SerializeField] private TMP_Text modeInfoWinConditionText;

    [Tooltip("Button to start the selected mode from the info panel.")]
    [SerializeField] private Button modeInfoPlayButton;

    [Tooltip("Button to close the info panel and return to run selector.")]
    [SerializeField] private Button modeInfoCloseButton;

    [Header("Player Ships")]
    [Tooltip("Ships available to select before starting a challenge run.")]
    [SerializeField] private PlayerShipDefinition[] availableShips;

    [Tooltip("The parent container for the ship selector UI.")]
    [SerializeField] private GameObject modeInfoShipSelectorContainer;
    
    [Tooltip("Text to display the selected ship's name.")]
    [SerializeField] private TMP_Text modeInfoShipNameText;
    
    [Tooltip("Text to display the selected ship's description.")]
    [SerializeField] private TMP_Text modeInfoShipDescText;
    
    [Tooltip("Button to cycle to the previous ship.")]
    [SerializeField] private Button modeInfoShipLeftButton;
    
    [Tooltip("Button to cycle to the next ship.")]
    [SerializeField] private Button modeInfoShipRightButton;

    private int _selectedShipIndex = 0;

    [Header("UI (optional - auto-wired if left blank)")]
    [Tooltip("Main Menu 'Play' button. If not set, the script will try to find the existing 'Play' button in the scene.")]
    [SerializeField] private Button startRunButton;

    [Tooltip("Main Menu 'Settings' button (opens Settings panel).")]
    [SerializeField] private Button settingsButton;

    [Tooltip("Main Menu 'Profile' button (opens Profile panel).")]
    [SerializeField] private Button profileButton;

    [Tooltip("Main Menu 'Progression' button (opens Progression panel).")]
    [SerializeField] private Button progressionButton;

    [Tooltip("Main Menu 'Quit' button (quits the application).")]
    [SerializeField] private Button quitButton;

    [Tooltip("Parent transform to place challenge buttons under. If not set, one will be created under the first Canvas.")]
    [SerializeField] private RectTransform challengeButtonsRoot;

    [Tooltip("Button prefab/template used for challenge buttons. If not set, the Start Run button is cloned.")]
    [SerializeField] private Button challengeButtonTemplate;

    [Tooltip("Optional prefab for challenge cards. Must have a ChallengeCardUI component. If not set, cards are built from code.")]
    [SerializeField] private GameObject challengeCardPrefab;

    [Header("Run selector extras (optional - auto-wired / auto-created)")]
    [Tooltip("Optional: Return button on the Run Selector panel. If not set, the script will try to find one, but will NOT create it.")]
    [SerializeField] private Button returnToMainMenuButton;

    [SerializeField] private InputActionReference backAction;

    [Header("Runtime (debug)")]
    [SerializeField] private MenuPanel currentPanel = MenuPanel.MainMenu;

    private bool _built;

    // Stores the currently selected mode's boards when viewing the info panel.
    private BoardDefinition[] _pendingChallengeBoards;
    // Stores the full challenge mode definition for round modifier generation.
    private ChallengeModeDefinition _pendingChallengeMode;

    public void LoadGameScene()
    {
        // Backwards-compatible entry point for the existing MainMenu button wiring.
        OpenRunSelectorPanel();
    }

    public void LoadMenuScene()
    {
        SceneManager.LoadScene("MainMenu");
    }

    private void Start()
    {
        BuildMenuIfNeeded();

        ProfileService.ProfileChanged += OnProfileChanged;
        ProfileService.ActiveSlotChanged += OnActiveSlotChanged;
    }

    private void OnDestroy()
    {
        ProfileService.ProfileChanged -= OnProfileChanged;
        ProfileService.ActiveSlotChanged -= OnActiveSlotChanged;
    }

    private void OnProfileChanged(ProfileSlotId slot)
    {
        if (currentPanel == MenuPanel.RunSelector)
        {
            BuildChallengeButtons();
        }
    }

    private void OnActiveSlotChanged(ProfileSlotId slot)
    {
        if (currentPanel == MenuPanel.RunSelector)
        {
            BuildChallengeButtons();
        }
    }

    private void Update()
    {
        if (backAction.action.triggered)
        {
            if (modeInfoPanel != null
                && modeInfoPanel.activeSelf)
            {
                CloseModeInfoPanel();
            }
            else if (currentPanel != MenuPanel.MainMenu)
            {
                OpenMainMenuPanel();
            }
        }
    }

    private void BuildMenuIfNeeded()
    {
        if (_built) return;
        _built = true;

        AutoWirePanels();
        AutoWireMainMenuButtons();
        AutoWireStartRunButton();
        AutoWireChallengeRootAndTemplate();
        AutoWireOrCreateRunSelectorButtons();
        AutoWireOrCreateModeInfoPanel();
        BuildChallengeButtons();

        // Ensure a consistent initial panel state at runtime.
        OpenMainMenuPanel();
    }

    private void AutoWirePanels()
    {
        if (mainMenuPanel == null)
        {
            var go = FindPanelLikeObject("Main Menu");
            if (go != null) mainMenuPanel = go;
        }

        if (runSelectorPanel == null)
        {
            var go = FindPanelLikeObject("Run Selector Panel") ?? FindPanelLikeObject("Run Selector");
            if (go != null) runSelectorPanel = go;
        }

        if (profilePanel == null)
        {
            var go = FindPanelLikeObject("Profile") ?? FindPanelLikeObject("Profile Panel");
            if (go != null) profilePanel = go;
        }

        if (progressionPanel == null)
        {
            var go = FindPanelLikeObject("Progression")
                ?? FindPanelLikeObject("Progression Panel")
                ?? FindPanelLikeObject("Collection")
                ?? FindPanelLikeObject("Collection Panel");
            if (go != null) progressionPanel = go;
        }

        if (modeInfoPanel == null)
        {
            var go = FindPanelLikeObject("Mode Info Panel") ?? FindPanelLikeObject("ModeInfoPanel") ?? FindPanelLikeObject("Mode Info");
            if (go != null) modeInfoPanel = go;
        }
    }

    private void AutoWireMainMenuButtons()
    {
        if (mainMenuPanel == null)
            return;

        if (settingsButton == null)
            settingsButton = FindButtonUnder(mainMenuPanel, "Settings");
        if (profileButton == null)
            profileButton = FindButtonUnder(mainMenuPanel, "Profile");
        if (progressionButton == null)
            progressionButton = FindButtonUnder(mainMenuPanel, "Progression")
                ?? FindButtonUnder(mainMenuPanel, "Collection");
        if (quitButton == null)
            quitButton = FindButtonUnder(mainMenuPanel, "Quit");

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettingsPanel);
            settingsButton.onClick.AddListener(OpenSettingsPanel);
        }

        if (profileButton != null)
        {
            profileButton.onClick.RemoveListener(OpenProfilePanel);
            profileButton.onClick.AddListener(OpenProfilePanel);
        }

        if (progressionButton != null)
        {
            progressionButton.onClick.RemoveListener(OpenProgressionPanel);
            progressionButton.onClick.AddListener(OpenProgressionPanel);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    private void AutoWireStartRunButton()
    {
        if (startRunButton == null)
        {
            startRunButton = mainMenuPanel != null ? FindButtonUnder(mainMenuPanel, "Play") : null;
        }

        if (startRunButton != null)
        {
            startRunButton.onClick.RemoveListener(OpenRunSelectorPanel);
            startRunButton.onClick.AddListener(OpenRunSelectorPanel);
        }
    }

    private void AutoWireChallengeRootAndTemplate()
    {
        if (challengeButtonsRoot != null) return;

        var existing =
            FindAnyGameObjectByNameIncludingInactive(
                "Challenges");

        if (existing != null)
        {
            challengeButtonsRoot =
                existing.GetComponent<RectTransform>();
        }

        if (challengeButtonsRoot == null)
        {
            Canvas canvas;
            canvas = ServiceLocator.Get<Canvas>();
            if (canvas != null)
            {
                var go = new GameObject(
                    "Challenges",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter));

                go.transform.SetParent(
                    canvas.transform, false);

                challengeButtonsRoot =
                    go.GetComponent<RectTransform>();

                challengeButtonsRoot.anchorMin =
                    new Vector2(0.5f, 0.5f);
                challengeButtonsRoot.anchorMax =
                    new Vector2(0.5f, 0.5f);
                challengeButtonsRoot.pivot =
                    new Vector2(0.5f, 0.5f);
                challengeButtonsRoot.anchoredPosition =
                    new Vector2(250f, 50f);
                challengeButtonsRoot.sizeDelta =
                    new Vector2(800f, 600f);

                var vlg = go.GetComponent<
                    VerticalLayoutGroup>();
                vlg.childAlignment =
                    TextAnchor.UpperCenter;
                vlg.childControlWidth = false;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = false;
                vlg.childForceExpandHeight = false;
                vlg.spacing = 16f;
                vlg.padding =
                    new RectOffset(10, 10, 10, 10);

                var fitter = go.GetComponent<
                    ContentSizeFitter>();
                fitter.horizontalFit =
                    ContentSizeFitter.FitMode
                        .Unconstrained;
                fitter.verticalFit =
                    ContentSizeFitter.FitMode
                        .PreferredSize;
            }
        }

        if (challengeButtonTemplate == null)
        {
            challengeButtonTemplate = startRunButton;
        }
    }

    private void AutoWireOrCreateRunSelectorButtons()
    {
        if (challengeButtonsRoot == null)
        {
            return;
        }

        if (returnToMainMenuButton == null)
        {
            var existing = challengeButtonsRoot.Find("RunSelector_Return");
            if (existing != null)
            {
                returnToMainMenuButton = existing.GetComponent<Button>();
            }
        }

        if (returnToMainMenuButton != null)
        {
            returnToMainMenuButton.onClick.RemoveListener(OpenMainMenuPanel);
            returnToMainMenuButton.onClick.AddListener(OpenMainMenuPanel);

            var text = returnToMainMenuButton.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = "Return";
        }
    }

    private void AutoWireOrCreateModeInfoPanel()
    {
        if (modeInfoPanel == null)
        {
            Canvas canvas;
            canvas = ServiceLocator.Get<Canvas>();
            if (canvas != null)
            {
                modeInfoPanel = CreateModeInfoPanelUI(canvas.transform);
            }
        }

        if (modeInfoPanel != null)
        {
            if (modeInfoTitleText == null)
                modeInfoTitleText = FindTextUnder(modeInfoPanel, "ModeInfo_Title");
            if (modeInfoDescriptionText == null)
                modeInfoDescriptionText = FindTextUnder(modeInfoPanel, "ModeInfo_Description");
            if (modeInfoWinConditionText == null)
                modeInfoWinConditionText = FindTextUnder(modeInfoPanel, "ModeInfo_WinCondition");
            if (modeInfoShipNameText == null)
                modeInfoShipNameText = FindTextUnder(modeInfoPanel, "ShipName");
            if (modeInfoShipDescText == null)
                modeInfoShipDescText = FindTextUnder(modeInfoPanel, "ShipDesc");
            if (modeInfoShipLeftButton == null)
                modeInfoShipLeftButton = FindButtonUnder(modeInfoPanel, "Ship_Left");
            if (modeInfoShipRightButton == null)
                modeInfoShipRightButton = FindButtonUnder(modeInfoPanel, "Ship_Right");
            if (modeInfoShipSelectorContainer == null)
            {
                var t = modeInfoPanel.transform.Find("ShipSelector");
                if (t != null) modeInfoShipSelectorContainer = t.gameObject;
            }

            if (modeInfoPlayButton != null)
            {
                modeInfoPlayButton.onClick.RemoveListener(OnModeInfoPlayClicked);
                modeInfoPlayButton.onClick.AddListener(OnModeInfoPlayClicked);
            }

            if (modeInfoCloseButton != null)
            {
                modeInfoCloseButton.onClick.RemoveListener(CloseModeInfoPanel);
                modeInfoCloseButton.onClick.AddListener(CloseModeInfoPanel);
            }

            if (modeInfoShipLeftButton != null)
            {
                modeInfoShipLeftButton.onClick.RemoveListener(OnShipLeftClicked);
                modeInfoShipLeftButton.onClick.AddListener(OnShipLeftClicked);
            }

            if (modeInfoShipRightButton != null)
            {
                modeInfoShipRightButton.onClick.RemoveListener(OnShipRightClicked);
                modeInfoShipRightButton.onClick.AddListener(OnShipRightClicked);
            }
        }
    }

    private void OnShipLeftClicked()
    {
        if (availableShips == null || availableShips.Length == 0) return;
        _selectedShipIndex--;
        if (_selectedShipIndex < 0) _selectedShipIndex = availableShips.Length - 1;
        UpdateShipUI();
    }

    private void OnShipRightClicked()
    {
        if (availableShips == null || availableShips.Length == 0) return;
        _selectedShipIndex++;
        if (_selectedShipIndex >= availableShips.Length) _selectedShipIndex = 0;
        UpdateShipUI();
    }

    private void UpdateShipUI()
    {
        if (availableShips == null || availableShips.Length == 0)
        {
            if (modeInfoShipSelectorContainer != null) modeInfoShipSelectorContainer.SetActive(false);
            return;
        }

        if (modeInfoShipSelectorContainer != null) modeInfoShipSelectorContainer.SetActive(true);

        _selectedShipIndex = Mathf.Clamp(_selectedShipIndex, 0, availableShips.Length - 1);
        var ship = availableShips[_selectedShipIndex];

        if (modeInfoShipNameText != null) modeInfoShipNameText.text = ship.displayName;
        if (modeInfoShipDescText != null) modeInfoShipDescText.text = ship.description;
    }

    private GameObject CreateModeInfoPanelUI(Transform parent)
    {
        var panelGo = new GameObject("Mode Info Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelGo.transform.SetParent(parent, false);

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var bgImage = panelGo.GetComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.85f);

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(panelGo.transform, false);

        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(600f, 400f);

        var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 20f;
        vlg.padding = new RectOffset(40, 40, 40, 40);

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        modeInfoTitleText = CreateTextElement(contentGo.transform, "ModeInfo_Title", "Mode Title", 42, FontStyles.Bold, TextAlignmentOptions.Center);
        modeInfoDescriptionText = CreateTextElement(contentGo.transform, "ModeInfo_Description", "Description goes here.", 24, FontStyles.Normal, TextAlignmentOptions.Center);
        modeInfoWinConditionText = CreateTextElement(contentGo.transform, "ModeInfo_WinCondition", "Win Condition: ...", 22, FontStyles.Italic, TextAlignmentOptions.Center);

        modeInfoShipSelectorContainer = new GameObject("ShipSelector", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        modeInfoShipSelectorContainer.transform.SetParent(contentGo.transform, false);
        var ssLayout = modeInfoShipSelectorContainer.GetComponent<HorizontalLayoutGroup>();
        ssLayout.childAlignment = TextAnchor.MiddleCenter;
        ssLayout.childControlWidth = false;
        ssLayout.childControlHeight = false;
        ssLayout.spacing = 20f;
        
        modeInfoShipLeftButton = CreateButton(modeInfoShipSelectorContainer.transform, "Ship_Left", "<", new Color(0.2f, 0.2f, 0.2f, 1f));
        modeInfoShipLeftButton.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 60f);

        var shipInfoGo = new GameObject("ShipInfo", typeof(RectTransform), typeof(VerticalLayoutGroup));
        shipInfoGo.transform.SetParent(modeInfoShipSelectorContainer.transform, false);
        var siLayout = shipInfoGo.GetComponent<VerticalLayoutGroup>();
        siLayout.childAlignment = TextAnchor.MiddleCenter;
        siLayout.childControlWidth = true;
        siLayout.childControlHeight = true;
        siLayout.spacing = 5f;
        
        modeInfoShipNameText = CreateTextElement(shipInfoGo.transform, "ShipName", "Ship Name", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        modeInfoShipNameText.GetComponent<LayoutElement>().preferredWidth = 350f;
        modeInfoShipDescText = CreateTextElement(shipInfoGo.transform, "ShipDesc", "Ship Description", 18, FontStyles.Normal, TextAlignmentOptions.Center);
        modeInfoShipDescText.GetComponent<LayoutElement>().preferredWidth = 350f;

        modeInfoShipRightButton = CreateButton(modeInfoShipSelectorContainer.transform, "Ship_Right", ">", new Color(0.2f, 0.2f, 0.2f, 1f));
        modeInfoShipRightButton.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 60f);

        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(contentGo.transform, false);
        spacer.GetComponent<LayoutElement>().minHeight = 30f;

        var buttonsGo = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonsGo.transform.SetParent(contentGo.transform, false);

        var btnLayout = buttonsGo.GetComponent<HorizontalLayoutGroup>();
        btnLayout.childAlignment = TextAnchor.MiddleCenter;
        btnLayout.childControlWidth = false;
        btnLayout.childControlHeight = false;
        btnLayout.spacing = 40f;

        modeInfoPlayButton = CreateButton(buttonsGo.transform, "ModeInfo_Play", "PLAY", new Color(0.2f, 0.7f, 0.3f, 1f));
        modeInfoCloseButton = CreateButton(buttonsGo.transform, "ModeInfo_Close", "BACK", new Color(0.5f, 0.5f, 0.5f, 1f));

        panelGo.SetActive(false);
        return panelGo;
    }

    private static TMP_Text CreateTextElement(Transform parent, string name, string defaultText, float fontSize, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = defaultText;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TMPro.TextWrappingModes.Normal;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 520f;

        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160f, 60f);

        var img = go.GetComponent<Image>();
        img.color = bgColor;

        var btn = go.GetComponent<Button>();

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var labelText = labelGo.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 28;
        labelText.fontStyle = FontStyles.Bold;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;

        ColorBlock buttonColors = btn.colors;
        buttonColors.selectedColor = Color.gray;
        btn.colors = buttonColors;

        ServiceLocator.Get<AudioManager>()?.WireButtonAudio(btn);
        return btn;
    }

    private static TMP_Text FindTextUnder(GameObject root, string name)
    {
        if (root == null) return null;

        var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null || t.name != name) continue;
            var txt = t.GetComponent<TMP_Text>();
            if (txt != null) return txt;
        }
        return null;
    }

    [SerializeField] private float cardWidth = 700f;
    [SerializeField] private float cardHeight = 120f;

    private static readonly Color cardBgColor =
        new Color(0.14f, 0.14f, 0.18f, 0.95f);

    private static readonly Color cardHoverColor =
        new Color(0.22f, 0.22f, 0.28f, 0.95f);

    private static readonly Color filledStarColor =
        new Color(1f, 0.85f, 0.1f, 1f);

    private static readonly Color emptyStarColor =
        new Color(0.4f, 0.4f, 0.4f, 0.6f);

    private const string filledStar = "\u2605";
    private const string emptyStar = "\u2606";

    private void BuildChallengeButtons()
    {
        if (challengeButtonsRoot == null)
        {
            return;
        }

        for (int i =
            challengeButtonsRoot.childCount - 1;
            i >= 0; i--)
        {
            var child =
                challengeButtonsRoot.GetChild(i);

            if (child != null
                && child.name.StartsWith(
                    "ChallengeButton_"))
            {
                Destroy(child.gameObject);
            }
        }

        int created = 0;

        if (challengeModes != null
            && challengeModes.Length > 0)
        {
            for (int i = 0;
                i < challengeModes.Length; i++)
            {
                var mode = challengeModes[i];

                if (mode == null) continue;

                var boards = mode.boards;

                if (boards == null
                    || boards.Length == 0)
                {
                    continue;
                }

                var capturedMode = mode;

                CreateChallengeCard(
                    capturedMode,
                    () => ShowModeInfoPanel(
                        capturedMode));

                created++;
            }
        }

        if (created == 0
            && challengeBoards != null
            && challengeBoards.Length > 0)
        {
            for (int i = 0;
                i < challengeBoards.Length; i++)
            {
                var board = challengeBoards[i];

                if (board == null) continue;

                var capturedBoard = board;

                CreateFallbackChallengeCard(
                    capturedBoard,
                    () => ShowModeInfoPanelForBoard(
                        capturedBoard));
            }
        }
    }

    private void CreateChallengeCard(
        ChallengeModeDefinition mode,
        UnityEngine.Events.UnityAction onClick)
    {
        long bestScore =
            ProfileService.GetChallengeBestScore(
                mode.displayName);

        RunRank rank = RunRankUtility.Calculate(
            bestScore,
            mode.cRankThreshold,
            mode.bRankThreshold,
            mode.aRankThreshold,
            mode.sRankThreshold,
            mode.sPlusThreshold);

        int stars = bestScore > 0L
            ? RunRankUtility.GetStarCount(rank)
            : 0;

        string rankLabel = bestScore > 0L
            ? RunRankUtility.GetLabel(rank)
            : "--";

        var card = BuildCardGameObject(
            mode.displayName,
            mode.icon,
            bestScore,
            rankLabel,
            stars,
            onClick);

        card.name =
            $"ChallengeButton_{mode.displayName}";
    }

    private void CreateFallbackChallengeCard(
        BoardDefinition board,
        UnityEngine.Events.UnityAction onClick)
    {
        string label =
            string.IsNullOrWhiteSpace(
                board.displayName)
                ? "Challenge"
                : board.displayName;

        var card = BuildCardGameObject(
            label, null, 0L, "--", 0, onClick);

        card.name = $"ChallengeButton_{label}";
    }

    private GameObject BuildCardGameObject(
        string displayName,
        Sprite icon,
        long bestScore,
        string rankLabel,
        int starCount,
        UnityEngine.Events.UnityAction onClick)
    {
        if (challengeCardPrefab != null)
        {
            return BuildCardFromPrefab(
                displayName, icon,
                bestScore, rankLabel,
                starCount, onClick);
        }

        return BuildCardFromCode(
            displayName, icon,
            bestScore, rankLabel,
            starCount, onClick);
    }

    private GameObject BuildCardFromPrefab(
        string displayName,
        Sprite icon,
        long bestScore,
        string rankLabel,
        int starCount,
        UnityEngine.Events.UnityAction onClick)
    {
        var cardGo = Instantiate(
            challengeCardPrefab,
            challengeButtonsRoot);

        var card =
            cardGo.GetComponent<ChallengeCardUI>();

        if (card != null)
        {
            if (card.gradeText != null)
            {
                card.gradeText.text = rankLabel;
                card.gradeText.color = bestScore > 0L
                    ? GetRankColor(rankLabel)
                    : new Color(
                        0.4f, 0.4f, 0.4f, 0.6f);
            }

            if (card.bestScoreText != null)
            {
                card.bestScoreText.text =
                    bestScore > 0L
                        ? FormatScoreValue(bestScore)
                        : "0";
            }

            if (card.starImages != null)
            {
                for (int i = 0;
                    i < card.starImages.Length; i++)
                {
                    if (card.starImages[i] == null)
                        continue;

                    card.starImages[i].sprite =
                        i < starCount
                            ? card.filledStarSprite
                            : card.emptyStarSprite;
                }
            }

            if (card.nameText != null)
            {
                card.nameText.text = displayName;
            }

        }

        var btn =
            cardGo.GetComponentInChildren<Button>();

        if (btn != null && onClick != null)
        {
            btn.onClick.AddListener(onClick);
        }

        if (btn != null)
        {
            ServiceLocator.Get<AudioManager>()
                ?.WireButtonAudio(btn);
        }

        return cardGo;
    }

    private GameObject BuildCardFromCode(
        string displayName,
        Sprite icon,
        long bestScore,
        string rankLabel,
        int starCount,
        UnityEngine.Events.UnityAction onClick)
    {
        var cardGo = new GameObject(
            "Card",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));

        cardGo.transform.SetParent(
            challengeButtonsRoot, false);

        var cardRect =
            cardGo.GetComponent<RectTransform>();
        cardRect.sizeDelta =
            new Vector2(cardWidth, cardHeight);

        var le = cardGo.GetComponent<LayoutElement>();
        le.preferredWidth = cardWidth;
        le.preferredHeight = cardHeight;

        var cardBg = cardGo.GetComponent<Image>();
        cardBg.color = cardBgColor;

        var btn = cardGo.GetComponent<Button>();

        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor =
            new Color(1.3f, 1.3f, 1.4f, 1f);
        colors.selectedColor =
            new Color(1.2f, 1.2f, 1.3f, 1f);
        colors.pressedColor =
            new Color(0.9f, 0.9f, 0.9f, 1f);
        btn.colors = colors;
        btn.targetGraphic = cardBg;

        if (onClick != null)
        {
            btn.onClick.AddListener(onClick);
        }

        ServiceLocator.Get<AudioManager>()
            ?.WireButtonAudio(btn);

        float iconAreaWidth = cardHeight - 16f;

        var iconGo = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(Image));

        iconGo.transform.SetParent(
            cardGo.transform, false);

        var iconRect =
            iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition =
            new Vector2(12f, 0f);
        iconRect.sizeDelta =
            new Vector2(iconAreaWidth, -16f);

        var iconImg = iconGo.GetComponent<Image>();

        if (icon != null)
        {
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
        }
        else
        {
            iconImg.color =
                new Color(0.3f, 0.3f, 0.3f, 0.5f);
        }

        float textLeftEdge = iconAreaWidth + 24f;

        var nameGo = new GameObject(
            "Name",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));

        nameGo.transform.SetParent(
            cardGo.transform, false);

        var nameRect =
            nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.55f);
        nameRect.anchorMax = new Vector2(0.6f, 0.95f);
        nameRect.offsetMin =
            new Vector2(textLeftEdge, 0f);
        nameRect.offsetMax = new Vector2(0f, -4f);

        var nameTmp =
            nameGo.GetComponent<TextMeshProUGUI>();
        nameTmp.text = displayName;
        nameTmp.fontSize = 28f;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.alignment =
            TextAlignmentOptions.BottomLeft;
        nameTmp.color = Color.white;
        nameTmp.textWrappingMode =
            TextWrappingModes.NoWrap;
        nameTmp.overflowMode =
            TextOverflowModes.Ellipsis;

        var scoreGo = new GameObject(
            "BestScore",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));

        scoreGo.transform.SetParent(
            cardGo.transform, false);

        var scoreRect =
            scoreGo.GetComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0f, 0.1f);
        scoreRect.anchorMax = new Vector2(0.6f, 0.5f);
        scoreRect.offsetMin =
            new Vector2(textLeftEdge, 0f);
        scoreRect.offsetMax = new Vector2(0f, 0f);

        var scoreTmp =
            scoreGo.GetComponent<TextMeshProUGUI>();

        scoreTmp.text = bestScore > 0L
            ? $"Best: {FormatScoreValue(bestScore)}"
            : "No attempts yet";

        scoreTmp.fontSize = 18f;
        scoreTmp.alignment =
            TextAlignmentOptions.TopLeft;
        scoreTmp.color = bestScore > 0L
            ? new Color(0.8f, 0.8f, 0.8f, 1f)
            : new Color(0.5f, 0.5f, 0.5f, 0.8f);
        scoreTmp.textWrappingMode =
            TextWrappingModes.NoWrap;

        var rankGo = new GameObject(
            "Rank",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));

        rankGo.transform.SetParent(
            cardGo.transform, false);

        var rankRect =
            rankGo.GetComponent<RectTransform>();
        rankRect.anchorMin =
            new Vector2(0.62f, 0.15f);
        rankRect.anchorMax =
            new Vector2(0.78f, 0.85f);
        rankRect.offsetMin = Vector2.zero;
        rankRect.offsetMax = Vector2.zero;

        var rankTmp =
            rankGo.GetComponent<TextMeshProUGUI>();
        rankTmp.text = rankLabel;
        rankTmp.fontSize = 36f;
        rankTmp.fontStyle = FontStyles.Bold;
        rankTmp.alignment =
            TextAlignmentOptions.Center;
        rankTmp.textWrappingMode =
            TextWrappingModes.NoWrap;

        rankTmp.color = bestScore > 0L
            ? GetRankColor(rankLabel)
            : new Color(0.4f, 0.4f, 0.4f, 0.6f);

        var starsGo = new GameObject(
            "Stars",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));

        starsGo.transform.SetParent(
            cardGo.transform, false);

        var starsRect =
            starsGo.GetComponent<RectTransform>();
        starsRect.anchorMin =
            new Vector2(0.8f, 0.15f);
        starsRect.anchorMax =
            new Vector2(0.98f, 0.85f);
        starsRect.offsetMin = Vector2.zero;
        starsRect.offsetMax = Vector2.zero;

        var starsTmp =
            starsGo.GetComponent<TextMeshProUGUI>();

        string starStr = "";

        for (int i = 0; i < 3; i++)
        {
            starStr += i < starCount
                ? filledStar
                : emptyStar;
        }

        starsTmp.text = starStr;
        starsTmp.fontSize = 32f;
        starsTmp.alignment =
            TextAlignmentOptions.Center;
        starsTmp.textWrappingMode =
            TextWrappingModes.NoWrap;
        starsTmp.enableAutoSizing = true;
        starsTmp.fontSizeMin = 20f;
        starsTmp.fontSizeMax = 32f;

        starsTmp.color = starCount > 0
            ? filledStarColor
            : emptyStarColor;

        return cardGo;
    }

    private static Color GetRankColor(string label)
    {
        if (string.IsNullOrEmpty(label)
            || label == "--")
        {
            return new Color(0.4f, 0.4f, 0.4f, 0.6f);
        }

        char letter = label[0];

        switch (letter)
        {
            case 'S':
                return new Color(
                    1f, 0.85f, 0.1f, 1f);
            case 'A':
                return new Color(
                    0.3f, 0.9f, 0.4f, 1f);
            case 'B':
                return new Color(
                    0.3f, 0.6f, 1f, 1f);
            case 'C':
                return new Color(
                    0.85f, 0.85f, 0.85f, 1f);
            default:
                return new Color(
                    0.6f, 0.6f, 0.6f, 1f);
        }
    }

    private static string FormatScoreValue(
        long value)
    {
        if (value >= 1000000L)
        {
            double m = value / 1000000.0;
            return m.ToString(
                "F1", CultureInfo.InvariantCulture)
                + "M";
        }

        if (value >= 1000L)
        {
            double k = value / 1000.0;
            return k.ToString(
                "F1", CultureInfo.InvariantCulture)
                + "K";
        }

        return value.ToString(
            "N0", CultureInfo.InvariantCulture);
    }

    public void StartQuickRun()
    {
        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        GameSession.Instance.ConfigureQuickRun(quickRunBoards, seed);
        SceneManager.LoadScene(gameplayCoreSceneName);
    }

    public void StartChallengeBoards(BoardDefinition[] boards)
    {
        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

        PlayerShipDefinition selectedShip = null;
        if (availableShips != null && availableShips.Length > 0 && _selectedShipIndex >= 0 && _selectedShipIndex < availableShips.Length)
        {
            selectedShip = availableShips[_selectedShipIndex];
        }

        if (_pendingChallengeMode != null)
        {
            GameSession.Instance.ConfigureChallenge(_pendingChallengeMode, selectedShip, seed);
        }
        else
        {
            GameSession.Instance.ConfigureChallenge(boards, seed); // Fallback single boards
            // Single board challenge Config doesn't have ship parameter right now, let's just ignore or update it if needed.
            // Since User said "no just challenges", we pass it for ChallengeModeDefinition. 
        }

        SceneManager.LoadScene(gameplayCoreSceneName);
    }

    public void OpenRunSelectorPanel()
    {
        BuildChallengeButtons();
        SetPanel(MenuPanel.RunSelector);
    }

    public void OpenMainMenuPanel()
    {
        SetPanel(MenuPanel.MainMenu);
    }

    public void OpenSettingsPanel()
    {
        SetPanel(MenuPanel.Settings);
    }

    public void OpenProfilePanel()
    {
        SetPanel(MenuPanel.Profile);
    }

    public void OpenProgressionPanel()
    {
        SetPanel(MenuPanel.Progression);
    }

    public void OpenCreditPanel()
    {
        SetPanel(MenuPanel.Credits);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ShowModeInfoPanel(ChallengeModeDefinition mode)
    {
        if (mode == null) return;

        _pendingChallengeMode = mode;
        _pendingChallengeBoards = mode.boards;

        if (modeInfoTitleText != null)
            modeInfoTitleText.text = mode.displayName;

        if (modeInfoDescriptionText != null)
            modeInfoDescriptionText.text = string.IsNullOrWhiteSpace(mode.description) ? "" : mode.description;

        if (modeInfoWinConditionText != null)
        {
            if (string.IsNullOrWhiteSpace(mode.winConditionDescription))
            {
                modeInfoWinConditionText.text = GenerateDefaultWinCondition(mode.boards);
            }
            else
            {
                modeInfoWinConditionText.text = mode.winConditionDescription;
            }
        }

        if (modeInfoPanel != null)
        {
            UpdateShipUI();
            modeInfoPanel.SetActive(true);
        }
    }

    public void ShowModeInfoPanelForBoard(BoardDefinition board)
    {
        if (board == null) return;

        _pendingChallengeMode = null; 
        _pendingChallengeBoards = new[] { board };

        if (modeInfoTitleText != null)
            modeInfoTitleText.text = board.displayName;

        if (modeInfoDescriptionText != null)
            modeInfoDescriptionText.text = "";

        if (modeInfoWinConditionText != null)
            modeInfoWinConditionText.text = GenerateDefaultWinCondition(new[] { board });

        if (modeInfoPanel != null)
        {
            UpdateShipUI();
            modeInfoPanel.SetActive(true);
        }
    }

    private string GenerateDefaultWinCondition(BoardDefinition[] boards)
    {
        if (boards == null || boards.Length == 0)
            return "Complete the challenge!";

        if (boards.Length == 1)
        {
            var board = boards[0];
            return GetBoardClearConditionText(board);
        }

        return $"Complete all {boards.Length} boards to win!";
    }

    private string GetBoardClearConditionText(BoardDefinition board)
    {
        if (board == null) return "Complete the board!";

        switch (board.clearCondition)
        {
            case BoardDefinition.ClearConditionKind.LevelIndexAtLeast:
                return $"Reach level {board.targetRoundIndex}";
            case BoardDefinition.ClearConditionKind.CoinsAtLeast:
                return $"Collect {board.targetCoins} coins";
            case BoardDefinition.ClearConditionKind.TotalScoreAtLeast:
                return $"Reach {board.targetRoundTotal:N0} total score";
            case BoardDefinition.ClearConditionKind.None:
            default:
                return "Complete the board!";
        }
    }

    private void OnModeInfoPlayClicked()
    {
        if (_pendingChallengeBoards != null && _pendingChallengeBoards.Length > 0)
        {
            StartChallengeBoards(_pendingChallengeBoards);
        }
    }

    public void CloseModeInfoPanel()
    {
        _pendingChallengeBoards = null;
        _pendingChallengeMode = null;
        if (modeInfoPanel != null)
            modeInfoPanel.SetActive(false);
    }

    private void SetPanel(MenuPanel panel)
    {
        currentPanel = panel;

        if (mainMenuPanel != null) mainMenuPanel.SetActive(panel == MenuPanel.MainMenu);

        if (runSelectorPanel != null) runSelectorPanel.SetActive(panel == MenuPanel.RunSelector);
        if (settingsPanel != null) settingsPanel.SetActive(panel == MenuPanel.Settings);
        if (profilePanel != null) profilePanel.SetActive(panel == MenuPanel.Profile);
        if (progressionPanel != null) progressionPanel.SetActive(panel == MenuPanel.Progression);
        if (creditPanel != null) creditPanel.SetActive(panel == MenuPanel.Credits);

        if (modeInfoPanel != null) modeInfoPanel.SetActive(false);
    }

    private static Button FindButtonUnder(GameObject root, string name)
    {
        if (root == null) return null;

        var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null || t.name != name) continue;
            var btn = t.GetComponent<Button>();
            if (btn != null) return btn;
        }
        return null;
    }

    private static GameObject FindAnyGameObjectByNameIncludingInactive(string name)
    {
        var transforms = FindAllTransformsInLoadedScenes(includeInactive: true);
        for (int i = 0; i < transforms.Count; i++)
        {
            var t = transforms[i];
            if (t != null && t.name == name)
                return t.gameObject;
        }
        return null;
    }

    private static GameObject FindPanelLikeObject(string name)
    {
        var transforms = FindAllTransformsInLoadedScenes(includeInactive: true);

        GameObject best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < transforms.Count; i++)
        {
            var t = transforms[i];
            if (t == null || t.name != name) continue;

            var rt = t.GetComponent<RectTransform>();
            if (rt == null) continue;

            int score = 0;
            if (!t.gameObject.activeSelf) score += 100;
            if (rt.sizeDelta.x >= 1000f && rt.sizeDelta.y >= 600f) score += 50;
            if (t.GetComponent<Image>() != null) score += 10;

            if (score > bestScore)
            {
                bestScore = score;
                best = t.gameObject;
            }
        }

        return best;
    }

    private static List<Transform> FindAllTransformsInLoadedScenes(bool includeInactive)
    {
        Transform[] all;

        all = Object.FindObjectsByType<Transform>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        var result = new List<Transform>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded) continue;
            if (!includeInactive && !t.gameObject.activeInHierarchy) continue;

            result.Add(t);
        }

        return result;
    }
}