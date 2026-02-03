using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Orchestrates the "enter shop" / "exit shop" transition for the additive-board architecture:
/// - Camera pans right (board appears to slide left)
/// - Shop screen-space UI slides in from right
/// - Board interactions are disabled while shop is open / during transition
///
/// Intended to live in GameplayCore (same scene as Main Camera + shop UI).
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopTransitionController : MonoBehaviour
{
    [Header("Camera pan")]
    [Tooltip("Parent transform of the Main Camera. This object is moved during the pan so camera-local effects (shake/alive motion) can keep working.")]
    [SerializeField] private Transform cameraRig;
    [SerializeField] private float panLocalX = 12f;
    [SerializeField] private float panDuration = 0.55f;
    [SerializeField] private AnimationCurve panEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Shop UI slide")]
    [Tooltip("Root GameObject to enable/disable for the shop. Usually the same object that GameRulesManager toggles.")]
    [SerializeField] private GameObject shopCanvasRoot;

    [Tooltip("The RectTransform that slides. If omitted, we will try to find it from a ShopUIController on the shopCanvasRoot.")]
    [SerializeField] private RectTransform shopPanelRect;

    [Tooltip("Extra padding beyond the panel width when hiding off-screen.")]
    [SerializeField] private float uiOffscreenPadding = 80f;

    [SerializeField] private float uiSlideDuration = 0.45f;
    [SerializeField] private AnimationCurve uiEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Board UI")]
    [Tooltip("Name of the board UI GameObject to find in loaded scenes.")]
    [SerializeField] private string boardUIName = "Board Canvas";
    private GameObject _boardUIRoot;

    [Header("Input lock (while shop open)")]
    [Tooltip("Disables PinballLauncher components while shop is open.")]
    [SerializeField] private bool disableLaunchers = true;

    [Tooltip("Disables PinballFlipper components while shop is open.")]
    [SerializeField] private bool disableFlippers = true;

    // "Home" == gameplay view (board centered). We cache it once and only refresh it while not in-shop.
    private bool _hasCachedHome;
    private Vector3 _cameraHomeLocalPos;
    private bool _hasCachedUiHome;
    private Vector2 _uiHomeAnchoredPos;

    private Vector3 _cameraShopLocalPos;
    private Vector2 _uiHiddenAnchoredPos;

    private bool _isOpen;
    private bool _isTransitioning;
    private Coroutine _transitionRoutine;

    // Tracks original enabled-state for any behaviour we disabled while locked.
    private readonly Dictionary<Behaviour, bool> _disabledInputBehaviours = new Dictionary<Behaviour, bool>();
    private bool _inputLocked;

    private void Awake()
    {
        AutoResolveRefs();
        CacheHomeIfNeeded(force: true);
        CacheUiHomeIfNeeded(force: true);
        RecomputeUiHiddenPos();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        AutoResolveRefs();
        CacheHomeIfNeeded(force: false);
        CacheUiHomeIfNeeded(force: false);
        RecomputeUiHiddenPos();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Clear cached board UI reference so we find the new one
        _boardUIRoot = null;

        // If boards are swapped additively while we're locked, disable newly-loaded controls too.
        if (_inputLocked)
        {
            LockGameplayInput();
        }
    }

    /// <summary>
    /// Begins the open transition (pan camera + slide UI in), and locks gameplay input.
    /// Safe to call repeatedly.
    /// </summary>
    public void OpenShop()
    {
        AutoResolveRefs();
        CacheHomeIfNeeded(force: false);

        if (_isTransitioning)
            return;

        if (_isOpen)
            return;

        _isOpen = true;
        _inputLocked = true;
        LockGameplayInput();

        // Hide board UI before camera pans to shop
        HideBoardUI();

        if (shopCanvasRoot != null && !shopCanvasRoot.activeSelf)
            shopCanvasRoot.SetActive(true);

        // Ensure layout has a chance to update so rect sizes are correct for offscreen positioning.
        Canvas.ForceUpdateCanvases();
        CacheUiHomeIfNeeded(force: false);
        RecomputeUiHiddenPos();

        // Start with the panel hidden off-screen.
        if (shopPanelRect != null)
            shopPanelRect.anchoredPosition = _uiHiddenAnchoredPos;

        StartTransition(OpenRoutine());
    }

    /// <summary>
    /// Begins the close transition (slide UI out + pan camera back).
    /// After the transition finishes, invokes <paramref name="afterClosed"/> (e.g. ContinueAfterShop).
    /// </summary>
    public void CloseShopThen(Action afterClosed)
    {
        AutoResolveRefs();
        // Do NOT recache home here; we want to return to the gameplay-home pose.
        CacheHomeIfNeeded(force: false);
        CacheUiHomeIfNeeded(force: false);
        RecomputeUiHiddenPos();

        if (_isTransitioning)
            return;

        if (!_isOpen)
        {
            afterClosed?.Invoke();
            return;
        }

        StartTransition(CloseRoutine(afterClosed));
    }

    /// <summary>
    /// Re-enables gameplay input that was disabled while the shop was open.
    /// Call this after the run flow has finished loading/swapping boards and is ready for input again.
    /// </summary>
    public void ResumeGameplayInput()
    {
        _inputLocked = false;
        UnlockGameplayInput();
    }

    /// <summary>
    /// Shows the board UI. Call this after round preview closes.
    /// </summary>
    public void ShowBoardUI()
    {
        FindBoardUI();
        if (_boardUIRoot != null)
            _boardUIRoot.SetActive(true);
    }

    /// <summary>
    /// Hides the board UI. Called automatically when opening shop.
    /// </summary>
    public void HideBoardUI()
    {
        FindBoardUI();
        if (_boardUIRoot != null)
            _boardUIRoot.SetActive(false);
    }

    private void FindBoardUI()
    {
        if (_boardUIRoot != null)
            return;

        if (string.IsNullOrEmpty(boardUIName))
            return;

        var go = GameObject.Find(boardUIName);
        if (go != null)
            _boardUIRoot = go;
    }

    private void StartTransition(IEnumerator routine)
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        _transitionRoutine = StartCoroutine(routine);
    }

    private IEnumerator OpenRoutine()
    {
        _isTransitioning = true;

        yield return Animate(
            fromCam: _cameraHomeLocalPos,
            toCam: _cameraShopLocalPos,
            fromUI: _uiHiddenAnchoredPos,
            toUI: _uiHomeAnchoredPos,
            duration: Mathf.Max(0.001f, Mathf.Max(panDuration, uiSlideDuration))
        );

        _isTransitioning = false;
    }

    private IEnumerator CloseRoutine(Action afterClosed)
    {
        _isTransitioning = true;

        yield return Animate(
            fromCam: cameraRig != null ? cameraRig.localPosition : _cameraShopLocalPos,
            toCam: _cameraHomeLocalPos,
            fromUI: shopPanelRect != null ? shopPanelRect.anchoredPosition : _uiHomeAnchoredPos,
            toUI: _uiHiddenAnchoredPos,
            duration: Mathf.Max(0.001f, Mathf.Max(panDuration, uiSlideDuration))
        );

        // Snap to hidden so the next open always starts from a known state.
        if (shopPanelRect != null)
            shopPanelRect.anchoredPosition = _uiHiddenAnchoredPos;

        if (shopCanvasRoot != null)
            shopCanvasRoot.SetActive(false);

        _isOpen = false;
        _isTransitioning = false;

        afterClosed?.Invoke();
    }

    private IEnumerator Animate(Vector3 fromCam, Vector3 toCam, Vector2 fromUI, Vector2 toUI, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float n = Mathf.Clamp01(t / duration);

            float camN = panEase != null ? panEase.Evaluate(n) : n;
            float uiN = uiEase != null ? uiEase.Evaluate(n) : n;

            if (cameraRig != null)
                cameraRig.localPosition = Vector3.LerpUnclamped(fromCam, toCam, camN);

            if (shopPanelRect != null)
                shopPanelRect.anchoredPosition = Vector2.LerpUnclamped(fromUI, toUI, uiN);

            yield return null;
        }

        if (cameraRig != null)
            cameraRig.localPosition = toCam;

        if (shopPanelRect != null)
            shopPanelRect.anchoredPosition = toUI;
    }

    private void LockGameplayInput()
    {
        if (!_inputLocked)
            return;

        if (disableLaunchers)
        {
            foreach (var launcher in FindAll<PinballLauncher>())
            {
                if (launcher == null) continue;
                TrackAndDisable(launcher);
            }
        }

        if (disableFlippers)
        {
            foreach (var flipper in FindAll<PinballFlipper>())
            {
                if (flipper == null) continue;
                TrackAndDisable(flipper);
            }
        }
    }

    private void UnlockGameplayInput()
    {
        foreach (var kvp in _disabledInputBehaviours)
        {
            Behaviour b = kvp.Key;
            if (b == null) continue;
            b.enabled = kvp.Value;
        }
        _disabledInputBehaviours.Clear();
    }

    private void TrackAndDisable(Behaviour b)
    {
        if (b == null)
            return;

        if (!_disabledInputBehaviours.ContainsKey(b))
            _disabledInputBehaviours.Add(b, b.enabled);

        b.enabled = false;
    }

    private static List<T> FindAll<T>() where T : UnityEngine.Object
    {
#if UNITY_2022_2_OR_NEWER
        var found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        var found = UnityEngine.Object.FindObjectsOfType<T>(includeInactive: false);
#endif
        if (found == null || found.Length == 0)
            return s_empty<T>();

        return new List<T>(found);
    }

    private static List<T> s_empty<T>()
    {
        return new List<T>(0);
    }

    private void AutoResolveRefs()
    {
        if (shopCanvasRoot == null)
        {
            var shop = FindFirstObjectByTypeCompat<ShopUIController>();
            if (shop != null)
            {
                shopCanvasRoot = shop.gameObject;
            }
        }

        if (shopPanelRect == null && shopCanvasRoot != null)
        {
            // Prefer the root RectTransform on the shop canvas root.
            shopPanelRect = shopCanvasRoot.GetComponent<RectTransform>();
        }

        if (cameraRig == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                // If the camera already has a parent, treat that as the rig.
                if (cam.transform.parent != null)
                {
                    cameraRig = cam.transform.parent;
                }
                else
                {
                    // Create a runtime rig so pan doesn't fight CameraShake (which writes camera.localPosition).
                    var rigGo = new GameObject("CameraRig (runtime)");
                    rigGo.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
                    rigGo.transform.localScale = Vector3.one;

                    cam.transform.SetParent(rigGo.transform, worldPositionStays: true);
                    cameraRig = rigGo.transform;
                }
            }
        }
    }

    private void CacheHomeIfNeeded(bool force)
    {
        // Only refresh the "home" pose while not in-shop (otherwise we'd learn the shop pose as home).
        bool canRefreshHome = !_isOpen && !_isTransitioning;
        if (!force && (!_hasCachedHome || !canRefreshHome))
        {
            // Even if we cannot refresh, still ensure shop targets are derived from the cached home.
            if (_hasCachedHome && cameraRig != null)
                _cameraShopLocalPos = _cameraHomeLocalPos + new Vector3(panLocalX, 0f, 0f);
            return;
        }

        if (cameraRig != null)
        {
            _cameraHomeLocalPos = cameraRig.localPosition;
            _cameraShopLocalPos = _cameraHomeLocalPos + new Vector3(panLocalX, 0f, 0f);
        }

        _hasCachedHome = true;
    }

    private void CacheUiHomeIfNeeded(bool force)
    {
        if (shopPanelRect == null)
            return;

        if (!force && _hasCachedUiHome)
            return;

        // Capture the "open" position ONCE (typically its design-time anchoredPosition in the scene).
        // Important: do NOT keep re-caching this at runtime because after we close we park the panel off-screen.
        _uiHomeAnchoredPos = shopPanelRect.anchoredPosition;
        _hasCachedUiHome = true;
    }

    private void RecomputeUiHiddenPos()
    {
        if (shopPanelRect == null)
            return;

        float width = shopPanelRect.rect.width;
        if (width <= 0.01f)
        {
            // Try to force layout, then re-read.
            LayoutRebuilder.ForceRebuildLayoutImmediate(shopPanelRect);
            Canvas.ForceUpdateCanvases();
            width = shopPanelRect.rect.width;
        }
        if (width <= 0.01f)
        {
            // Fallback: approximate using screen width in canvas units.
            width = Mathf.Max(800f, Screen.width);
        }

        float hiddenX = width + Mathf.Max(0f, uiOffscreenPadding);
        _uiHiddenAnchoredPos = _uiHomeAnchoredPos + Vector2.right * hiddenX;
    }

    private static T FindFirstObjectByTypeCompat<T>() where T : UnityEngine.Object
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>();
#else
        return UnityEngine.Object.FindObjectOfType<T>();
#endif
    }
}

