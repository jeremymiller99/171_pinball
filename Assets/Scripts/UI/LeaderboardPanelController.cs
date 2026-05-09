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
    private const int maxRowsShown = 10;
    private const int maxNameLength = 16;
    private const int sortingOrder = 9999;
    private const float referenceWidth = 1920f;
    private const float referenceHeight = 1080f;

    private const float entryPanelWidth = 560f;
    private const float entryPanelHeight = 360f;
    private const float listPanelWidth = 720f;
    private const float listPanelHeight = 720f;

    private const float titleFontSize = 36f;
    private const float scoreFontSize = 42f;
    private const float labelFontSize = 22f;
    private const float buttonFontSize = 28f;
    private const float rowFontSize = 22f;

    private const float buttonHeight = 56f;
    private const float inputHeight = 50f;
    private const float rowHeight = 36f;

    private float _timeScaleBefore = 1f;
    private float _fixedDeltaBefore = 0.02f;
    private bool _pausedByThis;
    private CursorLockMode _cursorLockBefore;
    private bool _cursorVisibleBefore;

    private readonly Dictionary<Behaviour, bool> _disabledInputBehaviours =
        new Dictionary<Behaviour, bool>();

    private long _score;
    private int _levelReached;
    private string _boardName;
    private bool _wasWin;
    private Action _onContinue;

    private GameObject _entryPanel;
    private GameObject _listPanel;
    private TMP_InputField _nameInput;
    private LeaderboardEntry _justSubmitted;
    private int _submittedRank = -1;
    private bool _isReadOnly;

    public static void ShowReadOnly(Action onClose = null)
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

        var ctrl = rootGo.GetComponent<LeaderboardPanelController>();
        ctrl._score = 0L;
        ctrl._levelReached = 0;
        ctrl._boardName = "";
        ctrl._wasWin = false;
        ctrl._onContinue = onClose;
        ctrl._isReadOnly = true;

        ctrl.PauseAndLockInput();
        ctrl.BuildListPanel();
    }

    public static void Show(long score, int levelReached, string boardName,
        bool wasWin, Action onContinue)
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

        var ctrl = rootGo.GetComponent<LeaderboardPanelController>();
        ctrl._score = Math.Max(0L, score);
        ctrl._levelReached = Math.Max(0, levelReached);
        ctrl._boardName = boardName ?? "";
        ctrl._wasWin = wasWin;
        ctrl._onContinue = onContinue;

        ctrl.PauseAndLockInput();
        ctrl.BuildEntryPanel();
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

    private void BuildEntryPanel()
    {
        _entryPanel = new GameObject("EntryPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup));

        _entryPanel.transform.SetParent(transform, false);

        var rt = _entryPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(entryPanelWidth, entryPanelHeight);

        var bg = _entryPanel.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

        var vlg = _entryPanel.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 16f;
        vlg.padding = new RectOffset(24, 24, 24, 24);

        BuildLabel(_entryPanel.transform,
            _wasWin ? "RUN COMPLETE" : "RUN ENDED",
            titleFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

        BuildLabel(_entryPanel.transform,
            "Score: " + FormatScore(_score),
            scoreFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

        BuildLabel(_entryPanel.transform,
            "Enter your name:",
            labelFontSize, FontStyles.Normal, TextAlignmentOptions.Center);

        BuildNameInput(_entryPanel.transform);
        BuildSubmitButton(_entryPanel.transform);
    }

    private void BuildNameInput(Transform parent)
    {
        var go = new GameObject("NameInput",
            typeof(RectTransform),
            typeof(Image),
            typeof(TMP_InputField),
            typeof(LayoutElement));

        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = inputHeight;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.18f, 1f);

        var textArea = new GameObject("Text Area",
            typeof(RectTransform),
            typeof(RectMask2D));
        textArea.transform.SetParent(go.transform, false);

        var taRt = textArea.GetComponent<RectTransform>();
        taRt.anchorMin = Vector2.zero;
        taRt.anchorMax = Vector2.one;
        taRt.offsetMin = new Vector2(12f, 6f);
        taRt.offsetMax = new Vector2(-12f, -6f);

        var placeholder = new GameObject("Placeholder",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        placeholder.transform.SetParent(textArea.transform, false);

        var phRt = placeholder.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = Vector2.zero;
        phRt.offsetMax = Vector2.zero;

        var phTmp = placeholder.GetComponent<TextMeshProUGUI>();
        phTmp.text = "Name...";
        phTmp.fontSize = 28f;
        phTmp.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        phTmp.alignment = TextAlignmentOptions.Left;
        phTmp.fontStyle = FontStyles.Italic;
        phTmp.enableWordWrapping = false;

        var textGo = new GameObject("Text",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textGo.transform.SetParent(textArea.transform, false);

        var txtRt = textGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        var txtTmp = textGo.GetComponent<TextMeshProUGUI>();
        txtTmp.text = "";
        txtTmp.fontSize = 28f;
        txtTmp.color = Color.white;
        txtTmp.alignment = TextAlignmentOptions.Left;
        txtTmp.enableWordWrapping = false;

        var input = go.GetComponent<TMP_InputField>();
        input.textViewport = taRt;
        input.textComponent = txtTmp;
        input.placeholder = phTmp;
        input.characterLimit = maxNameLength;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.text = LocalLeaderboard.GetLastUsedName();
        input.onSubmit.AddListener(OnInputSubmit);

        _nameInput = input;
    }

    private void BuildSubmitButton(Transform parent)
    {
        var go = BuildButton(parent, "SubmitButton", "SUBMIT SCORE", OnSubmit);

        if (_nameInput != null)
        {
            _nameInput.Select();
            _nameInput.ActivateInputField();
        }
    }

    private void OnInputSubmit(string ignored)
    {
        OnSubmit();
    }

    private void OnSubmit()
    {
        string playerName = _nameInput != null ? _nameInput.text : "";
        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Anonymous";
        }

        playerName = playerName.Trim();

        _submittedRank = LocalLeaderboard.Submit(playerName, _score, _levelReached,
            _boardName, _wasWin);

        var top = LocalLeaderboard.GetTopEntries(maxRowsShown);
        for (int i = 0; i < top.Count; i++)
        {
            if (top[i].score == _score
                && string.Equals(top[i].playerName, playerName, StringComparison.Ordinal)
                && top[i].levelReached == _levelReached)
            {
                _justSubmitted = top[i];
                break;
            }
        }

        if (_entryPanel != null)
        {
            Destroy(_entryPanel);
            _entryPanel = null;
        }

        BuildListPanel();
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
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(24, 24, 24, 24);

        string headerText;
        if (_isReadOnly)
        {
            headerText = "LEADERBOARD";
        }
        else
        {
            headerText = _submittedRank > 0
                ? "YOUR RANK: #" + _submittedRank
                : "LEADERBOARD";
        }

        BuildLabel(_listPanel.transform, headerText,
            titleFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

        BuildLabel(_listPanel.transform, "TOP " + maxRowsShown,
            labelFontSize, FontStyles.Bold, TextAlignmentOptions.Center);

        var top = LocalLeaderboard.GetTopEntries(maxRowsShown);
        for (int i = 0; i < top.Count; i++)
        {
            BuildRow(_listPanel.transform, i + 1, top[i],
                ReferenceEquals(top[i], _justSubmitted));
        }

        if (top.Count == 0)
        {
            BuildLabel(_listPanel.transform, "(no entries yet)", labelFontSize,
                FontStyles.Italic, TextAlignmentOptions.Center);
        }

        string buttonLabel = _isReadOnly ? "BACK" : "CONTINUE";
        BuildButton(_listPanel.transform, "ContinueButton", buttonLabel, OnContinue);
    }

    private void BuildRow(Transform parent, int rank, LeaderboardEntry entry, bool highlight)
    {
        var row = new GameObject("Row" + rank,
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

        BuildRowCell(row.transform, "#" + rank, 70f,
            TextAlignmentOptions.Left, highlight);
        BuildRowCell(row.transform, entry.playerName, 280f,
            TextAlignmentOptions.Left, highlight);
        BuildRowCell(row.transform, FormatScore(entry.score), 200f,
            TextAlignmentOptions.Right, highlight);
        BuildRowCell(row.transform, "Lv " + entry.levelReached, 80f,
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

    private void BuildLabel(Transform parent, string text, float fontSize,
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
