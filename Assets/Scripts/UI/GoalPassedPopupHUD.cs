using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows a popup when the round goal is passed in multiples:
/// "Goal Passed xN! +10% game speed +50% score" (values pulled from ScoreManager per-tier settings).
///
/// This auto-installs itself into the GameplayCore scene at runtime (no manual scene wiring required).
/// </summary>
public sealed class GoalPassedPopupHUD : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Scene name that should own this popup HUD.")]
    [SerializeField] private string gameplayCoreSceneName = "GameplayCore";

    [Header("Auto-find / Auto-create")]
    [Tooltip("If a TMP object with this name exists in GameplayCore, it will be used. Otherwise one will be created.")]
    [SerializeField] private string popupTextObjectName = "GoalPassedPopupText";

    [Header("Font (optional override)")]
    [Tooltip("If set, forces this popup to use the specified TMP font asset. " +
             "Leave null to auto-adopt from existing UI text in the scene.")]
    [SerializeField] private TMP_FontAsset fontOverride;

    [Tooltip("If Font Override is null, tries to find a TMP font asset loaded whose name contains this string (case-insensitive).")]
    [SerializeField] private string fallbackFontNameContains = "Early GameBoy";

    [Header("Layout")]
    [SerializeField] private Vector2 anchor = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 size = new Vector2(900f, 140f);
    [SerializeField] private float fontSize = 15f;
    [SerializeField] private Color textColor = Color.white;

    [Header("Animation (unscaled time)")]
    [Min(0f)] [SerializeField] private float popInDuration = 0.15f;
    [Min(0f)] [SerializeField] private float holdDuration = 1.00f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.35f;
    [Min(0f)] [SerializeField] private float popScale = 1.08f;

    private ScoreManager _score;
    private TMP_Text _popupText;
    private CanvasGroup _canvasGroup;
    private Coroutine _routine;
    private int _lastShownTier;

    private void Awake()
    {
        // This component is expected to live in GameplayCore (we move it there in the bootstrapper).
        // If it somehow ends up elsewhere, it will simply no-op.
        if (!string.IsNullOrWhiteSpace(gameplayCoreSceneName) &&
            gameObject.scene.IsValid() &&
            !string.Equals(gameObject.scene.name, gameplayCoreSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            enabled = false;
            return;
        }

        ResolveScoreManager();
        EnsurePopupText();
        HideImmediate();
    }

    private void OnEnable()
    {
        ResolveScoreManager();
        if (_score != null)
            _score.GoalTierChanged += OnGoalTierChanged;
    }

    private void OnDisable()
    {
        if (_score != null)
            _score.GoalTierChanged -= OnGoalTierChanged;
    }

    private void ResolveScoreManager()
    {
        if (_score != null) return;

#if UNITY_2022_2_OR_NEWER
        _score = FindFirstObjectByType<ScoreManager>();
#else
        _score = FindObjectOfType<ScoreManager>();
#endif
    }

    private void OnGoalTierChanged(int tier)
    {
        // Only show when tier increases (not on resets or goal changes that might drop tier).
        if (tier <= 0 || tier <= _lastShownTier)
        {
            _lastShownTier = Mathf.Max(_lastShownTier, tier);
            return;
        }

        _lastShownTier = tier;

        EnsurePopupText();
        if (_popupText == null) return;
        EnsurePopupFont();
        ApplyPopupStyle();

        float speedPct = _score != null ? (_score.SpeedIncreasePerGoalTier * 100f) : 10f;
        float scorePct = _score != null ? (_score.ScoreIncreasePerGoalTier * 100f) : 50f;

        // Match the requested phrasing, but with the "Goal Passed xN!" on its own line.
        _popupText.text =
            $"Goal Passed x{tier}!\n" +
            $"+{speedPct:0}% game speed   +{scorePct:0}% score";

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        if (_popupText == null) yield break;

        var rt = _popupText.rectTransform;
        EnsureCanvasGroup();

        rt.localScale = Vector3.one * Mathf.Max(0.01f, popScale);
        _canvasGroup.alpha = 1f;

        // Pop-in
        float inDur = Mathf.Max(0f, popInDuration);
        if (inDur > 0f)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / inDur;
                float u = Mathf.Clamp01(t);
                rt.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, Smooth01(u));
                yield return null;
            }
        }
        rt.localScale = Vector3.one;

        // Hold
        float hold = Mathf.Max(0f, holdDuration);
        if (hold > 0f)
        {
            float elapsed = 0f;
            while (elapsed < hold)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // Fade out
        float outDur = Mathf.Max(0f, fadeOutDuration);
        if (outDur > 0f)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / outDur;
                float u = Mathf.Clamp01(t);
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, Smooth01(u));
                yield return null;
            }
        }

        HideImmediate();
        _routine = null;
    }

    private void EnsurePopupText()
    {
        if (_popupText != null && _popupText.gameObject.scene == gameObject.scene)
        {
            // Re-apply style in case the object existed already (or settings changed in inspector).
            EnsureCanvasGroup();
            EnsurePopupFont();
            ApplyPopupStyle();
            return;
        }

        // Prefer an existing TMP in this scene by name.
        _popupText = FindTextInThisSceneByName(popupTextObjectName);

        if (_popupText == null)
        {
            // Create under a canvas in this scene (or create one if none exist).
            Canvas canvas = FindCanvasInThisScene();
            if (canvas == null)
            {
                var canvasGO = new GameObject("GoalPassedPopupCanvas");
                SceneManager.MoveGameObjectToScene(canvasGO, gameObject.scene);
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            var textGO = new GameObject(popupTextObjectName);
            SceneManager.MoveGameObjectToScene(textGO, gameObject.scene);
            textGO.transform.SetParent(canvas.transform, worldPositionStays: false);

            var rt = textGO.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            _canvasGroup = textGO.AddComponent<CanvasGroup>();

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            tmp.color = textColor;
            tmp.text = "";

            _popupText = tmp;
        }

        EnsureCanvasGroup();
        EnsurePopupFont();
        ApplyPopupStyle();
    }

    private void ApplyPopupStyle()
    {
        if (_popupText == null) return;
        _popupText.fontSize = fontSize;
        _popupText.color = textColor;
        _popupText.alignment = TextAlignmentOptions.Center;
    }

    private void EnsurePopupFont()
    {
        if (_popupText == null) return;

        // Explicit override if provided.
        if (fontOverride != null)
        {
            _popupText.font = fontOverride;
            return;
        }

        // First choice: adopt the font from an existing HUD text in this scene (matches "other UI assets").
        TMP_Text donor = FindAnyOtherTextInThisScene();
        if (donor != null && donor.font != null)
        {
            _popupText.font = donor.font;
            return;
        }

        // Fallback: try to find a loaded font asset by name fragment.
        TMP_FontAsset fallback = FindLoadedFontAssetByNameContains(fallbackFontNameContains);
        if (fallback != null)
        {
            _popupText.font = fallback;
        }
    }

    private TMP_Text FindAnyOtherTextInThisScene()
    {
        var all = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (t == _popupText) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (t.gameObject.scene != gameObject.scene) continue;
            if (t.font == null) continue;
            return t;
        }
        return null;
    }

    private static TMP_FontAsset FindLoadedFontAssetByNameContains(string nameContains)
    {
        if (string.IsNullOrWhiteSpace(nameContains))
            return null;

        string needle = nameContains.Trim();
        var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < all.Length; i++)
        {
            var f = all[i];
            if (f == null) continue;
            if (string.IsNullOrWhiteSpace(f.name)) continue;
            if (f.name.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return f;
        }
        return null;
    }

    private void EnsureCanvasGroup()
    {
        if (_popupText == null) return;
        if (_canvasGroup == null)
            _canvasGroup = _popupText.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = _popupText.gameObject.AddComponent<CanvasGroup>();
    }

    private void HideImmediate()
    {
        if (_popupText == null) return;
        EnsureCanvasGroup();
        _popupText.text = "";
        _canvasGroup.alpha = 0f;
        _popupText.rectTransform.localScale = Vector3.one;
    }

    private TMP_Text FindTextInThisSceneByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var all = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (t.gameObject.scene != gameObject.scene) continue;
            if (!t.gameObject.activeInHierarchy) continue;
            if (string.Equals(t.gameObject.name, name, System.StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
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

    private static float Smooth01(float u)
    {
        // SmoothStep
        return u * u * (3f - 2f * u);
    }
}

internal sealed class GoalPassedPopupBootstrapper : MonoBehaviour
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

        // Already installed?
        var existing = Object.FindObjectsByType<GoalPassedPopupHUD>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].gameObject.scene == scene)
                return;
        }

        var go = new GameObject(nameof(GoalPassedPopupHUD));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<GoalPassedPopupHUD>();
    }
}

internal static class GoalPassedPopupBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        // Avoid duplicates across domain reloads / scene reloads.
#if UNITY_2022_2_OR_NEWER
        if (Object.FindFirstObjectByType<GoalPassedPopupBootstrapper>() != null)
            return;
#else
        if (Object.FindObjectOfType<GoalPassedPopupBootstrapper>() != null)
            return;
#endif

        var go = new GameObject(nameof(GoalPassedPopupBootstrapper));
        Object.DontDestroyOnLoad(go);
        go.AddComponent<GoalPassedPopupBootstrapper>();
    }
}

