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

    [Tooltip("Root RectTransform of the shop UI. Used for layout/offscreen calculations.")]
    [SerializeField] private RectTransform shopPanelRect;

    [Header("Shop panels (animated)")]
    [Tooltip("Slides from top.")]
    [SerializeField] private RectTransform panelTitle;
    [Tooltip("Slides from top.")]
    [SerializeField] private RectTransform panelTabs;
    [Tooltip("Slides from top.")]
    [SerializeField] private RectTransform panelMoney;
    [Tooltip("Slides from left.")]
    [SerializeField] private RectTransform panelGumball;
    [Tooltip("Slides from right.")]
    [SerializeField] private RectTransform panelBalls;
    [Tooltip("Slides from right.")]
    [SerializeField] private RectTransform panelDone;

    [Tooltip("Extra padding beyond the panel width when hiding off-screen.")]
    [SerializeField] private float uiOffscreenPadding = 80f;

    [SerializeField] private float uiSlideDuration = 0.45f;
    [Tooltip("Total time for Phase 1 (Title -> Tabs -> Money). If <= 0, falls back to Ui Slide Duration.")]
    [SerializeField] private float phase1TotalDuration = 0f;

    [Tooltip("Total time for Phase 2 (Gumball -> Balls). If <= 0, falls back to Ui Slide Duration.")]
    [SerializeField] private float phase2TotalDuration = 0f;

    [Tooltip("Time for Phase 3 (Done panel). If <= 0, falls back to Ui Slide Duration.")]
    [SerializeField] private float phase3Duration = 0f;

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

    private Vector3 _cameraShopLocalPos;

    private bool _hasCachedPanelHomes;
    private Vector2 _panelTitleHome;
    private Vector2 _panelTabsHome;
    private Vector2 _panelMoneyHome;
    private Vector2 _panelGumballHome;
    private Vector2 _panelBallsHome;
    private Vector2 _panelDoneHome;

    private Vector2 _panelTitleHidden;
    private Vector2 _panelTabsHidden;
    private Vector2 _panelMoneyHidden;
    private Vector2 _panelGumballHidden;
    private Vector2 _panelBallsHidden;
    private Vector2 _panelDoneHidden;

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
        CachePanelHomesIfNeeded(force: true);
        RecomputePanelHiddenPositions();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        AutoResolveRefs();
        CacheHomeIfNeeded(force: false);
        CachePanelHomesIfNeeded(force: false);
        RecomputePanelHiddenPositions();
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

        EnsureShopLayout();
        CachePanelHomesIfNeeded(force: false);
        RecomputePanelHiddenPositions();
        SnapPanelsToHidden();

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
        EnsureShopLayout();
        CachePanelHomesIfNeeded(force: false);
        RecomputePanelHiddenPositions();

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

        // Camera pans first while the shop panels remain hidden.
        yield return AnimateCamera(
            fromCam: _cameraHomeLocalPos,
            toCam: _cameraShopLocalPos,
            duration: Mathf.Max(0.001f, panDuration)
        );

        // Then panels slide in, in the requested order.
        yield return SlidePanelsInSequence();

        _isTransitioning = false;
    }

    private IEnumerator CloseRoutine(Action afterClosed)
    {
        _isTransitioning = true;

        // Panels exit first (opposite direction of their entry), then camera pans back to the board.
        yield return SlidePanelsOutAll();

        yield return AnimateCamera(
            fromCam: cameraRig != null ? cameraRig.localPosition : _cameraShopLocalPos,
            toCam: _cameraHomeLocalPos,
            duration: Mathf.Max(0.001f, panDuration)
        );

        if (shopCanvasRoot != null)
            shopCanvasRoot.SetActive(false);

        _isOpen = false;
        _isTransitioning = false;

        afterClosed?.Invoke();
    }

    private IEnumerator AnimateCamera(Vector3 fromCam, Vector3 toCam, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float n = Mathf.Clamp01(t / duration);

            float camN = panEase != null ? panEase.Evaluate(n) : n;

            if (cameraRig != null)
                cameraRig.localPosition = Vector3.LerpUnclamped(fromCam, toCam, camN);

            yield return null;
        }

        if (cameraRig != null)
            cameraRig.localPosition = toCam;
    }

    private IEnumerator AnimatePanel(RectTransform panel, Vector2 from, Vector2 to, float duration)
    {
        if (panel == null)
            yield break;

        float d = Mathf.Max(0.001f, duration);
        float t = 0f;
        while (t < d)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            float n = Mathf.Clamp01(t / d);

            float uiN = uiEase != null ? uiEase.Evaluate(n) : n;
            panel.anchoredPosition = Vector2.LerpUnclamped(from, to, uiN);

            yield return null;
        }

        panel.anchoredPosition = to;
    }

    private IEnumerator SlidePanelsInSequence()
    {
        const int phase1PanelCount = 3;
        const int phase2PanelCount = 2;

        float fallback = Mathf.Max(0.001f, uiSlideDuration);

        float p1Total = phase1TotalDuration > 0f ? phase1TotalDuration : fallback;
        float p2Total = phase2TotalDuration > 0f ? phase2TotalDuration : fallback;
        float p3 = phase3Duration > 0f ? phase3Duration : fallback;

        float phase1 = Mathf.Max(0.001f, p1Total / phase1PanelCount);
        float phase2 = Mathf.Max(0.001f, p2Total / phase2PanelCount);
        float final = Mathf.Max(0.001f, p3);

        yield return AnimatePanel(panelTitle, _panelTitleHidden, _panelTitleHome, phase1);
        yield return AnimatePanel(panelTabs, _panelTabsHidden, _panelTabsHome, phase1);
        yield return AnimatePanel(panelMoney, _panelMoneyHidden, _panelMoneyHome, phase1);

        yield return AnimatePanel(panelGumball, _panelGumballHidden, _panelGumballHome, phase2);
        yield return AnimatePanel(panelBalls, _panelBallsHidden, _panelBallsHome, phase2);

        yield return AnimatePanel(panelDone, _panelDoneHidden, _panelDoneHome, final);
    }

    private IEnumerator SlidePanelsOutAll()
    {
        const int phase1PanelCount = 3;
        const int phase2PanelCount = 2;

        float fallback = Mathf.Max(0.001f, uiSlideDuration);

        float p1Total = phase1TotalDuration > 0f ? phase1TotalDuration : fallback;
        float p2Total = phase2TotalDuration > 0f ? phase2TotalDuration : fallback;
        float p3 = phase3Duration > 0f ? phase3Duration : fallback;

        float phase1 = Mathf.Max(0.001f, p1Total / phase1PanelCount);
        float phase2 = Mathf.Max(0.001f, p2Total / phase2PanelCount);
        float final = Mathf.Max(0.001f, p3);

        Vector2 titleFrom = panelTitle != null ? panelTitle.anchoredPosition : _panelTitleHome;
        Vector2 tabsFrom = panelTabs != null ? panelTabs.anchoredPosition : _panelTabsHome;
        Vector2 moneyFrom = panelMoney != null ? panelMoney.anchoredPosition : _panelMoneyHome;
        Vector2 gumballFrom = panelGumball != null ? panelGumball.anchoredPosition : _panelGumballHome;
        Vector2 ballsFrom = panelBalls != null ? panelBalls.anchoredPosition : _panelBallsHome;
        Vector2 doneFrom = panelDone != null ? panelDone.anchoredPosition : _panelDoneHome;

        if (panelTitle != null) StartCoroutine(AnimatePanel(panelTitle, titleFrom, _panelTitleHidden, phase1));
        if (panelTabs != null) StartCoroutine(AnimatePanel(panelTabs, tabsFrom, _panelTabsHidden, phase1));
        if (panelMoney != null) StartCoroutine(AnimatePanel(panelMoney, moneyFrom, _panelMoneyHidden, phase1));
        if (panelGumball != null) StartCoroutine(AnimatePanel(panelGumball, gumballFrom, _panelGumballHidden, phase2));
        if (panelBalls != null) StartCoroutine(AnimatePanel(panelBalls, ballsFrom, _panelBallsHidden, phase2));
        if (panelDone != null) StartCoroutine(AnimatePanel(panelDone, doneFrom, _panelDoneHidden, final));

        float maxDuration = Mathf.Max(phase1, Mathf.Max(phase2, final));
        float t = 0f;
        while (t < maxDuration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        SnapPanelsToHidden();
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
            var shop = FindFirstObjectByTypeCompat<ShopUIController>(includeInactive: true);
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

        if (shopCanvasRoot != null)
        {
            Transform root = shopCanvasRoot.transform;
            if (panelTitle == null) panelTitle = FindRectTransformByName(root, "Panel_Title");
            if (panelTabs == null) panelTabs = FindRectTransformByName(root, "Panel_Tabs");
            if (panelMoney == null) panelMoney = FindRectTransformByName(root, "Panel_Money");
            if (panelGumball == null) panelGumball = FindRectTransformByName(root, "Panel_Gumball");
            if (panelBalls == null) panelBalls = FindRectTransformByName(root, "Panel_Balls");
            if (panelDone == null) panelDone = FindRectTransformByName(root, "Panel_Done");
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

    private void CachePanelHomesIfNeeded(bool force)
    {
        AutoResolveRefs();

        if (!force && _hasCachedPanelHomes)
            return;

        if (panelTitle != null) _panelTitleHome = panelTitle.anchoredPosition;
        if (panelTabs != null) _panelTabsHome = panelTabs.anchoredPosition;
        if (panelMoney != null) _panelMoneyHome = panelMoney.anchoredPosition;
        if (panelGumball != null) _panelGumballHome = panelGumball.anchoredPosition;
        if (panelBalls != null) _panelBallsHome = panelBalls.anchoredPosition;
        if (panelDone != null) _panelDoneHome = panelDone.anchoredPosition;

        _hasCachedPanelHomes =
            panelTitle != null &&
            panelTabs != null &&
            panelMoney != null &&
            panelGumball != null &&
            panelBalls != null &&
            panelDone != null;
    }

    private void RecomputePanelHiddenPositions()
    {
        AutoResolveRefs();
        EnsureShopLayout();

        RectTransform boundsRect = shopPanelRect != null ? shopPanelRect : (panelTitle != null ? panelTitle.parent as RectTransform : null);
        float boundsWidth = GetSafeRectWidth(boundsRect);
        float boundsHeight = GetSafeRectHeight(boundsRect);

        float pad = Mathf.Max(0f, uiOffscreenPadding);

        float titleUp = GetHiddenOffsetY(boundsHeight, GetSafeRectHeight(panelTitle), pad);
        float tabsUp = GetHiddenOffsetY(boundsHeight, GetSafeRectHeight(panelTabs), pad);
        float moneyUp = GetHiddenOffsetY(boundsHeight, GetSafeRectHeight(panelMoney), pad);

        float gumballLeft = GetHiddenOffsetX(boundsWidth, GetSafeRectWidth(panelGumball), pad);
        float ballsRight = GetHiddenOffsetX(boundsWidth, GetSafeRectWidth(panelBalls), pad);
        float doneRight = GetHiddenOffsetX(boundsWidth, GetSafeRectWidth(panelDone), pad);

        _panelTitleHidden = _panelTitleHome + Vector2.up * titleUp;
        _panelTabsHidden = _panelTabsHome + Vector2.up * tabsUp;
        _panelMoneyHidden = _panelMoneyHome + Vector2.up * moneyUp;

        _panelGumballHidden = _panelGumballHome + Vector2.left * gumballLeft;
        _panelBallsHidden = _panelBallsHome + Vector2.right * ballsRight;
        _panelDoneHidden = _panelDoneHome + Vector2.right * doneRight;
    }

    private void SnapPanelsToHidden()
    {
        if (panelTitle != null) panelTitle.anchoredPosition = _panelTitleHidden;
        if (panelTabs != null) panelTabs.anchoredPosition = _panelTabsHidden;
        if (panelMoney != null) panelMoney.anchoredPosition = _panelMoneyHidden;
        if (panelGumball != null) panelGumball.anchoredPosition = _panelGumballHidden;
        if (panelBalls != null) panelBalls.anchoredPosition = _panelBallsHidden;
        if (panelDone != null) panelDone.anchoredPosition = _panelDoneHidden;
    }

    private void EnsureShopLayout()
    {
        if (shopPanelRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(shopPanelRect);
        }
        Canvas.ForceUpdateCanvases();
    }

    private static float GetSafeRectWidth(RectTransform rt)
    {
        if (rt == null)
        {
            return Mathf.Max(800f, Screen.width);
        }

        float w = rt.rect.width;
        if (w > 0.01f)
        {
            return w;
        }

        return Mathf.Max(800f, Screen.width);
    }

    private static float GetSafeRectHeight(RectTransform rt)
    {
        if (rt == null)
        {
            return Mathf.Max(600f, Screen.height);
        }

        float h = rt.rect.height;
        if (h > 0.01f)
        {
            return h;
        }

        return Mathf.Max(600f, Screen.height);
    }

    private static float GetHiddenOffsetX(float boundsWidth, float panelWidth, float padding)
    {
        float w = boundsWidth > 0.01f ? boundsWidth : Mathf.Max(800f, Screen.width);
        float p = panelWidth > 0.01f ? panelWidth : w * 0.5f;
        return (w * 0.5f) + (p * 0.5f) + padding;
    }

    private static float GetHiddenOffsetY(float boundsHeight, float panelHeight, float padding)
    {
        float h = boundsHeight > 0.01f ? boundsHeight : Mathf.Max(600f, Screen.height);
        float p = panelHeight > 0.01f ? panelHeight : h * 0.5f;
        return (h * 0.5f) + (p * 0.5f) + padding;
    }

    private static RectTransform FindRectTransformByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        RectTransform[] all = root.GetComponentsInChildren<RectTransform>(includeInactive: true);
        for (int i = 0; i < all.Length; i++)
        {
            RectTransform rt = all[i];
            if (rt != null && rt.name == targetName)
            {
                return rt;
            }
        }

        return null;
    }

    private static T FindFirstObjectByTypeCompat<T>(bool includeInactive) where T : UnityEngine.Object
    {
#if UNITY_2022_2_OR_NEWER
        if (includeInactive)
        {
            return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }

        return UnityEngine.Object.FindFirstObjectByType<T>();
#else
        return UnityEngine.Object.FindObjectOfType<T>(includeInactive: includeInactive);
#endif
    }
}

