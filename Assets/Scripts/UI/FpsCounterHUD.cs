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
    private const float AnchorPaddingX = 12f;
    private const float AnchorPaddingY = -15f; // moved down by 3 (was -12)
    private const float BaseFontSize = 24f;
    private const float FontScale = 0.65f; // 35% smaller
    private const string PreferredFontName = "Early GameBoy";

    [Header("UI")]
    [SerializeField] private TMP_Text label;

    [Header("Behavior")]
    [Tooltip("How often the label updates, in seconds.")]
    [Min(0.05f)]
    [SerializeField] private float updateInterval = 0.25f;

    [Tooltip("If true, shows both FPS and frame time in ms.")]
    [SerializeField] private bool showMs = true;

    private int _frames;
    private float _timeAccum;
    private float _timeLeft;

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

        float dt = Mathf.Max(0.000001f, Time.unscaledDeltaTime);

        _frames++;
        _timeAccum += dt;
        _timeLeft -= dt;

        if (_timeLeft > 0f)
            return;

        float fps = _frames / Mathf.Max(0.000001f, _timeAccum);
        if (showMs)
        {
            float ms = 1000f / Mathf.Max(0.000001f, fps);
            label.text = $"FPS: {fps:0}  ({ms:0.0}ms)";
        }
        else
        {
            label.text = $"FPS: {fps:0}";
        }

        _frames = 0;
        _timeAccum = 0f;
        _timeLeft = Mathf.Max(0.05f, updateInterval);
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
        rootRt.anchorMin = new Vector2(0f, 1f);
        rootRt.anchorMax = new Vector2(0f, 1f);
        rootRt.pivot = new Vector2(0f, 1f);
        rootRt.anchoredPosition = new Vector2(AnchorPaddingX, AnchorPaddingY);
        rootRt.sizeDelta = new Vector2(320f, 60f);

        if (!label)
        {
            Transform existing = transform.Find("FPS Text");
            var textGo = existing
                ? existing.gameObject
                : new GameObject("FPS Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));

            textGo.transform.SetParent(transform, worldPositionStays: false);

            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.fontSize = Mathf.Ceil(BaseFontSize * FontScale);
            tmp.color = Color.white;
            tmp.text = "FPS: --";
            ApplyPreferredFont(tmp);

            label = tmp;
        }
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

