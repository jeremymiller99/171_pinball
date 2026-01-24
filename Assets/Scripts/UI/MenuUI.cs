using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;


public class MainMenuUI : MonoBehaviour
{
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

    [Header("UI (optional - auto-wired if left blank)")]
    [Tooltip("If not set, the script will try to find the existing 'Play' button in the scene.")]
    [SerializeField] private Button startRunButton;

    [Tooltip("Parent transform to place challenge buttons under. If not set, one will be created under the first Canvas.")]
    [SerializeField] private RectTransform challengeButtonsRoot;

    [Tooltip("Button prefab/template used for challenge buttons. If not set, the Start Run button is cloned.")]
    [SerializeField] private Button challengeButtonTemplate;

    [Tooltip("If set, this label will be forced to 'Start Run' at runtime.")]
    [SerializeField] private TMP_Text startRunLabel;

    private bool _built;

    public void LoadGameScene()
    {
        // Backwards-compatible entry point for the existing MainMenu button wiring.
        StartQuickRun();
    }

    public void LoadMenuScene()
    {
        SceneManager.LoadScene("MainMenu");
    }

    private void Start()
    {
        BuildMenuIfNeeded();
    }

    private void BuildMenuIfNeeded()
    {
        if (_built) return;
        _built = true;

        AutoWireStartRunButton();
        AutoWireChallengeRootAndTemplate();
        BuildChallengeButtons();
    }

    private void AutoWireStartRunButton()
    {
        if (startRunButton == null)
        {
            var playGo = GameObject.Find("Play");
            if (playGo != null)
            {
                startRunButton = playGo.GetComponent<Button>();
            }
        }

        if (startRunButton != null)
        {
            // Ensure the Start Run button always starts a run (even if the scene has stale wiring).
            startRunButton.onClick = new Button.ButtonClickedEvent();
            startRunButton.onClick.AddListener(StartQuickRun);

            if (startRunLabel == null)
            {
                startRunLabel = startRunButton.GetComponentInChildren<TMP_Text>(true);
            }
            if (startRunLabel != null)
            {
                startRunLabel.text = "Start Run";
            }
        }
    }

    private void AutoWireChallengeRootAndTemplate()
    {
        if (challengeButtonsRoot != null) return;

        // Try to find an existing container by name first.
        var existing = GameObject.Find("Challenges");
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

                CreateChallengeButton(mode.displayName, () => StartChallengeBoards(boards));
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
                CreateChallengeButton(label, () => StartChallengeBoards(new[] { board }));
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
        GameSession.Instance.ConfigureChallenge(boards, seed);
        SceneManager.LoadScene(gameplayCoreSceneName);
    }
}
