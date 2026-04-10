// Updated with Antigravity by jjmil on 2026-04-09 (restored mult text updates; fixed desync on reset).
// Updated with Antigravity by jjmil on 2026-04-07
// (3-canvas layout: Score Canvas / Money Canvas / Mult Canvas;
//  removed per-ball points display; mult now shows "x1", "x1.1" etc.).
// Updated with Cursor (Composer) by assistant on 2026-03-31 (Phase 8).
// Updated with Cursor (Composer) on 2026-04-02: board-scene bindings.
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class ScoreUIController : MonoBehaviour
{
    [Header("Score Canvas")]
    [SerializeField] private TMP_Text roundIndexText;
    [SerializeField] private TMP_Text roundTotalText;
    [SerializeField] private TMP_Text goalText;

    [Header("Money Canvas")]
    [SerializeField] private TMP_Text coinsText;

    [Header("Mult Canvas")]
    [SerializeField] private TMP_Text multText;

    [Header("Optional")]
    [SerializeField] private TMP_Text ballsRemainingText;

    [Header("Round Index Juice (optional)")]
    [Tooltip(
        "If enabled, plays a 'pop' animation when the " +
        "displayed round/level index increases.")]
    [SerializeField] private bool enableRoundIndexPop = true;

    [Min(0f)]
    [SerializeField]
    private float roundIndexPopDuration = 0.22f;

    [Min(1f)]
    [SerializeField]
    private float roundIndexPopPeakScale = 1.35f;

    [Tooltip(
        "Optional vertical offset (anchoredPosition Y) " +
        "applied during the pop.")]
    [SerializeField]
    private float roundIndexPopYOffset = 12f;

    [SerializeField]
    private bool roundIndexPopFlashColor = true;

    [SerializeField]
    private Color roundIndexPopFlashTargetColor =
        new Color(1f, 0.85f, 0.2f, 1f);

    [SerializeField]
    private AnimationCurve roundIndexPopCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.22f, 1f),
            new Keyframe(0.52f, -0.18f),
            new Keyframe(0.78f, 0.12f),
            new Keyframe(1f, 0f));

    private float multUiDisplayed;
    private int coinsUiDisplayed;
    private float _goalUiLast = -1f;

    private readonly Queue<float> multQueue =
        new Queue<float>();

    private int _roundIndexUiLast = -1;
    private int _roundIndexJuiceTextInstanceId;
    private bool _roundIndexJuiceBaselineCaptured;
    private Vector3 _roundIndexBaseLocalScale;
    private Vector2 _roundIndexBaseAnchoredPos;
    private Color _roundIndexBaseColor;
    private Coroutine _roundIndexPopRoutine;

    private Vector3 _multBaseLocalScale;
    private Color _multBaseColor;
    private bool _multBaselineCaptured;
    private int _multTextInstanceId;
    private Coroutine _multResetFlashRoutine;

    private const string ScoreCanvasRootName =
        "Score Canvas";
    private const string MoneyCanvasRootName =
        "Money Canvas";
    private const string MultCanvasRootName =
        "Mult Canvas";

    private const string RoundIndexObjectName =
        "Round Index";
    private const string RoundTotalObjectName =
        "RoundTotal";
    private const string RoundScoreObjectName =
        "Round Score";
    private const string GoalObjectName = "Goal";
    private const string BallsRemainingObjectName =
        "Balls Remaining";
    private const string CoinsObjectName = "Coins";
    private const string MultObjectName = "Mult text";

    public event Action<bool> ScoreUiPopped;

    private void Awake()
    {
        ServiceLocator.Register<ScoreUIController>(this);
        EnsureCoreScoreTextBindings();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        ScoreManager sm =
            ServiceLocator.Get<ScoreManager>();
        if (sm != null)
        {
            sm.MultAdded += OnMultAdded;
            sm.MultReset += OnMultReset;
            sm.ScoreChanged += OnScoreChanged;
        }

        EnsureCoreScoreTextBindings();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (ServiceLocator.TryGet<ScoreManager>(
                out var sm) && sm != null)
        {
            sm.MultAdded -= OnMultAdded;
            sm.MultReset -= OnMultReset;
            sm.ScoreChanged -= OnScoreChanged;
        }

        ServiceLocator.Unregister<ScoreUIController>();
    }

    private void Start()
    {
        EnsureCoreScoreTextBindings();
        RefreshAllText();
    }

    private void Update()
    {
        // Continuously re-attempt binding if any key
        // text reference is missing (handles late
        // scene loads, shop transitions, etc.).
        if (roundTotalText == null
            || multText == null
            || goalText == null
            || coinsText == null)
        {
            EnsureCoreScoreTextBindings();

            if (roundTotalText != null
                || multText != null
                || goalText != null
                || coinsText != null)
            {
                RefreshAllText();
            }
        }
    }

    private void HandleSceneLoaded(
        Scene scene, LoadSceneMode mode)
    {
        EnsureCoreScoreTextBindings();
        RefreshAllText();
    }

    private void OnMultAdded(
        float applied, float newTotal)
    {
        multQueue.Enqueue(newTotal);
    }

    private void OnMultReset()
    {
        if (multText != null)
        {
            multQueue.Clear();
            multUiDisplayed = 1f;
            multText.text = FormatMultiplier(1f);
            PlayMultResetFlash();
        }
    }

    private void OnScoreChanged()
    {
        if (!ServiceLocator.TryGet<ScoreManager>(
                out var sm))
            return;

        // Re-attempt binding if any key text is null.
        if (roundTotalText == null
            || multText == null
            || goalText == null
            || coinsText == null)
        {
            EnsureCoreScoreTextBindings();
        }

        if (roundTotalText != null)
            roundTotalText.text =
                FormatRoundTotalWhole(
                    sm.DisplayRoundTotal);

        if (multText != null)
        {
            float newMult = sm.DisplayMult;
            if (!Mathf.Approximately(
                    newMult, multUiDisplayed))
            {
                multUiDisplayed = newMult;
                multText.text =
                    FormatMultiplier(multUiDisplayed);
            }
        }

        if (goalText != null)
        {
            float currentGoal = sm.CumulativeGoal;
            if (_goalUiLast < 0f)
            {
                _goalUiLast = currentGoal;
            }
            else if (currentGoal > _goalUiLast && !Mathf.Approximately(currentGoal, _goalUiLast))
            {
                _goalUiLast = currentGoal;
                if (ServiceLocator.TryGet<FloatingTextSpawner>(out var spawner) && spawner != null)
                {
                    spawner.PlayJuiceOnTarget(goalText.rectTransform);
                }
            }
            else
            {
                _goalUiLast = currentGoal;
            }
            goalText.text = FormatPointsCompact(currentGoal);
        }

        if (coinsText != null
            && ServiceLocator.TryGet<CoinController>(
                out var cc))
        {
            coinsText.text = $"${cc.DisplayCoins}";
        }
    }

    public void UpdateScoreText()
    {
        bool multActuallyChanged = false;

        if (multText != null && multQueue.Count > 0)
        {
            float newMult = multQueue.Dequeue();
            if (!Mathf.Approximately(
                    newMult, multUiDisplayed))
            {
                multActuallyChanged = true;
                multUiDisplayed = newMult;
                multText.text =
                    FormatMultiplier(multUiDisplayed);
            }
        }

        ScoreUiPopped?.Invoke(multActuallyChanged);
    }

    public void RefreshAllText()
    {
        if (ServiceLocator.TryGet<ScoreManager>(
                out var sm))
        {
            multUiDisplayed = sm.DisplayMult;

            if (multText != null)
                multText.text =
                    FormatMultiplier(multUiDisplayed);
            if (roundTotalText != null)
                roundTotalText.text =
                    FormatRoundTotalWhole(
                        sm.DisplayRoundTotal);
            if (goalText != null)
            {
                _goalUiLast = sm.CumulativeGoal;
                goalText.text = FormatPointsCompact(sm.CumulativeGoal);
            }
        }

        if (ServiceLocator.TryGet<CoinController>(
                out var cc))
        {
            if (coinsText != null)
                coinsText.text = $"${cc.DisplayCoins}";
        }
        else if (ServiceLocator.TryGet<GameRulesManager>(
                out var gm))
        {
            if (coinsText != null)
                coinsText.text = $"${gm.Coins}";
        }
    }

    public void SetRoundIndex(int roundIndex)
    {
        if (roundIndexText == null) return;

        int prev = _roundIndexUiLast;
        _roundIndexUiLast = roundIndex;

        roundIndexText.text =
            $"Level {roundIndex + 1}";

        bool shouldAnimate = enableRoundIndexPop
            && prev >= 0 && roundIndex > prev;
        if (shouldAnimate)
        {
            CaptureRoundIndexJuiceBaselineIfNeeded();
            PlayRoundIndexPop();
        }
    }

    public void SetBallsRemaining(int ballsRemaining)
    {
        if (ballsRemainingText != null)
            ballsRemainingText.text =
                ballsRemaining.ToString();
    }

    public void SetCoins(int coins)
    {
        if (coinsText != null)
            coinsText.text = $"${coins}";
        coinsUiDisplayed = coins;
    }

    /// <summary>
    /// Legacy hook: balance is already in
    /// <see cref="CoinController"/>; sync HUD.
    /// </summary>
    public void ApplyDeferredCoinsUi(int applied)
    {
        if (ServiceLocator.TryGet<CoinController>(
                out var cc) && cc != null)
            SetCoins(cc.DisplayCoins);
        else
        {
            coinsUiDisplayed += applied;
            if (coinsText != null)
                coinsText.text =
                    $"${coinsUiDisplayed}";
        }
    }

    private void CaptureRoundIndexJuiceBaselineIfNeeded()
    {
        if (roundIndexText == null) return;

        int id = roundIndexText.GetInstanceID();
        if (_roundIndexJuiceBaselineCaptured
            && id == _roundIndexJuiceTextInstanceId)
            return;

        _roundIndexJuiceTextInstanceId = id;
        _roundIndexJuiceBaselineCaptured = true;

        RectTransform rt =
            roundIndexText.rectTransform;
        _roundIndexBaseLocalScale = rt.localScale;
        _roundIndexBaseAnchoredPos =
            rt.anchoredPosition;
        _roundIndexBaseColor = roundIndexText.color;
    }

    private void PlayRoundIndexPop()
    {
        if (roundIndexText == null) return;

        if (!_roundIndexJuiceBaselineCaptured)
        {
            CaptureRoundIndexJuiceBaselineIfNeeded();
            if (!_roundIndexJuiceBaselineCaptured)
                return;
        }

        if (_roundIndexPopRoutine != null)
            StopCoroutine(_roundIndexPopRoutine);

        _roundIndexPopRoutine =
            StartCoroutine(RoundIndexPopRoutine());
    }

    private System.Collections.IEnumerator
        RoundIndexPopRoutine()
    {
        TMP_Text text = roundIndexText;
        if (text == null) yield break;

        RectTransform rt = text.rectTransform;
        Vector3 baseScale = _roundIndexBaseLocalScale;
        Vector2 basePos = _roundIndexBaseAnchoredPos;
        Color baseColor = _roundIndexBaseColor;

        float duration =
            Mathf.Max(0.01f, roundIndexPopDuration);
        float amp =
            Mathf.Max(0f, roundIndexPopPeakScale - 1f);

        float t = 0f;
        while (t < duration)
        {
            if (text == null) yield break;

            float n = Mathf.Clamp01(t / duration);
            float k = roundIndexPopCurve != null
                ? roundIndexPopCurve.Evaluate(n)
                : Mathf.Sin(n * Mathf.PI);
            float scaleMul = 1f + (k * amp);

            rt.localScale = baseScale * scaleMul;

            if (!Mathf.Approximately(
                    roundIndexPopYOffset, 0f))
            {
                rt.anchoredPosition = basePos
                    + new Vector2(
                        0f,
                        roundIndexPopYOffset * k);
            }

            if (roundIndexPopFlashColor)
            {
                float flashT =
                    Mathf.Clamp01(n / 0.35f);
                text.color = Color.Lerp(
                    roundIndexPopFlashTargetColor,
                    baseColor,
                    flashT);
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (text != null)
        {
            rt.localScale = baseScale;
            rt.anchoredPosition = basePos;
            text.color = baseColor;
        }

        _roundIndexPopRoutine = null;
    }

    private void CaptureMultBaselineIfNeeded()
    {
        if (multText == null) return;
        int id = multText.GetInstanceID();
        if (_multBaselineCaptured && id == _multTextInstanceId) return;

        _multTextInstanceId = id;
        _multBaselineCaptured = true;
        _multBaseLocalScale = multText.rectTransform.localScale;
        _multBaseColor = multText.color;
    }

    private void PlayMultResetFlash()
    {
        if (multText == null) return;
        CaptureMultBaselineIfNeeded();

        if (_multResetFlashRoutine != null)
            StopCoroutine(_multResetFlashRoutine);

        _multResetFlashRoutine = StartCoroutine(MultResetFlashRoutine());
    }

    private System.Collections.IEnumerator MultResetFlashRoutine()
    {
        TMP_Text text = multText;
        if (text == null) yield break;

        RectTransform rt = text.rectTransform;
        Vector3 baseScale = _multBaseLocalScale;
        Color baseColor = _multBaseColor;

        float duration = 0.35f;
        float t = 0f;

        while (t < duration)
        {
            if (text == null) yield break;

            float n = Mathf.Clamp01(t / duration);
            Color flashColor = Color.red;
            text.color = Color.Lerp(flashColor, baseColor, n);
            rt.localScale = baseScale * (1f + 0.3f * Mathf.Sin(n * Mathf.PI));

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (text != null)
        {
            rt.localScale = baseScale;
            text.color = baseColor;
        }

        _multResetFlashRoutine = null;
    }

    public void EnsureCoreScoreTextBindings()
    {
        if (AllScoreTextBindingsLive())
            return;

        if (TryResolveBindingsFromBoardLoaderScene())
        {
            if (AllScoreTextBindingsLive())
                return;
        }

        // Try each canvas by name (active objects).
        TryBindFromCanvasRoot(
            ScoreCanvasRootName);
        TryBindFromCanvasRoot(
            MoneyCanvasRootName);
        TryBindFromCanvasRoot(
            MultCanvasRootName);

        if (AllScoreTextBindingsLive())
            return;

        // Search all loaded scene root objects
        // (catches inactive canvases too).
        TryBindFromAllSceneRoots();

        if (AllScoreTextBindingsLive())
            return;

        // Final fallback: global search.
        TMP_Text[] loadedTexts =
            FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        ApplyBindingsByDisplayObjectName(loadedTexts);

        Debug.Log(
            $"[ScoreUIController] Bindings: " +
            $"roundTotal={roundTotalText != null}, " +
            $"mult={multText != null}, " +
            $"goal={goalText != null}, " +
            $"coins={coinsText != null}, " +
            $"roundIndex={roundIndexText != null}, " +
            $"ballsRemaining=" +
            $"{ballsRemainingText != null}");
    }

    private void TryBindFromCanvasRoot(
        string canvasRootName)
    {
        GameObject root =
            GameObject.Find(canvasRootName);
        if (root == null) return;

        TMP_Text[] texts =
            root.GetComponentsInChildren<TMP_Text>(
                true);
        ApplyBindingsByDisplayObjectName(texts);
    }

    /// <summary>
    /// Searches all root GameObjects in every
    /// loaded scene for canvases that match our
    /// expected names (catches inactive roots
    /// that GameObject.Find would miss).
    /// </summary>
    private void TryBindFromAllSceneRoots()
    {
        int sceneCount = SceneManager.sceneCount;
        for (int s = 0; s < sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots =
                scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                GameObject go = roots[r];
                if (go == null) continue;

                string n = go.name;
                if (string.Equals(
                        n, ScoreCanvasRootName,
                        StringComparison
                            .OrdinalIgnoreCase)
                    || string.Equals(
                        n, MoneyCanvasRootName,
                        StringComparison
                            .OrdinalIgnoreCase)
                    || string.Equals(
                        n, MultCanvasRootName,
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    TMP_Text[] texts =
                        go.GetComponentsInChildren
                            <TMP_Text>(true);
                    ApplyBindingsByDisplayObjectName(
                        texts);
                }
            }
        }
    }

    private bool AllScoreTextBindingsLive()
    {
        return IsLiveSceneText(multText)
            && IsLiveSceneText(roundTotalText)
            && IsLiveSceneText(goalText)
            && IsLiveSceneText(coinsText);
    }

    private bool
        TryResolveBindingsFromBoardLoaderScene()
    {
        if (!ServiceLocator.TryGet<BoardLoader>(
                out BoardLoader loader)
            || loader == null)
            return false;

        string sceneName =
            loader.CurrentBoardSceneName;
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        Scene scene =
            SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        GameObject[] roots =
            scene.GetRootGameObjects();
        var buffer = new List<TMP_Text>(64);
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject go = roots[i];
            if (go == null) continue;
            buffer.AddRange(
                go.GetComponentsInChildren<TMP_Text>(
                    true));
        }

        ApplyBindingsByDisplayObjectName(
            buffer, overrideCrossScene: true);
        return true;
    }

    private void ApplyBindingsByDisplayObjectName(
        IReadOnlyList<TMP_Text> texts,
        bool overrideCrossScene = false)
    {
        if (texts == null) return;

        for (int i = 0; i < texts.Count; i++)
        {
            TMP_Text t = texts[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;

            string n = t.gameObject.name;

            if (ShouldBind(
                    multText, t, overrideCrossScene)
                && string.Equals(
                    n, MultObjectName,
                    StringComparison
                        .OrdinalIgnoreCase))
                multText = t;
            else if (ShouldBind(
                    roundIndexText, t, overrideCrossScene)
                && string.Equals(
                    n, RoundIndexObjectName,
                    StringComparison
                        .OrdinalIgnoreCase))
                roundIndexText = t;
            else if (ShouldBind(
                    roundTotalText, t, overrideCrossScene)
                && (string.Equals(
                        n, RoundTotalObjectName,
                        StringComparison
                            .OrdinalIgnoreCase)
                    || string.Equals(
                        n, RoundScoreObjectName,
                        StringComparison
                            .OrdinalIgnoreCase)))
                roundTotalText = t;
            else if (ShouldBind(
                    goalText, t, overrideCrossScene)
                && string.Equals(
                    n, GoalObjectName,
                    StringComparison
                        .OrdinalIgnoreCase))
                goalText = t;
            else if (ShouldBind(
                    ballsRemainingText, t,
                    overrideCrossScene)
                && string.Equals(
                    n, BallsRemainingObjectName,
                    StringComparison
                        .OrdinalIgnoreCase))
                ballsRemainingText = t;
            else if (ShouldBind(
                    coinsText, t, overrideCrossScene)
                && string.Equals(
                    n, CoinsObjectName,
                    StringComparison
                        .OrdinalIgnoreCase))
                coinsText = t;
        }
    }

    private static bool ShouldBind(
        TMP_Text existing,
        TMP_Text candidate,
        bool overrideCrossScene)
    {
        if (!IsLiveSceneText(existing))
            return true;
        if (overrideCrossScene
            && existing.gameObject.scene
                != candidate.gameObject.scene)
            return true;
        return false;
    }

    private static bool IsLiveSceneText(TMP_Text t)
    {
        if (t == null) return false;
        if (!t.gameObject.scene.IsValid()) return false;
        return true;
    }

    public static string FormatPointsCompact(
        float value)
    {
        float abs = Mathf.Abs(value);
        if (abs < 10000f)
        {
            float rounded1 =
                Mathf.Round(value * 10f) / 10f;
            return rounded1.ToString(
                "#,##0.#", CultureInfo.InvariantCulture);
        }

        float scale = 1000f;
        string suffix = "K";
        if (abs >= 1000000000f)
        {
            scale = 1000000000f;
            suffix = "B";
        }
        else if (abs >= 1000000f)
        {
            scale = 1000000f;
            suffix = "M";
        }

        float scaled = abs / scale;
        float scaledRounded1 =
            Mathf.Round(scaled * 10f) / 10f;

        if (scaledRounded1 >= 1000f)
        {
            if (suffix == "K")
            {
                scale = 1000000f;
                suffix = "M";
            }
            else if (suffix == "M")
            {
                scale = 1000000000f;
                suffix = "B";
            }

            scaled = abs / scale;
            scaledRounded1 =
                Mathf.Round(scaled * 10f) / 10f;
        }

        if (scaledRounded1 >= 100f)
        {
            string s =
                Mathf.RoundToInt(scaledRounded1)
                    .ToString(
                        CultureInfo.InvariantCulture)
                + suffix;
            return value < 0f ? "-" + s : s;
        }

        string core = scaledRounded1.ToString(
            "#,##0.#", CultureInfo.InvariantCulture)
            + suffix;
        return value < 0f ? "-" + core : core;
    }

    public static string FormatPointsOneDecimal(
        float value)
    {
        float abs = Mathf.Abs(value);
        if (abs < 10000f)
        {
            float r =
                Mathf.Round(value * 10f) / 10f;
            return r.ToString(
                "#,##0.0", CultureInfo.InvariantCulture);
        }
        return FormatPointsCompact(value);
    }

    public static string FormatRoundTotalWhole(
        float value)
    {
        float abs = Mathf.Abs(value);
        if (abs < 10000f)
        {
            int w = Mathf.CeilToInt(value);
            return w.ToString(
                "N0", CultureInfo.InvariantCulture);
        }
        return FormatPointsCompact(value);
    }

    /// <summary>
    /// Formats the board multiplier as "x1", "x1.1",
    /// "x2.3", etc.
    /// </summary>
    public static string FormatMultiplier(float value)
    {
        float rounded =
            Mathf.Round(value * 100f) / 100f;
        return "x" + rounded.ToString(
            "#,##0.##", CultureInfo.InvariantCulture);
    }
}
