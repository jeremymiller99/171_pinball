// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FloatingTextSpawner : MonoBehaviour
{
    [SerializeField] private FloatingText floatingTextPrefab;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 spawnOffset = new Vector2(20f, 0f);

    [Header("Fly To Score UI")]
    [SerializeField] private bool enableFlyToScoreUi = true;
    [SerializeField] private Vector2 flyToOffset = Vector2.zero;

    [Header("Arrival Juice")]
    [SerializeField] private bool juiceOnArrival = true;
    [SerializeField] private float juiceScaleMultiplier = 1.25f;
    [SerializeField] private float juiceScaleDuration = 0.12f;
    [SerializeField] private float juiceRotationDegreesMax = 10f;
    [SerializeField] private float juiceRotationDuration = 0.10f;

    [Header("Points")]
    [Tooltip("TMP Font Asset for points text (blue style).")]
    [SerializeField] private TMP_FontAsset pointsFontAsset;
    [SerializeField] private float pointsScaleMin = 0.6f;
    [SerializeField] private float pointsScaleMax = 1.2f;
    [Tooltip("Points value at which scale reaches max.")]
    [SerializeField] private float pointsMaxValue = 100f;

    [Header("Multiplier")]
    [Tooltip("TMP Font Asset for multiplier text (red style).")]
    [SerializeField] private TMP_FontAsset multFontAsset;
    [SerializeField] private float multScaleMin = 0.6f;
    [SerializeField] private float multScaleMax = 1.3f;
    [Tooltip("Mult value at which scale reaches max.")]
    [SerializeField] private float multMaxValue = 3f;

    [Header("Gold/Coins")]
    [Tooltip("TMP Font Asset for gold/coins text (yellow style).")]
    [SerializeField] private TMP_FontAsset goldFontAsset;
    [SerializeField] private float goldScaleMin = 0.6f;
    [SerializeField] private float goldScaleMax = 1.1f;
    [Tooltip("Gold value at which scale reaches max.")]
    [SerializeField] private float goldMaxValue = 5f;

    [Header("Goal Passed Popups (UI anchored)")]
    [SerializeField] private Vector2 goalMultPopupOffset = new Vector2(40f, 18f);
    [SerializeField] private float goalMultPopupScale = 1.10f;
    [SerializeField] private float goalMultPopupLifetime = 0.85f;

    [Tooltip("Offset from screen center for goal praise popup.")]
    [SerializeField] private Vector2 goalPraisePopupOffset = Vector2.zero;
    [SerializeField] private TMP_FontAsset goalPraiseFontAsset;
    [SerializeField] private float goalPraisePopupScale = 0.95f;
    [SerializeField] private float goalPraisePopupLifetime = 0.85f;

    [Header("Goal Passed Popups - Pop In (unscaled time)")]
    [SerializeField] private bool goalPopupEnablePopIn = true;
    [SerializeField] private float goalPopupPopInStartScaleMultiplier = 1.18f;
    [SerializeField] private float goalPopupPopInDuration = 0.12f;

    private RectTransform pointsTarget;
    private RectTransform multTarget;
    private RectTransform coinsTarget;

    private const string scorePanelRootName = "Score Panel";
    private const string roundInfoPanelRootName = "Round Info Panel";
    private const string pointsObjectName = "Points";
    private const string multObjectName = "Mult";
    private const string coinsObjectName = "Coins";

    private enum FlyToTarget
    {
        None,
        Points,
        Mult,
        Coins,
    }

    private readonly Dictionary<RectTransform, Coroutine> juiceRoutineByTarget =
        new Dictionary<RectTransform, Coroutine>();

    private readonly Dictionary<RectTransform, Vector3> juiceBaseScaleByTarget =
        new Dictionary<RectTransform, Vector3>();

    private readonly Dictionary<RectTransform, Quaternion> juiceBaseRotationByTarget =
        new Dictionary<RectTransform, Quaternion>();

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;

        EnsureTargetBindings();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InvalidateTargetBindings();
        EnsureTargetBindings();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        InvalidateTargetBindings();
    }

    /// <summary>
    /// Spawns floating text at the given world position, mapped to the canvas.
    /// </summary>
    /// <param name="worldPosition">World position to spawn the text (e.g., ball position)</param>
    /// <param name="text">The text to display</param>
    public void SpawnText(Vector3 worldPosition, string text)
    {
        SpawnTextInternal(worldPosition, text, null, 0.6f, FlyToTarget.None);
    }

    /// <summary>
    /// Spawns floating text for points using the points font asset, size based on value.
    /// </summary>
    public void SpawnPointsText(Vector3 worldPosition, string text, float pointsValue)
    {
        SpawnPointsText(worldPosition, text, pointsValue, null);
    }

    /// <summary>
    /// Spawns points text and invokes <paramref name="onArrive"/> when the text finishes flying (or immediately if it can't fly).
    /// </summary>
    public void SpawnPointsText(Vector3 worldPosition, string text, float pointsValue, Action onArrive)
    {
        float t = Mathf.Clamp01(pointsValue / pointsMaxValue);
        float scale = Mathf.Lerp(pointsScaleMin, pointsScaleMax, t);
        SpawnTextInternal(worldPosition, text, pointsFontAsset, scale, FlyToTarget.Points, onArrive);
    }

    /// <summary>
    /// Spawns floating text for multiplier using the mult font asset, size based on value.
    /// </summary>
    public void SpawnMultText(Vector3 worldPosition, string text, float multValue)
    {
        SpawnMultText(worldPosition, text, multValue, null);
    }

    /// <summary>
    /// Spawns mult text and invokes <paramref name="onArrive"/> when the text finishes flying (or immediately if it can't fly).
    /// </summary>
    public void SpawnMultText(Vector3 worldPosition, string text, float multValue, Action onArrive)
    {
        float t = Mathf.Clamp01(multValue / multMaxValue);
        float scale = Mathf.Lerp(multScaleMin, multScaleMax, t);
        SpawnTextInternal(worldPosition, text, multFontAsset, scale, FlyToTarget.Mult, onArrive);
    }

    /// <summary>
    /// Spawns floating text for gold/coins using the gold font asset, size based on value.
    /// </summary>
    public void SpawnGoldText(Vector3 worldPosition, string text, float goldValue)
    {
        float t = Mathf.Clamp01(goldValue / goldMaxValue);
        float scale = Mathf.Lerp(goldScaleMin, goldScaleMax, t);
        SpawnTextInternal(worldPosition, text, goldFontAsset, scale, FlyToTarget.Coins);
    }

    /// <summary>
    /// Spawns gold/coins text and invokes <paramref name="onArrive"/> when the text finishes flying
    /// (or immediately if it can't fly).
    /// </summary>
    public void SpawnGoldText(Vector3 worldPosition, string text, float goldValue, Action onArrive)
    {
        float t = Mathf.Clamp01(goldValue / goldMaxValue);
        float scale = Mathf.Lerp(goldScaleMin, goldScaleMax, t);
        SpawnTextInternal(worldPosition, text, goldFontAsset, scale, FlyToTarget.Coins, onArrive);
    }

    public void SpawnGoalTierMultPopup(int multiplierNumber)
    {
        EnsureTargetBindings();
        if (!IsLiveRect(multTarget)) return;

        int n = Mathf.Max(0, multiplierNumber);
        string text = "x" + n;
        SpawnUiTextInternal(
            multTarget,
            goalMultPopupOffset,
            text,
            multFontAsset,
            goalMultPopupScale,
            goalMultPopupLifetime,
            goalPopupEnablePopIn);
    }

    public void SpawnGoalPraisePopup(string praise)
    {
        if (string.IsNullOrWhiteSpace(praise))
            return;

        if (!TryGetCanvasCenterAnchoredPosition(out Vector2 anchoredOnCanvas))
            return;

        SpawnAnchoredTextInternal(
            anchoredOnCanvas + goalPraisePopupOffset,
            praise.Trim(),
            goalPraiseFontAsset,
            goalPraisePopupScale,
            goalPraisePopupLifetime,
            goalPopupEnablePopIn,
            goalPopupPopInStartScaleMultiplier,
            goalPopupPopInDuration);
    }

    private void SpawnTextInternal(
        Vector3 worldPosition,
        string text,
        TMP_FontAsset fontAsset,
        float scale,
        FlyToTarget flyToTarget,
        Action onArrive = null)
    {
        if (floatingTextPrefab == null || canvas == null) return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        FloatingText ft = Instantiate(floatingTextPrefab, canvas.transform);
        ft.gameObject.hideFlags = HideFlags.HideInHierarchy;
        RectTransform rt = ft.GetComponent<RectTransform>();

        if (!TryGetBestScreenPoint(worldPosition, null, targetCamera, out Vector3 screenPos))
            return;

        // Convert screen pixel position to anchored position inside canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPos,
            GetCanvasCamera(),
            out Vector2 anchoredPos);

        rt.anchoredPosition = anchoredPos + spawnOffset;
        ft.SetText(text);
        
        if (fontAsset != null)
        {
            ft.SetFontAsset(fontAsset);
        }
        
        ft.SetScale(scale);

        if (flyToTarget != FlyToTarget.None
            && enableFlyToScoreUi
            && TryGetFlyToAnchoredPosition(flyToTarget, out Vector2 destAnchored))
        {
            ft.PlayFlyTo(destAnchored);
            ft.SetOnFlyComplete(() =>
            {
                onArrive?.Invoke();
                TriggerJuiceForTarget(flyToTarget);
            });
        }
        else
        {
            onArrive?.Invoke();
        }
    }

    private void SpawnUiTextInternal(
        RectTransform uiAnchor,
        Vector2 offset,
        string text,
        TMP_FontAsset fontAsset,
        float scale,
        float lifetime,
        bool enablePopIn)
    {
        if (floatingTextPrefab == null || canvas == null) return;
        if (!IsLiveRect(uiAnchor)) return;

        if (!TryGetUiElementAnchoredOnSpawnerCanvas(uiAnchor, out Vector2 anchoredOnCanvas))
            return;

        Vector2 anchoredPos = anchoredOnCanvas + offset;
        SpawnAnchoredTextInternal(
            anchoredPos,
            text,
            fontAsset,
            scale,
            lifetime,
            enablePopIn,
            goalPopupPopInStartScaleMultiplier,
            goalPopupPopInDuration);
    }

    private void SpawnAnchoredTextInternal(
        Vector2 anchoredPosition,
        string text,
        TMP_FontAsset fontAsset,
        float scale,
        float lifetime,
        bool enablePopIn,
        float popInStartScaleMultiplier,
        float popInDurationSeconds)
    {
        if (floatingTextPrefab == null || canvas == null) return;

        FloatingText ft = Instantiate(floatingTextPrefab, canvas.transform);
        ft.gameObject.hideFlags = HideFlags.HideInHierarchy;
        RectTransform rt = ft.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchoredPosition = anchoredPosition;
        ft.SetText(text);

        if (fontAsset != null)
        {
            ft.SetFontAsset(fontAsset);
        }

        float safeScale = Mathf.Max(0.0001f, scale);
        ft.SetScale(safeScale);
        ft.SetLifetime(lifetime);

        if (enablePopIn)
        {
            float startMult = Mathf.Max(0.0001f, popInStartScaleMultiplier);
            rt.localScale = Vector3.one * (safeScale * startMult);
            StartCoroutine(PopInScaleRoutine(rt, safeScale, popInStartScaleMultiplier, popInDurationSeconds));
        }
    }

    private void TriggerJuiceForTarget(FlyToTarget target)
    {
        if (!juiceOnArrival)
            return;

        EnsureTargetBindings();

        RectTransform rt = null;
        if (target == FlyToTarget.Points) rt = pointsTarget;
        else if (target == FlyToTarget.Mult) rt = multTarget;
        else if (target == FlyToTarget.Coins) rt = coinsTarget;

        if (!IsLiveRect(rt))
            return;

        if (juiceRoutineByTarget.TryGetValue(rt, out Coroutine existing) && existing != null)
        {
            StopCoroutine(existing);

            if (juiceBaseScaleByTarget.TryGetValue(rt, out Vector3 s))
                rt.localScale = s;
            if (juiceBaseRotationByTarget.TryGetValue(rt, out Quaternion q))
                rt.localRotation = q;
        }

        juiceRoutineByTarget[rt] = StartCoroutine(JuiceRoutine(rt));
    }

    private IEnumerator JuiceRoutine(RectTransform target)
    {
        if (target == null) yield break;

        Vector3 baseScale = target.localScale;
        Quaternion baseRot = target.localRotation;

        juiceBaseScaleByTarget[target] = baseScale;
        juiceBaseRotationByTarget[target] = baseRot;

        float scaleDuration = Mathf.Max(0.0001f, juiceScaleDuration);
        float rotDuration = Mathf.Max(0.0001f, juiceRotationDuration);
        float duration = Mathf.Max(scaleDuration, rotDuration);

        float rot = UnityEngine.Random.Range(-juiceRotationDegreesMax, juiceRotationDegreesMax);
        Vector3 peakScale = baseScale * Mathf.Max(0.0001f, juiceScaleMultiplier);
        Quaternion peakRot = baseRot * Quaternion.Euler(0f, 0f, rot);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);

            float scaleU = Mathf.Clamp01(elapsed / scaleDuration);
            float scaleP = scaleU < 0.5f ? (scaleU / 0.5f) : ((1f - scaleU) / 0.5f);
            float scaleS = scaleP * scaleP * (3f - 2f * scaleP);
            target.localScale = Vector3.LerpUnclamped(baseScale, peakScale, scaleS);

            float rotU = Mathf.Clamp01(elapsed / rotDuration);
            float rotP = rotU < 0.5f ? (rotU / 0.5f) : ((1f - rotU) / 0.5f);
            float rotS = rotP * rotP * (3f - 2f * rotP);
            target.localRotation = Quaternion.SlerpUnclamped(baseRot, peakRot, rotS);

            yield return null;
        }

        target.localScale = baseScale;
        target.localRotation = baseRot;
    }

    private Camera GetCanvasCamera()
    {
        if (canvas == null) return targetCamera != null ? targetCamera : Camera.main;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (canvas.worldCamera != null)
            return canvas.worldCamera;

        return targetCamera != null ? targetCamera : Camera.main;
    }

    private void InvalidateTargetBindings()
    {
        pointsTarget = null;
        multTarget = null;
        coinsTarget = null;
    }

    private void EnsureTargetBindings()
    {
        if (IsLiveRect(pointsTarget) && IsLiveRect(multTarget) && IsLiveRect(coinsTarget))
            return;

        if (!IsLiveRect(pointsTarget))
            pointsTarget = TryFindTextRectInPanel(scorePanelRootName, pointsObjectName);

        if (!IsLiveRect(multTarget))
            multTarget = TryFindTextRectInPanel(scorePanelRootName, multObjectName);

        if (!IsLiveRect(coinsTarget))
            coinsTarget = TryFindTextRectInPanel(roundInfoPanelRootName, coinsObjectName);

        if (IsLiveRect(pointsTarget) && IsLiveRect(multTarget) && IsLiveRect(coinsTarget))
            return;

        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < allTexts.Length; i++)
        {
            TMP_Text t = allTexts[i];
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            if (!t.gameObject.activeInHierarchy) continue;

            string n = t.gameObject.name;
            if (!IsLiveRect(pointsTarget)
                && string.Equals(n, pointsObjectName, StringComparison.OrdinalIgnoreCase))
            {
                pointsTarget = t.rectTransform;
            }
            else if (!IsLiveRect(multTarget)
                && string.Equals(n, multObjectName, StringComparison.OrdinalIgnoreCase))
            {
                multTarget = t.rectTransform;
            }
            else if (!IsLiveRect(coinsTarget)
                && string.Equals(n, coinsObjectName, StringComparison.OrdinalIgnoreCase))
            {
                coinsTarget = t.rectTransform;
            }
        }
    }

    private static RectTransform TryFindTextRectInPanel(string panelRootName, string textObjectName)
    {
        if (string.IsNullOrEmpty(panelRootName) || string.IsNullOrEmpty(textObjectName))
            return null;

        GameObject panel = GameObject.Find(panelRootName);
        if (panel == null) return null;

        TMP_Text[] texts = panel.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text t = texts[i];
            if (t == null) continue;
            if (!t.gameObject.activeInHierarchy) continue;
            if (!string.Equals(t.gameObject.name, textObjectName, StringComparison.OrdinalIgnoreCase))
                continue;

            return t.rectTransform;
        }

        return null;
    }

    private static bool IsLiveRect(RectTransform r)
    {
        if (r == null) return false;
        if (!r.gameObject.scene.IsValid()) return false;
        if (!r.gameObject.activeInHierarchy) return false;
        return true;
    }

    private bool TryGetFlyToAnchoredPosition(FlyToTarget target, out Vector2 anchoredOnSpawnerCanvas)
    {
        anchoredOnSpawnerCanvas = default;
        if (!enableFlyToScoreUi) return false;
        if (canvas == null) return false;

        EnsureTargetBindings();

        RectTransform dest = null;
        if (target == FlyToTarget.Points) dest = pointsTarget;
        else if (target == FlyToTarget.Mult) dest = multTarget;
        else if (target == FlyToTarget.Coins) dest = coinsTarget;

        if (!IsLiveRect(dest)) return false;

        int layer = dest.gameObject.layer;
        Camera preferred = GetPreferredCameraForUiElement(dest);
        if (!TryGetBestScreenPoint(dest.position, layer, preferred, out Vector3 screen3))
            return false;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return false;

        Vector2 screen = new Vector2(screen3.x, screen3.y);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screen,
                GetCanvasCamera(),
                out Vector2 anchored))
        {
            return false;
        }

        anchoredOnSpawnerCanvas = anchored + flyToOffset;
        return true;
    }

    private bool TryGetUiElementAnchoredOnSpawnerCanvas(RectTransform uiElement, out Vector2 anchoredOnSpawnerCanvas)
    {
        anchoredOnSpawnerCanvas = default;
        if (canvas == null) return false;
        if (!IsLiveRect(uiElement)) return false;

        int layer = uiElement.gameObject.layer;
        Camera preferred = GetPreferredCameraForUiElement(uiElement);
        if (!TryGetBestScreenPoint(uiElement.position, layer, preferred, out Vector3 screen3))
            return false;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return false;

        Vector2 screen = new Vector2(screen3.x, screen3.y);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screen,
                GetCanvasCamera(),
                out Vector2 anchored))
        {
            return false;
        }

        anchoredOnSpawnerCanvas = anchored;
        return true;
    }

    private bool TryGetCanvasCenterAnchoredPosition(out Vector2 anchoredOnSpawnerCanvas)
    {
        anchoredOnSpawnerCanvas = default;
        if (canvas == null) return false;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return false;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenCenter,
                GetCanvasCamera(),
                out Vector2 anchored))
        {
            return false;
        }

        anchoredOnSpawnerCanvas = anchored;
        return true;
    }

    private Camera GetPreferredCameraForUiElement(RectTransform uiElement)
    {
        if (uiElement == null)
            return Camera.main;

        Canvas targetCanvas = uiElement.GetComponentInParent<Canvas>();
        if (targetCanvas != null)
        {
            if (targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            if (targetCanvas.worldCamera != null)
                return targetCanvas.worldCamera;
        }

        return Camera.main;
    }

    private static bool TryGetBestScreenPoint(
        Vector3 worldPoint,
        int? targetLayer,
        Camera preferred,
        out Vector3 screenPos)
    {
        screenPos = default;

        if (preferred != null && CameraSeesWorldPoint(preferred, worldPoint, targetLayer))
        {
            screenPos = preferred.WorldToScreenPoint(worldPoint);
            return true;
        }

        Camera main = Camera.main;
        if (main != null && main != preferred && CameraSeesWorldPoint(main, worldPoint, targetLayer))
        {
            screenPos = main.WorldToScreenPoint(worldPoint);
            return true;
        }

        Camera[] cams = Camera.allCameras;
        if (cams == null || cams.Length == 0)
            return false;

        Camera best = null;
        float bestDepth = float.NegativeInfinity;
        for (int i = 0; i < cams.Length; i++)
        {
            Camera c = cams[i];
            if (c == null || !c.enabled) continue;
            if (c == preferred || c == main) continue;
            if (!CameraSeesWorldPoint(c, worldPoint, targetLayer)) continue;

            if (c.depth >= bestDepth)
            {
                bestDepth = c.depth;
                best = c;
            }
        }

        if (best == null)
            return false;

        screenPos = best.WorldToScreenPoint(worldPoint);
        return true;
    }

    private static bool CameraSeesWorldPoint(Camera c, Vector3 worldPoint, int? targetLayer)
    {
        if (c == null || !c.enabled)
            return false;

        if (targetLayer.HasValue)
        {
            int mask = 1 << targetLayer.Value;
            if ((c.cullingMask & mask) == 0)
                return false;
        }

        Vector3 vp = c.WorldToViewportPoint(worldPoint);
        if (vp.z <= 0f)
            return false;

        if (vp.x < 0f || vp.x > 1f)
            return false;

        if (vp.y < 0f || vp.y > 1f)
            return false;

        return true;
    }

    private IEnumerator PopInScaleRoutine(
        RectTransform rt,
        float baseScale,
        float startScaleMultiplier,
        float durationSeconds)
    {
        if (rt == null) yield break;

        float dur = Mathf.Max(0f, durationSeconds);
        float safeBase = Mathf.Max(0.0001f, baseScale);
        float start = safeBase * Mathf.Max(0.0001f, startScaleMultiplier);
        float end = safeBase;

        if (dur <= 0f)
        {
            rt.localScale = Vector3.one * end;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            if (rt == null) yield break;

            t += Time.unscaledDeltaTime / dur;
            float u = Mathf.Clamp01(t);
            float s = Smooth01(u);
            float scale = Mathf.Lerp(start, end, s);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }

        if (rt != null)
            rt.localScale = Vector3.one * end;
    }

    private static float Smooth01(float u)
    {
        return u * u * (3f - 2f * u);
    }
}
