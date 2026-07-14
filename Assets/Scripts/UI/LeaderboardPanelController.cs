using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LeaderboardPanelController : MonoBehaviour
{
    private const string overlayCanvasName = "LeaderboardOverlayCanvas";
    private const string rootObjectName = "LeaderboardPanel";
    private const string boardDefinitionsResourcePath = "BoardDefinitions";
    private const int maxRowsShown = 10;
    private const int sortingOrder = 9999;
    private const float referenceWidth = 1920f;
    private const float referenceHeight = 1080f;

    private const float listPanelWidth = 720f;
    private const float listPanelHeight = 780f;

    private const float titleFontSize = 36f;
    private const float scoreFontSize = 42f;
    private const float labelFontSize = 22f;
    private const float buttonFontSize = 28f;
    private const float rowFontSize = 22f;

    private const float buttonHeight = 56f;
    private const float rowHeight = 36f;
    private const float scrollViewHeight = 360f;

    private float _timeScaleBefore = 1f;
    private float _fixedDeltaBefore = 0.02f;
    private bool _pausedByThis;
    private CursorLockMode _cursorLockBefore;
    private bool _cursorVisibleBefore;

    private readonly Dictionary<Behaviour, bool> _disabledInputBehaviours =
        new Dictionary<Behaviour, bool>();

    private long _score;
    private string _boardName;
    private bool _wasWin;
    private bool _isReadOnly;
    private Action _onContinue;

    private enum ViewMode
    {
        Global,
        Friends,
        Local,
    }

    private BoardDefinition[] _boards = Array.Empty<BoardDefinition>();
    private int _boardIndex;
    private ViewMode _viewMode;
    private int _fetchToken;
    private int _localRank = -1;
    private bool _clearArmed;

    private List<SteamLeaderboardEntry> _entries = new List<SteamLeaderboardEntry>();

    private GameObject _listPanel;
    private GameObject _rowsRoot;
    private ScrollRect _scrollRect;
    private TextMeshProUGUI _headerLabel;
    private TextMeshProUGUI _boardLabel;
    private TextMeshProUGUI _statusLabel;
    private TextMeshProUGUI _modeButtonLabel;
    private TextMeshProUGUI _topLabel;
    private GameObject _clearButton;
    private TextMeshProUGUI _clearButtonLabel;

    public static void ShowReadOnly(Action onClose = null)
    {
        LeaderboardPanelController ctrl = CreateRoot();
        ctrl._isReadOnly = true;
        ctrl._onContinue = onClose;
        ctrl._boards = Resources.LoadAll<BoardDefinition>(boardDefinitionsResourcePath);

        ctrl.PauseAndLockInput();
        ctrl.BuildListPanel();
        ctrl.FetchScores();
    }

    public static void Show(long score, int levelReached, string boardName,
        bool wasWin, Action onContinue)
    {
        LeaderboardPanelController ctrl = CreateRoot();
        ctrl._score = Math.Max(0L, score);
        ctrl._boardName = boardName ?? "";
        ctrl._wasWin = wasWin;
        ctrl._onContinue = onContinue;

        ctrl.PauseAndLockInput();
        ctrl.BuildListPanel();
        ctrl.FetchScores();
    }

    private static LeaderboardPanelController CreateRoot()
    {
        Canvas canvas = EnsureOverlayCanvas();

        var rootGo = new GameObject(rootObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LeaderboardPanelController));

        rootGo.transform.SetParent(canvas.transform, false);

        var rt = rootGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = rootGo.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        return rootGo.GetComponent<LeaderboardPanelController>();
    }

    private static Canvas EnsureOverlayCanvas()
    {
        GameObject existing = GameObject.Find(overlayCanvasName);
        if (existing != null)
        {
            return existing.GetComponent<Canvas>();
        }

        var go = new GameObject(overlayCanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(referenceWidth, referenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        return c;
    }

    private void OnEnable()
    {
        SteamLeaderboards.PlayerNamesUpdated += OnPlayerNamesUpdated;
        SteamLeaderboards.ScoreUploaded += OnScoreUploaded;
    }

    private void OnDisable()
    {
        SteamLeaderboards.PlayerNamesUpdated -= OnPlayerNamesUpdated;
        SteamLeaderboards.ScoreUploaded -= OnScoreUploaded;
    }

    private void OnScoreUploaded()
    {
        // The panel usually opens while the run's upload is still in flight;
        // refetch once Steam confirms so the player's fresh entry shows up.
        FetchScores();
    }

    private string CurrentBoardSceneName
    {
        get
        {
            if (!_isReadOnly) return _boardName;
            if (_boards.Length == 0) return "";

            return _boards[_boardIndex].boardSceneName;
        }
    }

    private void FetchScores()
    {
        _entries.Clear();
        _localRank = -1;
        RebuildRows();

        // Local scores come from PlayerPrefs, so this view works even
        // when Steam is unavailable (shared-machine playtests).
        if (_viewMode == ViewMode.Local)
        {
            FetchLocalScores();
            return;
        }

        if (!SteamLeaderboards.IsAvailable)
        {
            SetStatus(LocalizedUI.Get("gameplay.leaderboard.steamUnavailable", "(Steam unavailable)"));
            return;
        }

        string board = CurrentBoardSceneName;
        if (string.IsNullOrEmpty(board))
        {
            SetStatus(LocalizedUI.Get("gameplay.leaderboard.noEntries", "(no entries yet)"));
            return;
        }

        SetStatus(LocalizedUI.Get("gameplay.leaderboard.fetching", "(fetching scores...)"));

        _fetchToken++;
        int token = _fetchToken;

        Action<List<SteamLeaderboardEntry>> onScores = entries =>
        {
            if (this == null || token != _fetchToken) return;

            _entries = entries;
            SetStatus(_entries.Count == 0
                ? LocalizedUI.Get("gameplay.leaderboard.noEntries", "(no entries yet)")
                : "");
            RebuildRows();
        };

        if (_viewMode == ViewMode.Friends)
        {
            SteamLeaderboards.DownloadFriendScores(board, onScores);
        }
        else
        {
            SteamLeaderboards.DownloadGlobalScores(board, 1, maxRowsShown, onScores);
        }

        if (!_isReadOnly)
        {
            SteamLeaderboards.DownloadScoresAroundUser(board, 0, entries =>
            {
                if (this == null || token != _fetchToken) return;

                foreach (SteamLeaderboardEntry entry in entries)
                {
                    if (!entry.isLocalUser) continue;

                    _localRank = entry.rank;
                    RefreshHeader();
                    break;
                }
            });
        }
    }

    private void FetchLocalScores()
    {
        string board = CurrentBoardSceneName;
        if (string.IsNullOrEmpty(board))
        {
            SetStatus(LocalizedUI.Get("gameplay.leaderboard.noEntries", "(no entries yet)"));
            return;
        }

        List<LocalLeaderboards.Entry> saved = LocalLeaderboards.GetEntries(board);
        for (int i = 0; i < saved.Count; i++)
        {
            DateTime when = new DateTime(saved[i].ticksUtc, DateTimeKind.Utc)
                .ToLocalTime();

            _entries.Add(new SteamLeaderboardEntry
            {
                rank = i + 1,
                playerName = when.ToString("MM/dd HH:mm", CultureInfo.InvariantCulture),
                score = (int)Math.Min(saved[i].score, int.MaxValue),
                levelReached = saved[i].level,
            });
        }

        SetStatus(_entries.Count == 0
            ? LocalizedUI.Get("gameplay.leaderboard.noEntries", "(no entries yet)")
            : "");
        RebuildRows();
    }

    private void OnPlayerNamesUpdated()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            string resolved = SteamLeaderboards.GetPlayerName(_entries[i].steamId);
            if (string.IsNullOrEmpty(resolved)) continue;

            SteamLeaderboardEntry entry = _entries[i];
            entry.playerName = resolved;
            _entries[i] = entry;
        }

        RebuildRows();
    }

    private void BuildListPanel()
    {
        _listPanel = new GameObject("ListPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup));

        _listPanel.transform.SetParent(transform, false);

        var rt = _listPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(listPanelWidth, listPanelHeight);

        var bg = _listPanel.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

        var vlg = _listPanel.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(24, 24, 24, 24);

        _headerLabel = BuildLabel(_listPanel.transform, "",
            titleFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        RefreshHeader();

        if (!_isReadOnly)
        {
            BuildLabel(_listPanel.transform,
                LocalizedUI.Format("gameplay.leaderboard.score", "Score: {0}", FormatScore(_score)),
                scoreFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

            string boardDisplayName = ResolveBoardDisplayName(_boardName);
            if (!string.IsNullOrEmpty(boardDisplayName))
            {
                BuildLabel(_listPanel.transform, boardDisplayName,
                    labelFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            }
        }

        if (_isReadOnly && _boards.Length > 0)
        {
            BuildBoardSwitcher(_listPanel.transform);
        }

        BuildModeButton(_listPanel.transform);
        BuildClearButton(_listPanel.transform);

        _topLabel = BuildLabel(_listPanel.transform, "",
            labelFontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        RefreshTopLabel();

        var scrollGo = new GameObject("ScrollView",
            typeof(RectTransform),
            typeof(RectMask2D),
            typeof(ScrollRect),
            typeof(LayoutElement));

        scrollGo.transform.SetParent(_listPanel.transform, false);

        var scrollLe = scrollGo.GetComponent<LayoutElement>();
        scrollLe.preferredHeight = scrollViewHeight;

        _rowsRoot = new GameObject("Rows",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));

        _rowsRoot.transform.SetParent(scrollGo.transform, false);

        var rowsRt = _rowsRoot.GetComponent<RectTransform>();
        rowsRt.anchorMin = new Vector2(0f, 1f);
        rowsRt.anchorMax = new Vector2(1f, 1f);
        rowsRt.pivot = new Vector2(0.5f, 1f);
        rowsRt.offsetMin = Vector2.zero;
        rowsRt.offsetMax = Vector2.zero;

        var rowsVlg = _rowsRoot.GetComponent<VerticalLayoutGroup>();
        rowsVlg.childAlignment = TextAnchor.UpperCenter;
        rowsVlg.childControlWidth = true;
        rowsVlg.childControlHeight = true;
        rowsVlg.childForceExpandWidth = true;
        rowsVlg.childForceExpandHeight = false;
        rowsVlg.spacing = 6f;

        var rowsFitter = _rowsRoot.GetComponent<ContentSizeFitter>();
        rowsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scrollRect = scrollGo.GetComponent<ScrollRect>();
        _scrollRect.content = rowsRt;
        _scrollRect.viewport = scrollGo.GetComponent<RectTransform>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 24f;

        _statusLabel = BuildLabel(_listPanel.transform, "", labelFontSize,
            FontStyles.Italic, TextAlignmentOptions.Center);

        string buttonLabel = _isReadOnly
            ? LocalizedUI.Get("gameplay.leaderboard.back", "BACK")
            : LocalizedUI.Get("gameplay.leaderboard.continue", "CONTINUE");
        BuildButton(_listPanel.transform, "ContinueButton", buttonLabel, OnContinue);
    }

    private void RefreshHeader()
    {
        if (_headerLabel == null) return;

        if (_isReadOnly)
        {
            _headerLabel.text = LocalizedUI.Get("gameplay.leaderboard.title", "LEADERBOARD");
            return;
        }

        string runText = _wasWin
            ? LocalizedUI.Get("gameplay.leaderboard.runComplete", "RUN COMPLETE")
            : LocalizedUI.Get("gameplay.leaderboard.runEnded", "RUN ENDED");

        _headerLabel.text = _localRank > 0
            ? runText + " -- " + LocalizedUI.Format(
                "gameplay.leaderboard.yourRank", "YOUR RANK: #{0}", _localRank)
            : runText;
    }

    private void BuildBoardSwitcher(Transform parent)
    {
        var row = new GameObject("BoardSwitcher",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));

        row.transform.SetParent(parent, false);

        var le = row.GetComponent<LayoutElement>();
        le.preferredHeight = buttonHeight;

        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 12f;

        BuildSmallButton(row.transform, "PrevBoard", "<", () => CycleBoard(-1));

        var labelGo = new GameObject("BoardLabel",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        labelGo.transform.SetParent(row.transform, false);

        var labelLe = labelGo.GetComponent<LayoutElement>();
        labelLe.preferredWidth = 360f;
        labelLe.preferredHeight = buttonHeight;

        _boardLabel = labelGo.GetComponent<TextMeshProUGUI>();
        _boardLabel.fontSize = buttonFontSize;
        _boardLabel.fontStyle = FontStyles.Bold;
        _boardLabel.color = Color.white;
        _boardLabel.alignment = TextAlignmentOptions.Center;
        _boardLabel.enableWordWrapping = false;
        RefreshBoardLabel();

        BuildSmallButton(row.transform, "NextBoard", ">", () => CycleBoard(1));
    }

    private void CycleBoard(int direction)
    {
        if (_boards.Length == 0) return;

        _boardIndex = (_boardIndex + direction + _boards.Length) % _boards.Length;
        RefreshBoardLabel();
        FetchScores();
    }

    private void RefreshBoardLabel()
    {
        if (_boardLabel == null || _boards.Length == 0) return;

        _boardLabel.text = _boards[_boardIndex].displayName;
    }

    private static string ResolveBoardDisplayName(string boardSceneName)
    {
        if (string.IsNullOrEmpty(boardSceneName)) return "";

        BoardDefinition[] boards = Resources.LoadAll<BoardDefinition>(boardDefinitionsResourcePath);
        foreach (BoardDefinition board in boards)
        {
            if (board.boardSceneName == boardSceneName) return board.displayName;
        }

        return boardSceneName;
    }

    private void BuildModeButton(Transform parent)
    {
        GameObject go = BuildButton(parent, "ModeButton", ModeButtonText(), () =>
        {
            _viewMode = (ViewMode)(((int)_viewMode + 1) % 3);
            if (_modeButtonLabel != null) _modeButtonLabel.text = ModeButtonText();
            RefreshClearButton();
            RefreshTopLabel();
            FetchScores();
        });

        _modeButtonLabel = go.GetComponentInChildren<TextMeshProUGUI>();
    }

    private string ModeButtonText()
    {
        switch (_viewMode)
        {
            case ViewMode.Friends:
                return LocalizedUI.Get("gameplay.leaderboard.viewFriends", "VIEW: FRIENDS");
            case ViewMode.Local:
                return LocalizedUI.Get("gameplay.leaderboard.viewLocal", "VIEW: LOCAL");
            default:
                return LocalizedUI.Get("gameplay.leaderboard.viewGlobal", "VIEW: GLOBAL");
        }
    }

    private void BuildClearButton(Transform parent)
    {
        _clearButton = BuildButton(parent, "ClearButton", "", OnClearClicked);
        _clearButton.GetComponent<Image>().color = new Color(0.8f, 0.25f, 0.25f, 1f);
        _clearButtonLabel = _clearButton.GetComponentInChildren<TextMeshProUGUI>();
        RefreshClearButton();
    }

    // Two clicks to clear so a stray tap can't wipe a playtest session.
    private void OnClearClicked()
    {
        if (_viewMode != ViewMode.Local) return;

        if (!_clearArmed)
        {
            _clearArmed = true;
            if (_clearButtonLabel != null) _clearButtonLabel.text = ClearButtonText();
            return;
        }

        LocalLeaderboards.ClearAll();
        _clearArmed = false;
        if (_clearButtonLabel != null) _clearButtonLabel.text = ClearButtonText();
        FetchScores();
    }

    private string ClearButtonText()
    {
        return _clearArmed
            ? LocalizedUI.Get("gameplay.leaderboard.clearConfirm",
                "REALLY CLEAR? (ALL BOARDS)")
            : LocalizedUI.Get("gameplay.leaderboard.clear", "CLEAR LOCAL SCORES");
    }

    private void RefreshClearButton()
    {
        if (_clearButton == null) return;

        _clearArmed = false;
        if (_clearButtonLabel != null) _clearButtonLabel.text = ClearButtonText();
        _clearButton.SetActive(_viewMode == ViewMode.Local);
    }

    private void RefreshTopLabel()
    {
        if (_topLabel == null) return;

        _topLabel.text = _viewMode == ViewMode.Local
            ? LocalizedUI.Get("gameplay.leaderboard.allRuns", "ALL RUNS")
            : LocalizedUI.Format("gameplay.leaderboard.top", "TOP {0}", maxRowsShown);
    }

    private void SetStatus(string text)
    {
        if (_statusLabel != null) _statusLabel.text = text ?? "";
    }

    private void RebuildRows()
    {
        if (_rowsRoot == null) return;

        for (int i = _rowsRoot.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(_rowsRoot.transform.GetChild(i).gameObject);
        }

        // Local mode lists every recorded run and relies on the scroll
        // view; Steam modes keep the fixed top-N cut.
        int shown = _viewMode == ViewMode.Local
            ? _entries.Count
            : Mathf.Min(_entries.Count, maxRowsShown);
        for (int i = 0; i < shown; i++)
        {
            BuildRow(_rowsRoot.transform, _entries[i]);
        }

        if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void BuildRow(Transform parent, SteamLeaderboardEntry entry)
    {
        bool highlight = entry.isLocalUser;

        var row = new GameObject("Row" + entry.rank,
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));

        row.transform.SetParent(parent, false);

        var le = row.GetComponent<LayoutElement>();
        le.preferredHeight = rowHeight;

        var bg = row.GetComponent<Image>();
        bg.color = highlight
            ? new Color(1f, 0.85f, 0.1f, 0.35f)
            : new Color(1f, 1f, 1f, 0.05f);

        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.spacing = 12f;
        hlg.padding = new RectOffset(12, 12, 4, 4);

        BuildRowCell(row.transform, "#" + entry.rank, 70f,
            TextAlignmentOptions.Left, highlight);
        BuildRowCell(row.transform, entry.playerName, 280f,
            TextAlignmentOptions.Left, highlight);
        BuildRowCell(row.transform, FormatScore(entry.score), 200f,
            TextAlignmentOptions.Right, highlight);
        BuildRowCell(row.transform, LocalizedUI.Format(
                "gameplay.leaderboard.levelShort", "Lv {0}", entry.levelReached), 80f,
            TextAlignmentOptions.Right, highlight);
    }

    private void BuildRowCell(Transform parent, string text, float width,
        TextAlignmentOptions align, bool highlight)
    {
        var go = new GameObject("Cell",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));

        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = 28f;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text ?? "";
        tmp.fontSize = rowFontSize;
        tmp.color = highlight ? new Color(1f, 0.95f, 0.6f, 1f) : Color.white;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.fontStyle = highlight ? FontStyles.Bold : FontStyles.Normal;
    }

    private GameObject BuildButton(Transform parent, string objectName, string text,
        Action onClick)
    {
        var go = new GameObject(objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));

        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = buttonHeight;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.5f, 0.95f, 1f);

        var labelGo = new GameObject("Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);

        var lRt = labelGo.GetComponent<RectTransform>();
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        lRt.offsetMin = Vector2.zero;
        lRt.offsetMax = Vector2.zero;

        var lTmp = labelGo.GetComponent<TextMeshProUGUI>();
        lTmp.text = text;
        lTmp.fontSize = buttonFontSize;
        lTmp.fontStyle = FontStyles.Bold;
        lTmp.color = Color.white;
        lTmp.alignment = TextAlignmentOptions.Center;
        lTmp.enableWordWrapping = false;

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        return go;
    }

    private void BuildSmallButton(Transform parent, string objectName, string text,
        Action onClick)
    {
        GameObject go = BuildButton(parent, objectName, text, onClick);

        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = buttonHeight;
        le.preferredHeight = buttonHeight;
    }

    private TextMeshProUGUI BuildLabel(Transform parent, string text, float fontSize,
        FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject("Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));

        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = fontSize + 10f;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text ?? "";
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;

        return tmp;
    }

    private void OnContinue()
    {
        Action cb = _onContinue;
        _onContinue = null;

        RestorePauseAndInput();
        Destroy(gameObject);

        cb?.Invoke();
    }

    private void OnDestroy()
    {
        RestorePauseAndInput();
    }

    private void PauseAndLockInput()
    {
        if (_pausedByThis) return;

        _timeScaleBefore = Time.timeScale;
        _fixedDeltaBefore = Time.fixedDeltaTime;
        _cursorLockBefore = Cursor.lockState;
        _cursorVisibleBefore = Cursor.visible;

        Time.timeScale = 0f;
        _pausedByThis = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        DisableGameplayInput();
    }

    private void RestorePauseAndInput()
    {
        if (!_pausedByThis) return;

        float restored = Mathf.Max(0f, _timeScaleBefore);
        if (restored <= 0f) restored = 1f;

        Time.timeScale = restored;
        Time.fixedDeltaTime = Mathf.Max(0.0001f, _fixedDeltaBefore);

        Cursor.lockState = _cursorLockBefore;
        Cursor.visible = _cursorVisibleBefore;

        RestoreGameplayInput();
        _pausedByThis = false;
    }

    private void DisableGameplayInput()
    {
        DisableIfEnabledByTypeName("PinballLauncher");
        DisableIfEnabledByTypeName("PinballFlipper");
    }

    private void DisableIfEnabledByTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return;

        Behaviour[] all = FindObjectsByType<Behaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            Behaviour b = all[i];
            if (b == null) continue;
            if (!b.enabled) continue;
            if (!string.Equals(b.GetType().Name, typeName, StringComparison.Ordinal)) continue;

            if (!_disabledInputBehaviours.ContainsKey(b))
            {
                _disabledInputBehaviours.Add(b, true);
            }

            b.enabled = false;
        }
    }

    private void RestoreGameplayInput()
    {
        foreach (KeyValuePair<Behaviour, bool> kv in _disabledInputBehaviours)
        {
            if (kv.Key != null)
            {
                kv.Key.enabled = kv.Value;
            }
        }

        _disabledInputBehaviours.Clear();
    }

    private static string FormatScore(long value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
