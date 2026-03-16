using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

    public event Action OpenTransitionFinished;
    /// <summary>Fires when the camera pan completes (before panel slides). Use for Main panel reveal.</summary>
    public event Action CameraPanFinished;

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
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        AutoResolveRefs();
        CacheHomeIfNeeded(force: false);
        CachePanelHomesIfNeeded(force: false);
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
        CachePanelHomesIfNeeded(force: true);
        EnsurePanelsActive();
        SnapPanelsToHome();

        StartTransition(OpenRoutine());
    }

    /// <summary>
    /// Begins the close transition (slide UI out + pan camera back).
    /// After the transition finishes, invokes <paramref name="afterClosed"/> (e.g. ContinueAfterShop).
    /// </summary>
    public void CloseShopThen(Action afterClosed)
    {
        AutoResolveRefs();
        CacheHomeIfNeeded(force: false);

        if (_isTransitioning)
            return;

        if (!_isOpen)
        {
            afterClosed?.Invoke();
            return;
        }

        AudioManager.Instance.PlayTransition();

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

        // Wait one frame for layout to settle after activation (Unity may return zero rects in the same frame).
        yield return null;

        // Camera pans to shop view. Panels are already at home (no slide-in animation).
        AudioManager.Instance.PlayTransition();
        yield return AnimateCamera(
            fromCam: _cameraHomeLocalPos,
            toCam: _cameraShopLocalPos,
            duration: Mathf.Max(0.001f, panDuration)
        );

        CameraPanFinished?.Invoke();

        _isTransitioning = false;
        OpenTransitionFinished?.Invoke();
    }

    private IEnumerator CloseRoutine(Action afterClosed)
    {
        _isTransitioning = true;

        if (AudioManager.Instance != null) AudioManager.Instance.SetMusicMuffled(false);

        // Hide shop UI before camera pans so the board is visible during the transition.
        if (shopCanvasRoot != null)
            shopCanvasRoot.SetActive(false);

        // Camera pans back to the board (no panel slide-out animation).
        yield return AnimateCamera(
            fromCam: cameraRig != null ? cameraRig.localPosition : _cameraShopLocalPos,
            toCam: _cameraHomeLocalPos,
            duration: Mathf.Max(0.001f, panDuration)
        );

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

    private void EnsurePanelsActive()
    {
        if (panelTitle != null && !panelTitle.gameObject.activeSelf) panelTitle.gameObject.SetActive(true);
        if (panelTabs != null && !panelTabs.gameObject.activeSelf) panelTabs.gameObject.SetActive(true);
        if (panelMoney != null && !panelMoney.gameObject.activeSelf) panelMoney.gameObject.SetActive(true);
        // Do NOT activate panelGumball or panelBalls here — they are tab content managed by ShopTabsController.
        // Activating them would show the Balls panel during the camera transition instead of waiting for Main panel.
        if (panelDone != null && !panelDone.gameObject.activeSelf) panelDone.gameObject.SetActive(true);
    }

    private void SnapPanelsToHome()
    {
        if (panelTitle != null) panelTitle.anchoredPosition = _panelTitleHome;
        if (panelTabs != null) panelTabs.anchoredPosition = _panelTabsHome;
        if (panelMoney != null) panelMoney.anchoredPosition = _panelMoneyHome;
        if (panelGumball != null) panelGumball.anchoredPosition = _panelGumballHome;
        if (panelBalls != null) panelBalls.anchoredPosition = _panelBallsHome;
        if (panelDone != null) panelDone.anchoredPosition = _panelDoneHome;
    }

    private void EnsureShopLayout()
    {
        if (shopPanelRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(shopPanelRect);
        }
        Canvas.ForceUpdateCanvases();
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



