// Updated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-23.
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Simple FPS counter overlay that auto-spawns in the GameplayCore scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class FpsCounterHUD : MonoBehaviour
{
    internal const string RootObjectName = "__FPSCounterHUD";
    // Top-right inset (anchoredPosition is relative to the anchor/pivot).
    private const float AnchorPaddingX = -12f;
    private const float AnchorPaddingY = -15f;
    private const float BaseFontSize = 24f;
    private const float FontScale = 0.65f; // 35% smaller
    private const string PreferredFontName = "Early GameBoy";
    private const string FpsTextObjectName = "FPS Text";
    private const string SpeedTextObjectName = "Speed Text";

    [Header("UI")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text speedLabel;

    [Header("Behavior")]
    [Tooltip("How often the label updates, in seconds.")]
    [Min(0.05f)]
    [SerializeField] private float updateInterval = 0.25f;

    [Tooltip("Format uses Time.timeScale (actual effective game speed).")]
    [SerializeField] private string speedFormat = "x{0:0.00}";

    // Intentionally kept for backward compatibility with existing serialized data.
    // FPS display is now always a compact "fps: 000" format.
    [SerializeField, HideInInspector] private bool showMs = false;

    private int _frames;
    private float _timeAccum;
    private float _timeLeft;
    private float _lastSpeedScale = -1f;

    private void Awake()
    {
        _timeLeft = Mathf.Max(0.05f, updateInterval);
    }

    private void Start()
    {
        EnsureInstalledUI();
    }

    private void Update()
    {
        if (!label)
            return;

        UpdateSpeedLabel();

        float dt = Mathf.Max(0.000001f, Time.unscaledDeltaTime);

        _frames++;
        _timeAccum += dt;
        _timeLeft -= dt;

        if (_timeLeft > 0f)
            return;

        float fps = _frames / Mathf.Max(0.000001f, _timeAccum);
        int fpsRounded = Mathf.Clamp(Mathf.RoundToInt(fps), 0, 999);
        label.text = $"fps: {fpsRounded:000}";

        _frames = 0;
        _timeAccum = 0f;
        _timeLeft = Mathf.Max(0.05f, updateInterval);
    }

    private void UpdateSpeedLabel()
    {
        if (!speedLabel)
            return;

        float scale = Time.timeScale;
        if (Mathf.Abs(scale - _lastSpeedScale) <= 0.0005f)
            return;

        _lastSpeedScale = scale;
        speedLabel.text = string.Format(GetSpeedFormatOrDefault(), scale);
    }

    private void EnsureInstalledUI()
    {
        // Ensure we're parented under a canvas in THIS scene.
        Canvas canvas = FindCanvasInThisScene();
        if (!canvas)
        {
            // Extremely defensive fallback: create a small overlay canvas if the scene has none.
            var canvasGo = new GameObject("FPS Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, gameObject.scene);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        if (transform.parent != canvas.transform)
            transform.SetParent(canvas.transform, worldPositionStays: false);

        transform.SetAsLastSibling(); // keep on top

        var rootRt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(1f, 1f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(1f, 1f);
        rootRt.anchoredPosition = new Vector2(AnchorPaddingX, AnchorPaddingY);
        rootRt.sizeDelta = new Vector2(200f, 56f);

        if (!label)
        {
            Transform existing = transform.Find(FpsTextObjectName);
            var textGo = existing
                ? existing.gameObject
                : new GameObject(FpsTextObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));

            textGo.transform.SetParent(transform, worldPositionStays: false);

            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = new Vector2(0f, 0.5f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(0f, 0f);
            textRt.offsetMax = new Vector2(0f, 0f);

            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.TopRight;
            tmp.fontSize = Mathf.Ceil(BaseFontSize * FontScale);
            tmp.color = Color.white;
            tmp.text = "fps: 000";
            ApplyPreferredFont(tmp);

            label = tmp;
        }
        ConfigureFpsText(label);

        if (!speedLabel)
        {
            Transform existing = transform.Find(SpeedTextObjectName);
            var textGo = existing
                ? existing.gameObject
                : new GameObject(SpeedTextObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));

            textGo.transform.SetParent(transform, worldPositionStays: false);

            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 0.5f);
            textRt.offsetMin = new Vector2(0f, 0f);
            textRt.offsetMax = new Vector2(0f, 0f);

            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.BottomRight;
            tmp.fontSize = Mathf.Ceil(BaseFontSize * FontScale);
            tmp.color = Color.white;
            tmp.text = string.Format(GetSpeedFormatOrDefault(), Time.timeScale);
            ApplyPreferredFont(tmp);

            speedLabel = tmp;
            _lastSpeedScale = -1f; // force refresh next Update()
        }
        ConfigureSpeedText(speedLabel);
    }

    private string GetSpeedFormatOrDefault()
    {
        return string.IsNullOrWhiteSpace(speedFormat) ? "x{0:0.00}" : speedFormat;
    }

    private static void ConfigureFpsText(TMP_Text t)
    {
        if (!t)
            return;

        RectTransform rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.alignment = TextAlignmentOptions.TopRight;
    }

    private static void ConfigureSpeedText(TMP_Text t)
    {
        if (!t)
            return;

        RectTransform rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.alignment = TextAlignmentOptions.BottomRight;
    }

    private static void ApplyPreferredFont(TextMeshProUGUI tmp)
    {
        if (!tmp) return;

        // Find the project's TMP font asset by name from loaded assets (GameplayCore already references it).
        TMP_FontAsset preferred = FindFontAssetByNameHint(PreferredFontName);
        if (preferred)
        {
            tmp.font = preferred;
            return;
        }

        if (TMP_Settings.defaultFontAsset)
            tmp.font = TMP_Settings.defaultFontAsset;
    }

    private static TMP_FontAsset FindFontAssetByNameHint(string nameHint)
    {
        if (string.IsNullOrWhiteSpace(nameHint))
            return null;

        string hint = NormalizeName(nameHint);
        var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < all.Length; i++)
        {
            var f = all[i];
            if (!f) continue;
            if (NormalizeName(f.name).Contains(hint))
                return f;
        }
        return null;
    }

    private static string NormalizeName(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = s.ToLowerInvariant();
        return s.Replace(" ", "").Replace("_", "").Replace("-", "");
    }

    private Canvas FindCanvasInThisScene()
    {
        var all = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c == null) continue;
            if (!c.gameObject.scene.IsValid()) continue;
            if (c.gameObject.scene != gameObject.scene) continue;
            if (!c.gameObject.activeInHierarchy) continue;
            return c;
        }
        return null;
    }
}

internal sealed class FpsCounterHUDBootstrapper : MonoBehaviour
{
    private const string GameplayCoreSceneName = "GameplayCore";

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstallIntoLoadedScenes();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstallIntoScene(scene);
    }

    private static void TryInstallIntoLoadedScenes()
    {
        int count = SceneManager.sceneCount;
        for (int i = 0; i < count; i++)
        {
            TryInstallIntoScene(SceneManager.GetSceneAt(i));
        }
    }

    private static void TryInstallIntoScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (!string.Equals(scene.name, GameplayCoreSceneName, System.StringComparison.OrdinalIgnoreCase))
            return;

        // Already installed in this scene?
        var existing = Object.FindObjectsByType<FpsCounterHUD>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].gameObject.scene == scene)
                return;
        }

        var go = new GameObject(FpsCounterHUD.RootObjectName, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<FpsCounterHUD>();
    }
}

internal static class FpsCounterHUDBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        // Avoid duplicates across domain reloads / scene reloads.
#if UNITY_2022_2_OR_NEWER
        if (Object.FindFirstObjectByType<FpsCounterHUDBootstrapper>() != null)
            return;
#else
        if (Object.FindObjectOfType<FpsCounterHUDBootstrapper>() != null)
            return;
#endif

        var go = new GameObject(nameof(FpsCounterHUDBootstrapper));
        Object.DontDestroyOnLoad(go);
        go.AddComponent<FpsCounterHUDBootstrapper>();
    }
}

