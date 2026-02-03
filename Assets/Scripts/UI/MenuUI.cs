using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
        Collection = 4
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

    [Tooltip("Collection screen panel.")]
    [SerializeField] private GameObject collectionPanel;

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

    [Header("UI (optional - auto-wired if left blank)")]
    [Tooltip("Main Menu 'Play' button. If not set, the script will try to find the existing 'Play' button in the scene.")]
    [SerializeField] private Button startRunButton;

    [Tooltip("Main Menu 'Settings' button (opens Settings panel).")]
    [SerializeField] private Button settingsButton;

    [Tooltip("Main Menu 'Profile' button (opens Profile panel).")]
    [SerializeField] private Button profileButton;

    [Tooltip("Main Menu 'Collection' button (opens Collection panel).")]
    [SerializeField] private Button collectionButton;

    [Tooltip("Parent transform to place challenge buttons under. If not set, one will be created under the first Canvas.")]
    [SerializeField] private RectTransform challengeButtonsRoot;

    [Tooltip("Button prefab/template used for challenge buttons. If not set, the Start Run button is cloned.")]
    [SerializeField] private Button challengeButtonTemplate;

    [Header("Run selector extras (optional - auto-wired / auto-created)")]
    [Tooltip("Optional: Return button on the Run Selector panel. If not set, the script will try to find one, but will NOT create it.")]
    [SerializeField] private Button returnToMainMenuButton;

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
    }

    private void Update()
    {
        // Allow Esc to return from sub-panels.
        if (WasEscapePressedThisFrame())
        {
            // First check if mode info overlay is open.
            if (modeInfoPanel != null && modeInfoPanel.activeSelf)
            {
                CloseModeInfoPanel();
            }
            else if (currentPanel != MenuPanel.MainMenu)
            {
                // From other sub-panels, go back to main menu.
                OpenMainMenuPanel();
            }
        }
    }

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
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

        if (settingsPanel == null)
        {
            // Note: there is also usually a main menu button named "Settings". We prefer the panel-like object.
            var go = FindPanelLikeObject("Settings") ?? FindPanelLikeObject("Settings Panel");
            if (go != null) settingsPanel = go;
        }

        if (profilePanel == null)
        {
            // Note: there is also usually a main menu button named "Profile". We prefer the panel-like object.
            var go = FindPanelLikeObject("Profile") ?? FindPanelLikeObject("Profile Panel");
            if (go != null) profilePanel = go;
        }

        if (collectionPanel == null)
        {
            // Note: there is also usually a main menu button named "Collection". We prefer the panel-like object.
            var go = FindPanelLikeObject("Collection") ?? FindPanelLikeObject("Collection Panel");
            if (go != null) collectionPanel = go;
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
        if (collectionButton == null)
            collectionButton = FindButtonUnder(mainMenuPanel, "Collection");

        if (settingsButton != null)
        {
            settingsButton.onClick = new Button.ButtonClickedEvent();
            settingsButton.onClick.AddListener(OpenSettingsPanel);
        }

        if (profileButton != null)
        {
            profileButton.onClick = new Button.ButtonClickedEvent();
            profileButton.onClick.AddListener(OpenProfilePanel);
        }

        if (collectionButton != null)
        {
            collectionButton.onClick = new Button.ButtonClickedEvent();
            collectionButton.onClick.AddListener(OpenCollectionPanel);
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
            // Ensure the Play button opens the Run Selector panel (even if the scene has stale wiring).
            startRunButton.onClick = new Button.ButtonClickedEvent();
            startRunButton.onClick.AddListener(OpenRunSelectorPanel);
        }
    }

    private void AutoWireChallengeRootAndTemplate()
    {
        if (challengeButtonsRoot != null) return;

        // Try to find an existing container by name first.
        var existing = FindAnyGameObjectByNameIncludingInactive("Challenges");
        if (existing != null)
        {
            challengeButtonsRoot = existing.GetComponent<RectTransform>();
        }

        if (challengeButtonsRoot == null)
        {
            Canvas canvas;
#if UNITY_2022_2_OR_NEWER
            canvas = FindFirstObjectByType<Canvas>();
#else
            canvas = FindObjectOfType<Canvas>();
#endif
            if (canvas != null)
            {
                var go = new GameObject("Challenges", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                go.transform.SetParent(canvas.transform, false);

                challengeButtonsRoot = go.GetComponent<RectTransform>();
                challengeButtonsRoot.anchorMin = new Vector2(0.5f, 0.5f);
                challengeButtonsRoot.anchorMax = new Vector2(0.5f, 0.5f);
                challengeButtonsRoot.pivot = new Vector2(0.5f, 0.5f);
                challengeButtonsRoot.anchoredPosition = new Vector2(250f, 125f);
                challengeButtonsRoot.sizeDelta = new Vector2(400f, 400f);

                var vlg = go.GetComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.spacing = 12f;

                var fitter = go.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        if (challengeButtonTemplate == null)
        {
            challengeButtonTemplate = startRunButton;
        }
    }

    private void AutoWireOrCreateRunSelectorButtons()
    {
        // We prefer to find the Return button under the same root used for challenge buttons,
        // since it already exists inside the Run Selector panel in the scene.
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
            returnToMainMenuButton.onClick = new Button.ButtonClickedEvent();
            returnToMainMenuButton.onClick.AddListener(OpenMainMenuPanel);
            var text = returnToMainMenuButton.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = "Return";
        }
    }

    private void AutoWireOrCreateModeInfoPanel()
    {
        // If no panel assigned, try to create one programmatically.
        if (modeInfoPanel == null)
        {
            Canvas canvas;
#if UNITY_2022_2_OR_NEWER
            canvas = FindFirstObjectByType<Canvas>();
#else
            canvas = FindObjectOfType<Canvas>();
#endif
            if (canvas != null)
            {
                modeInfoPanel = CreateModeInfoPanelUI(canvas.transform);
            }
        }

        // Wire up buttons if panel exists.
        if (modeInfoPanel != null)
        {
            // Try to find UI elements if not assigned.
            if (modeInfoTitleText == null)
                modeInfoTitleText = FindTextUnder(modeInfoPanel, "ModeInfo_Title");
            if (modeInfoDescriptionText == null)
                modeInfoDescriptionText = FindTextUnder(modeInfoPanel, "ModeInfo_Description");
            if (modeInfoWinConditionText == null)
                modeInfoWinConditionText = FindTextUnder(modeInfoPanel, "ModeInfo_WinCondition");
            if (modeInfoPlayButton == null)
                modeInfoPlayButton = FindButtonUnder(modeInfoPanel, "ModeInfo_Play");
            if (modeInfoCloseButton == null)
                modeInfoCloseButton = FindButtonUnder(modeInfoPanel, "ModeInfo_Close");

            // Wire play button.
            if (modeInfoPlayButton != null)
            {
                modeInfoPlayButton.onClick = new Button.ButtonClickedEvent();
                modeInfoPlayButton.onClick.AddListener(OnModeInfoPlayClicked);
            }

            // Wire close button.
            if (modeInfoCloseButton != null)
            {
                modeInfoCloseButton.onClick = new Button.ButtonClickedEvent();
                modeInfoCloseButton.onClick.AddListener(CloseModeInfoPanel);
            }
        }
    }

    private GameObject CreateModeInfoPanelUI(Transform parent)
    {
        // Create the panel container.
        var panelGo = new GameObject("Mode Info Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelGo.transform.SetParent(parent, false);

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Semi-transparent dark background.
        var bgImage = panelGo.GetComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.85f);

        // Create content container (centered).
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

        // Title text.
        modeInfoTitleText = CreateTextElement(contentGo.transform, "ModeInfo_Title", "Mode Title", 42, FontStyles.Bold, TextAlignmentOptions.Center);

        // Description text.
        modeInfoDescriptionText = CreateTextElement(contentGo.transform, "ModeInfo_Description", "Description goes here.", 24, FontStyles.Normal, TextAlignmentOptions.Center);

        // Win condition text.
        modeInfoWinConditionText = CreateTextElement(contentGo.transform, "ModeInfo_WinCondition", "Win Condition: ...", 22, FontStyles.Italic, TextAlignmentOptions.Center);

        // Spacer.
        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(contentGo.transform, false);
        spacer.GetComponent<LayoutElement>().minHeight = 30f;

        // Buttons container.
        var buttonsGo = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonsGo.transform.SetParent(contentGo.transform, false);

        var btnLayout = buttonsGo.GetComponent<HorizontalLayoutGroup>();
        btnLayout.childAlignment = TextAnchor.MiddleCenter;
        btnLayout.childControlWidth = false;
        btnLayout.childControlHeight = false;
        btnLayout.spacing = 40f;

        // Play button.
        modeInfoPlayButton = CreateButton(buttonsGo.transform, "ModeInfo_Play", "PLAY", new Color(0.2f, 0.7f, 0.3f, 1f));

        // Close button.
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

        // Button label.
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

    private void BuildChallengeButtons()
    {
        if (challengeButtonsRoot == null || challengeButtonTemplate == null)
        {
            return;
        }

        // Clear previously generated buttons (keep any existing non-generated children).
        for (int i = challengeButtonsRoot.childCount - 1; i >= 0; i--)
        {
            var child = challengeButtonsRoot.GetChild(i);
            if (child != null && child.name.StartsWith("ChallengeButton_"))
            {
                Destroy(child.gameObject);
            }
        }

        int created = 0;

        // Preferred: ChallengeMode assets.
        if (challengeModes != null && challengeModes.Length > 0)
        {
            for (int i = 0; i < challengeModes.Length; i++)
            {
                var mode = challengeModes[i];
                if (mode == null) continue;

                var boards = mode.boards;
                if (boards == null || boards.Length == 0) continue;

                // Capture the mode reference for the lambda.
                var capturedMode = mode;
                CreateChallengeButton(mode.displayName, () => ShowModeInfoPanel(capturedMode));
                created++;
            }
        }

        // Fallback: per-board single challenges.
        if (created == 0 && challengeBoards != null && challengeBoards.Length > 0)
        {
            for (int i = 0; i < challengeBoards.Length; i++)
            {
                var board = challengeBoards[i];
                if (board == null) continue;
                string label = string.IsNullOrWhiteSpace(board.displayName) ? "Challenge" : board.displayName;
                // Capture the board reference for the lambda.
                var capturedBoard = board;
                CreateChallengeButton(label, () => ShowModeInfoPanelForBoard(capturedBoard));
            }
        }
    }

    private void CreateChallengeButton(string label, UnityEngine.Events.UnityAction onClick)
    {
        var btn = Instantiate(challengeButtonTemplate, challengeButtonsRoot);
        btn.name = $"ChallengeButton_{label}";

        // Clear template wiring.
        btn.onClick = new Button.ButtonClickedEvent();
        if (onClick != null)
        {
            btn.onClick.AddListener(onClick);
        }

        var text = btn.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
        }
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

        // Use the full challenge mode if available (for round modifier support)
        if (_pendingChallengeMode != null)
        {
            GameSession.Instance.ConfigureChallenge(_pendingChallengeMode, seed);
        }
        else
        {
            GameSession.Instance.ConfigureChallenge(boards, seed);
        }

        SceneManager.LoadScene(gameplayCoreSceneName);
    }

    public void OpenRunSelectorPanel()
    {
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

    public void OpenCollectionPanel()
    {
        SetPanel(MenuPanel.Collection);
    }

    /// <summary>
    /// Shows the mode info panel with details from the given challenge mode definition.
    /// </summary>
    public void ShowModeInfoPanel(ChallengeModeDefinition mode)
    {
        if (mode == null) return;

        // Store the challenge mode and boards for when the user clicks Play.
        _pendingChallengeMode = mode;
        _pendingChallengeBoards = mode.boards;

        // Populate the panel UI.
        if (modeInfoTitleText != null)
            modeInfoTitleText.text = mode.displayName;

        if (modeInfoDescriptionText != null)
            modeInfoDescriptionText.text = string.IsNullOrWhiteSpace(mode.description) ? "" : mode.description;

        if (modeInfoWinConditionText != null)
        {
            if (string.IsNullOrWhiteSpace(mode.winConditionDescription))
            {
                // Generate a default win condition from the board definitions if not specified.
                modeInfoWinConditionText.text = GenerateDefaultWinCondition(mode.boards);
            }
            else
            {
                modeInfoWinConditionText.text = mode.winConditionDescription;
            }
        }

        // Show as overlay on top of current panel.
        if (modeInfoPanel != null)
            modeInfoPanel.SetActive(true);
    }

    /// <summary>
    /// Shows the mode info panel for a single board (used for fallback challenge boards).
    /// </summary>
    public void ShowModeInfoPanelForBoard(BoardDefinition board)
    {
        if (board == null) return;

        _pendingChallengeMode = null; // No challenge mode for single board
        _pendingChallengeBoards = new[] { board };

        if (modeInfoTitleText != null)
            modeInfoTitleText.text = board.displayName;

        if (modeInfoDescriptionText != null)
            modeInfoDescriptionText.text = "";

        if (modeInfoWinConditionText != null)
            modeInfoWinConditionText.text = GenerateDefaultWinCondition(new[] { board });

        // Show as overlay on top of current panel.
        if (modeInfoPanel != null)
            modeInfoPanel.SetActive(true);
    }

    private string GenerateDefaultWinCondition(BoardDefinition[] boards)
    {
        if (boards == null || boards.Length == 0)
            return "Complete the challenge!";

        // If there's a single board, show its specific condition.
        if (boards.Length == 1)
        {
            var board = boards[0];
            return GetBoardClearConditionText(board);
        }

        // Multiple boards - summarize.
        return $"Complete all {boards.Length} boards to win!";
    }

    private string GetBoardClearConditionText(BoardDefinition board)
    {
        if (board == null) return "Complete the board!";

        switch (board.clearCondition)
        {
            case BoardDefinition.ClearConditionKind.RoundIndexAtLeast:
                return $"Reach round {board.targetRoundIndex}";
            case BoardDefinition.ClearConditionKind.CoinsAtLeast:
                return $"Collect {board.targetCoins} coins";
            case BoardDefinition.ClearConditionKind.RoundTotalAtLeast:
                return $"Score {board.targetRoundTotal:N0} points in a round";
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

        // Main menu
        if (mainMenuPanel != null) mainMenuPanel.SetActive(panel == MenuPanel.MainMenu);

        // Sub-panels
        if (runSelectorPanel != null) runSelectorPanel.SetActive(panel == MenuPanel.RunSelector);
        if (settingsPanel != null) settingsPanel.SetActive(panel == MenuPanel.Settings);
        if (profilePanel != null) profilePanel.SetActive(panel == MenuPanel.Profile);
        if (collectionPanel != null) collectionPanel.SetActive(panel == MenuPanel.Collection);

        // Close the mode info overlay when switching panels.
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
        // Prefer an inactive, fullscreen-ish RectTransform (a "panel") over a small active button with the same name.
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

#if UNITY_2022_2_OR_NEWER
        all = Object.FindObjectsByType<Transform>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
        // Resources.FindObjectsOfTypeAll includes inactive, but also includes assets/prefabs.
        all = Resources.FindObjectsOfTypeAll<Transform>();
#endif

        var result = new List<Transform>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded) continue;
#if !UNITY_2022_2_OR_NEWER
            // Filter out non-scene objects when using Resources.FindObjectsOfTypeAll.
            if ((t.hideFlags & HideFlags.NotEditable) != 0) continue;
            if ((t.hideFlags & HideFlags.HideAndDontSave) != 0) continue;
#endif
            if (!includeInactive && !t.gameObject.activeInHierarchy) continue;

            result.Add(t);
        }

        return result;
    }
}
