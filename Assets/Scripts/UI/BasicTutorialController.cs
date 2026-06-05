// Generated with Claude Code (Opus 4.7) for jjmil on 2026-05-14.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BasicTutorialController : MonoBehaviour
{
    // ====================================================================
    // PANEL CONTENT — edit these to change what each tutorial panel says.
    // Use \n for line breaks. Set the *Button text to "" to hide a button
    // (panel 2 has no button by design — it auto-closes when the player
    // hits the shop button).
    // ====================================================================

    // Localized lazily via the Gameplay table; the literal below each is the English fallback.
    private static string firstPlayTitle =>
        LocalizedUI.Get("tutorial.firstPlay.title", "CONTROLS");
    private static string firstPlayBody =>
        LocalizedUI.Get("tutorial.firstPlay.body",
            "Flippers:  Left Arrow / Right Arrow,  A / D,  or Left / Right Mouse Button.\n\n"
            + "Click START to begin.");
    private static string firstPlayButton =>
        LocalizedUI.Get("tutorial.firstPlay.button", "START");

    private static string levelUpTitle =>
        LocalizedUI.Get("tutorial.levelUp.title", "YOU LEVELED UP!");
    private static string levelUpBody =>
        LocalizedUI.Get("tutorial.levelUp.body",
            "Reaching the goal unlocks the SHOP for one visit.\n\n"
            + "The SHOP button below the board has lit up. Press it now to spend coins on new balls and "
            + "board upgrades before the next round.");

    private static string shopTitle =>
        LocalizedUI.Get("tutorial.shop.title", "WELCOME TO THE SHOP");
    private static string shopBody =>
        LocalizedUI.Get("tutorial.shop.body",
            "Drag offers from the shelf onto your hand or onto the board to buy them.\n\n"
            + "Drag a hand ball onto a shop hub to sell it back for coins.\n\n"
            + "Hit DONE inside the shop when you're ready to play the next round.");
    private static string shopButton =>
        LocalizedUI.Get("tutorial.shop.button", "GOT IT");

    // ====================================================================
    // LAYOUT / VISUAL CONSTANTS
    // ====================================================================

    private const string overlayCanvasName = "BasicTutorialOverlayCanvas";
    private const string firstPlayPanelName = "FirstPlayTutorialPanel";
    private const string levelUpPanelName = "LevelUpTutorialPanel";
    private const string shopPanelName = "ShopTutorialPanel";

    private const int sortingOrder = 9990;
    private const float referenceWidth = 1920f;
    private const float referenceHeight = 1080f;

    private const float panelWidth = 720f;
    private const float panelHeight = 560f;

    private const float titleFontSize = 40f;
    private const float bodyFontSize = 24f;
    private const float levelUpBodyFontSize = 32f;
    private const float buttonFontSize = 28f;
    private const float buttonHeight = 64f;

    private const float titlePreferredHeight = 64f;
    private const float bodyPreferredHeight = 280f;
    private const float panelVerticalSpacing = 36f;
    private const int panelPaddingTop = 36;
    private const int panelPaddingBottom = 36;
    private const int panelPaddingLeft = 36;
    private const int panelPaddingRight = 36;

    private const string preferredFontNameHint = "Manticore";

    private static BasicTutorialController _instance;
    private static TMP_FontAsset _cachedFont;
    private static bool _fontLookupAttempted;

    private GameRulesManager _cachedRules;

    private GameObject _firstPlayPanel;
    private GameObject _levelUpPanel;
    private GameObject _shopPanel;

    private float _timeScaleBefore = 1f;
    private float _fixedDeltaBefore = 0.02f;
    private bool _pausedByThis;
    private CursorLockMode _cursorLockBefore;
    private bool _cursorVisibleBefore;

    private readonly Dictionary<Behaviour, bool> _disabledInputBehaviours =
        new Dictionary<Behaviour, bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        var go = new GameObject(nameof(BasicTutorialController));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<BasicTutorialController>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeFromRules();

        if (_pausedByThis) RestorePauseAndInput();

        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        TrySubscribeToRules();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySubscribeToRules();
    }

    private void TrySubscribeToRules()
    {
        var rules = ServiceLocator.Get<GameRulesManager>();
        if (rules == _cachedRules) return;

        UnsubscribeFromRules();

        _cachedRules = rules;
        if (rules == null) return;

        rules.RoundStarted += OnRoundStarted;
        rules.ShopAvailabilityChanged += OnShopAvailabilityChanged;
        rules.ShopOpened += OnShopOpened;
    }

    private void UnsubscribeFromRules()
    {
        if (_cachedRules == null) return;

        _cachedRules.RoundStarted -= OnRoundStarted;
        _cachedRules.ShopAvailabilityChanged -= OnShopAvailabilityChanged;
        _cachedRules.ShopOpened -= OnShopOpened;
        _cachedRules = null;
    }

    private void OnRoundStarted()
    {
        if (_firstPlayPanel != null) return;
        if (ProfileService.HasSeenFirstPlayTutorial()) return;

        ShowFirstPlayPanel();
    }

    private void OnShopAvailabilityChanged(bool available)
    {
        if (!available) return;
        if (_levelUpPanel != null) return;
        if (ProfileService.HasSeenLevelUpTutorial()) return;

        ShowLevelUpPanel();
    }

    private void OnShopOpened()
    {
        if (_levelUpPanel != null)
        {
            ProfileService.RecordLevelUpTutorialSeen();
            DestroyLevelUpPanel();
            RestorePauseAndInput();
        }

        if (_shopPanel != null) return;
        if (ProfileService.HasSeenShopTutorial()) return;

        ShowShopPanel();
    }

    private void ShowFirstPlayPanel()
    {
        PauseAndLockInput();

        Canvas canvas = EnsureOverlayCanvas();
        _firstPlayPanel = BuildPanel(canvas.transform, firstPlayPanelName,
            firstPlayTitle, firstPlayBody, firstPlayButton, OnFirstPlayClosed);
    }

    private void OnFirstPlayClosed()
    {
        ProfileService.RecordFirstPlayTutorialSeen();

        if (_firstPlayPanel != null)
        {
            Destroy(_firstPlayPanel);
            _firstPlayPanel = null;
        }

        RestorePauseAndInput();
    }

    private void ShowLevelUpPanel()
    {
        PauseAndLockInput();

        Canvas canvas = EnsureOverlayCanvas();
        _levelUpPanel = BuildPanel(canvas.transform, levelUpPanelName,
            levelUpTitle, levelUpBody, null, null,
            bodyAlign: TextAlignmentOptions.Center,
            bodyFontSizeOverride: levelUpBodyFontSize,
            childAlignment: TextAnchor.MiddleCenter);
    }

    private void DestroyLevelUpPanel()
    {
        if (_levelUpPanel != null)
        {
            Destroy(_levelUpPanel);
            _levelUpPanel = null;
        }
    }

    private void ShowShopPanel()
    {
        Canvas canvas = EnsureOverlayCanvas();
        _shopPanel = BuildPanel(canvas.transform, shopPanelName,
            shopTitle, shopBody, shopButton, OnShopPanelClosed);
    }

    private void OnShopPanelClosed()
    {
        ProfileService.RecordShopTutorialSeen();

        if (_shopPanel != null)
        {
            Destroy(_shopPanel);
            _shopPanel = null;
        }
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

        DontDestroyOnLoad(go);

        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(referenceWidth, referenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        return c;
    }

    private GameObject BuildPanel(Transform parent, string objectName,
        string title, string body, string buttonLabel, Action onClose,
        TextAlignmentOptions bodyAlign = TextAlignmentOptions.TopLeft,
        float bodyFontSizeOverride = -1f,
        TextAnchor childAlignment = TextAnchor.UpperCenter)
    {
        var rootGo = new GameObject(objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        rootGo.transform.SetParent(parent, false);

        var rt = rootGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = rootGo.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = true;

        var panelGo = new GameObject("Panel",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup));

        panelGo.transform.SetParent(rootGo.transform, false);

        var prt = panelGo.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(panelWidth, panelHeight);

        var pbg = panelGo.GetComponent<Image>();
        pbg.color = new Color(0.08f, 0.08f, 0.12f, 0.97f);

        var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = childAlignment;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = panelVerticalSpacing;
        vlg.padding = new RectOffset(panelPaddingLeft, panelPaddingRight,
            panelPaddingTop, panelPaddingBottom);

        BuildLabel(panelGo.transform, title, titleFontSize,
            FontStyles.Bold, TextAlignmentOptions.Center, titlePreferredHeight);

        float resolvedBodyFontSize = bodyFontSizeOverride > 0f ? bodyFontSizeOverride : bodyFontSize;
        BuildLabel(panelGo.transform, body, resolvedBodyFontSize,
            FontStyles.Normal, bodyAlign, bodyPreferredHeight);

        if (!string.IsNullOrEmpty(buttonLabel) && onClose != null)
        {
            BuildButton(panelGo.transform, buttonLabel, onClose);
        }

        return rootGo;
    }

    private static void BuildLabel(Transform parent, string text, float fontSize,
        FontStyles style, TextAlignmentOptions align, float preferredHeight)
    {
        var go = new GameObject("Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));

        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text ?? "";
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        ApplyPreferredFont(tmp);
    }

    private static void BuildButton(Transform parent, string label, Action onClick)
    {
        var go = new GameObject("CloseButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));

        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = buttonHeight;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.55f, 0.95f, 1f);

        var labelGo = new GameObject("Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));

        labelGo.transform.SetParent(go.transform, false);

        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        var lTmp = labelGo.GetComponent<TextMeshProUGUI>();
        lTmp.text = label;
        lTmp.fontSize = buttonFontSize;
        lTmp.fontStyle = FontStyles.Bold;
        lTmp.color = Color.white;
        lTmp.alignment = TextAlignmentOptions.Center;
        lTmp.textWrappingMode = TextWrappingModes.NoWrap;
        ApplyPreferredFont(lTmp);

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        ServiceLocator.Get<AudioManager>()?.WireButtonAudio(btn);
    }

    private static void ApplyPreferredFont(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;

        TMP_FontAsset font = ResolvePreferredFont();
        if (font != null) tmp.font = font;
    }

    private static TMP_FontAsset ResolvePreferredFont()
    {
        if (_cachedFont != null) return _cachedFont;
        if (_fontLookupAttempted && _cachedFont == null) return null;

        _fontLookupAttempted = true;
        _cachedFont = FindFontAssetByNameHint(preferredFontNameHint);
        return _cachedFont;
    }

    private static TMP_FontAsset FindFontAssetByNameHint(string nameHint)
    {
        if (string.IsNullOrWhiteSpace(nameHint)) return null;

        string hint = NormalizeName(nameHint);
        TMP_FontAsset[] all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < all.Length; i++)
        {
            TMP_FontAsset f = all[i];
            if (f == null) continue;
            if (NormalizeName(f.name).Contains(hint)) return f;
        }

        return null;
    }

    private static string NormalizeName(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = s.ToLowerInvariant();
        return s.Replace(" ", "").Replace("_", "").Replace("-", "");
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
}
