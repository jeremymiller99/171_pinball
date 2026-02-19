// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// When the round goal is passed in multiples (GoalTier increases), updates a persistent multiplier text
/// (e.g. "x2", "x3", ...) and spawns a centered "Level Up" popup.
///
/// This auto-installs itself into the GameplayCore scene at runtime (no manual scene wiring required).
/// </summary>
public sealed class GoalPassedPopupHUD : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Scene name that should own this popup HUD.")]
    [SerializeField] private string gameplayCoreSceneName = "GameplayCore";

    [Header("Multiplier")]
    [Tooltip("Displayed multiplier number = GoalTier + this offset.\n" +
             "Example: if GoalTier becomes 1 at the first goal, set this to 1 to display x2.")]
    [SerializeField] private int tierToMultiplierNumberOffset = 1;

    [Tooltip("Name of the TMP text object used for the persistent multiplier display.\n" +
             "Create and position this yourself in the UI, or let this script auto-create it.")]
    [SerializeField] private string multiplierTextObjectName = "GoalMultiplierText";

    [Tooltip("If true, this script will SetActive(true/false) on the multiplier text object\n" +
             "when showing/hiding (recommended if you keep it disabled in the scene by default).")]
    [SerializeField] private bool toggleMultiplierObjectActive = true;

    [SerializeField] private bool autoCreateMultiplierTextIfMissing = false;

    [Header("Multiplier Text Style (used only when auto-creating)")]
    [SerializeField] private Vector2 multiplierAnchor = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 multiplierPivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 multiplierAnchoredPosition = Vector2.zero;
    [SerializeField] private Vector2 multiplierSize = new Vector2(200f, 60f);
    [SerializeField] private float multiplierFontSize = 22f;
    [SerializeField] private Color multiplierTextColor = Color.white;
    [SerializeField] private TMP_FontAsset multiplierFontOverride;

    [Header("Level Up Popup")]
    [SerializeField] private bool spawnPraise = true;
    [SerializeField] private string levelUpPopupText = "Level Up";

    [Tooltip("If true, hides the old center-screen TMP object if it exists in the scene.")]
    [SerializeField] private bool hideLegacyCenterPopupIfPresent = true;

    [SerializeField] private string legacyPopupTextObjectName = "GoalPassedPopupText";

    private ScoreManager score;
    private FloatingTextSpawner spawner;
    private GameRulesManager rules;
    private TMP_Text multiplierText;
    private CanvasGroup multiplierCanvasGroup;
    private int lastSeenTier;

    private void Awake()
    {
        if (!string.IsNullOrWhiteSpace(gameplayCoreSceneName) &&
            gameObject.scene.IsValid() &&
            !string.Equals(
                gameObject.scene.name,
                gameplayCoreSceneName,
                StringComparison.OrdinalIgnoreCase))
        {
            enabled = false;
            return;
        }

        ResolveRefs();
        EnsureMultiplierText();
        HideMultiplierImmediate();

        if (hideLegacyCenterPopupIfPresent)
        {
            HideLegacyPopupIfPresent();
        }
    }

    private void OnEnable()
    {
        ResolveRefs();
        EnsureMultiplierText();
        if (score != null)
        {
            score.GoalTierChanged += OnGoalTierChanged;
        }

        if (rules != null)
        {
            rules.RoundStarted += OnRoundStarted;
            rules.ShopOpened += OnShopOpened;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (score != null)
        {
            score.GoalTierChanged -= OnGoalTierChanged;
        }

        if (rules != null)
        {
            rules.RoundStarted -= OnRoundStarted;
            rules.ShopOpened -= OnShopOpened;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void ResolveRefs()
    {
        ResolveScoreManager();
        ResolveSpawner();
        ResolveRulesManager();
    }

    private void ResolveScoreManager()
    {
        if (score != null) return;

#if UNITY_2022_2_OR_NEWER
        score = FindFirstObjectByType<ScoreManager>();
#else
        score = FindObjectOfType<ScoreManager>();
#endif
    }

    private void ResolveSpawner()
    {
        if (spawner != null) return;

#if UNITY_2022_2_OR_NEWER
        spawner = FindFirstObjectByType<FloatingTextSpawner>();
#else
        spawner = FindObjectOfType<FloatingTextSpawner>();
#endif
    }

    private void ResolveRulesManager()
    {
        if (rules != null) return;

#if UNITY_2022_2_OR_NEWER
        rules = FindFirstObjectByType<GameRulesManager>();
#else
        rules = FindObjectOfType<GameRulesManager>();
#endif
    }

    private void OnRoundStarted()
    {
        lastSeenTier = 0;
        HideMultiplierImmediate();
    }

    private void OnShopOpened()
    {
        HideMultiplierImmediate();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureMultiplierText();
    }

    private void OnGoalTierChanged(int tier)
    {
        if (tier <= 0)
        {
            lastSeenTier = tier;
            HideMultiplierImmediate();
            return;
        }

        if (tier <= lastSeenTier)
        {
            lastSeenTier = tier;
            return;
        }

        lastSeenTier = tier;
        ResolveSpawner();
        EnsureMultiplierText();

        int multiplierNumber = Mathf.Max(0, tier + tierToMultiplierNumberOffset);
        SetMultiplierText(multiplierNumber);

        if (spawnPraise && spawner != null)
        {
            if (!string.IsNullOrWhiteSpace(levelUpPopupText))
            {
                spawner.SpawnGoalPraisePopup(levelUpPopupText);
            }
        }
    }

    private void HideLegacyPopupIfPresent()
    {
        if (string.IsNullOrWhiteSpace(legacyPopupTextObjectName))
            return;

        TMP_Text legacy = FindTextInThisSceneByName(legacyPopupTextObjectName.Trim());
        if (legacy == null) return;

        legacy.text = "";

        CanvasGroup cg = legacy.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = legacy.gameObject.AddComponent<CanvasGroup>();
        }

        cg.alpha = 0f;
        legacy.rectTransform.localScale = Vector3.one;
    }

    private void EnsureMultiplierText()
    {
        if (multiplierText != null && IsLiveSceneObject(multiplierText.gameObject))
        {
            EnsureMultiplierCanvasGroup();
            return;
        }

        multiplierText = FindTextInLoadedScenesByName(multiplierTextObjectName);
        if (multiplierText == null)
        {
            if (!autoCreateMultiplierTextIfMissing)
                return;

            Canvas canvas = FindCanvasInThisScene();
            if (canvas == null)
            {
                var canvasGO = new GameObject("GoalMultiplierCanvas");
                SceneManager.MoveGameObjectToScene(canvasGO, gameObject.scene);
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            string n = string.IsNullOrWhiteSpace(multiplierTextObjectName)
                ? "GoalMultiplierText"
                : multiplierTextObjectName.Trim();

            var textGO = new GameObject(n);
            SceneManager.MoveGameObjectToScene(textGO, gameObject.scene);
            textGO.transform.SetParent(canvas.transform, worldPositionStays: false);

            var rt = textGO.AddComponent<RectTransform>();
            rt.anchorMin = multiplierAnchor;
            rt.anchorMax = multiplierAnchor;
            rt.pivot = multiplierPivot;
            rt.anchoredPosition = multiplierAnchoredPosition;
            rt.sizeDelta = multiplierSize;

            multiplierCanvasGroup = textGO.AddComponent<CanvasGroup>();

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = multiplierFontSize;
            tmp.color = multiplierTextColor;
            tmp.text = "";

            if (multiplierFontOverride != null)
            {
                tmp.font = multiplierFontOverride;
            }

            multiplierText = tmp;
        }

        EnsureMultiplierCanvasGroup();
    }

    private void EnsureMultiplierCanvasGroup()
    {
        if (multiplierText == null) return;

        if (multiplierCanvasGroup == null)
        {
            multiplierCanvasGroup = multiplierText.GetComponent<CanvasGroup>();
        }

        if (multiplierCanvasGroup == null)
        {
            multiplierCanvasGroup = multiplierText.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void HideMultiplierImmediate()
    {
        if (multiplierText == null) return;
        EnsureMultiplierCanvasGroup();

        multiplierText.text = "";
        multiplierCanvasGroup.alpha = 0f;

        if (toggleMultiplierObjectActive)
        {
            multiplierText.gameObject.SetActive(false);
        }
    }

    private void SetMultiplierText(int multiplierNumber)
    {
        if (multiplierText == null) return;
        EnsureMultiplierCanvasGroup();

        int n = Mathf.Max(0, multiplierNumber);
        multiplierText.text = "x" + n;
        multiplierCanvasGroup.alpha = 1f;

        if (toggleMultiplierObjectActive)
        {
            multiplierText.gameObject.SetActive(true);
        }
    }

    private TMP_Text FindTextInThisSceneByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var all = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < all.Length; i++)
        {
            TMP_Text t = all[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (t.gameObject.scene != gameObject.scene) continue;
            if (!t.gameObject.activeInHierarchy) continue;
            if (string.Equals(t.gameObject.name, name, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }

    private TMP_Text FindTextInLoadedScenesByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string n = name.Trim();
        TMP_Text bestAny = null;
        TMP_Text bestNotGameplayCore = null;

        var all = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < all.Length; i++)
        {
            TMP_Text t = all[i];
            if (t == null) continue;
            if (!IsLiveSceneObject(t.gameObject)) continue;
            if (!string.Equals(t.gameObject.name, n, StringComparison.OrdinalIgnoreCase))
                continue;

            bestAny = t;

            if (t.gameObject.scene.IsValid()
                && !string.Equals(
                    t.gameObject.scene.name,
                    gameplayCoreSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                bestNotGameplayCore = t;
                break;
            }
        }

        return bestNotGameplayCore != null ? bestNotGameplayCore : bestAny;
    }

    private static bool IsLiveSceneObject(GameObject go)
    {
        if (go == null) return false;
        if (!go.scene.IsValid()) return false;
        return true;
    }

    private Canvas FindCanvasInThisScene()
    {
        var all = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < all.Length; i++)
        {
            Canvas c = all[i];
            if (c == null) continue;
            if (!c.gameObject.scene.IsValid()) continue;
            if (c.gameObject.scene != gameObject.scene) continue;
            if (!c.gameObject.activeInHierarchy) continue;
            return c;
        }

        return null;
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
        var existing = UnityEngine.Object
            .FindObjectsByType<GoalPassedPopupHUD>(FindObjectsSortMode.None);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].gameObject.scene == scene)
                return;
        }

        var go = new GameObject(nameof(GoalPassedPopupHUD));
        go.hideFlags = HideFlags.HideInHierarchy;
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
        if (UnityEngine.Object.FindFirstObjectByType<GoalPassedPopupBootstrapper>() != null)
            return;
#else
        if (UnityEngine.Object.FindObjectOfType<GoalPassedPopupBootstrapper>() != null)
            return;
#endif

        var go = new GameObject(nameof(GoalPassedPopupBootstrapper));
        go.hideFlags = HideFlags.HideInHierarchy;
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<GoalPassedPopupBootstrapper>();
    }
}

