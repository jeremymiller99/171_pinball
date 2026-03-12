// Generated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-24.
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[DisallowMultipleComponent]
public sealed class ModifierCardPanelController : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler,
    IPointerExitHandler, IPointerMoveHandler
{
    private const string CardRootName = "Modifier Card";
    private const string LegacyBgObjectName = "BG";
    private const string NormalBgObjectName = "Normal BG";
    private const string AngelBgObjectName = "Angel BG";
    private const string DevilBgObjectName = "Devil BG";
    private const string AngelCardSpriteName = "Angel Card";
    private const string DevilCardSpriteName = "Devil Card";
    private const string LevelNumObjectName = "Level Num";
    private const string ModNameObjectName = "Mod Name";
    private const string DescriptionObjectName = "Description";

    [Header("Card Source (optional)")]
    [Tooltip("Optional: If the panel has no child named 'Modifier Card', this prefab will be instantiated as the card contents.")]
    [SerializeField] private GameObject modifierCardPrefab;

    [Header("Visibility")]
    [SerializeField] private bool closeOnAnyClickOrKey = true;
    [SerializeField] private bool deactivateGameObjectWhenHidden = true;

    [Header("Render Order")]
    [Tooltip("If enabled, adds/uses a local Canvas with overrideSorting to ensure this panel renders above other UI like floating text.")]
    [SerializeField] private bool forceTopmostUi = true;
    [SerializeField] private int topmostSortingOrder = 5000;

    [Header("Dismiss Timing")]
    [Min(0f)]
    [SerializeField] private float minSecondsBeforeDismiss = 1f;

    [Header("Animation")]
    [Min(0.01f)]
    [SerializeField] private float flyInDurationSeconds = 0.45f;
    [SerializeField] private Vector2 flyInOffset = new Vector2(0f, -280f);
    [Range(0.01f, 1f)]
    [SerializeField] private float flyInStartScale = 0.82f;
    [Range(0f, 1f)]
    [SerializeField] private float flyInStartAlpha = 0f;

    [Header("Card Feel")]
    [SerializeField] private bool enableFloating = true;
    [SerializeField] private float floatAmplitudePixels = 8f;
    [SerializeField] private float floatFrequencyHz = 0.7f;

    [SerializeField] private bool enableHoverTilt = true;
    [Range(0f, 25f)]
    [SerializeField] private float tiltMaxDegrees = 10f;
    [Min(0f)]
    [SerializeField] private float tiltSmoothing = 14f;

    [Header("UI (auto-resolved if blank)")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text levelNumText;
    [SerializeField] private TMP_Text modNameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Runtime (debug)")]
    [SerializeField] private bool isVisible;

    private Transform _cardRoot;
    private GameObject _normalBgObject;
    private GameObject _angelBgObject;
    private GameObject _devilBgObject;
    private Image _legacyBgImage;
    private Sprite _legacyAngelSprite;
    private Sprite _legacyDevilSprite;
    private RectTransform _cardRect;
    private Canvas _parentCanvas;
    private Camera _uiCamera;
    private Canvas _overrideCanvas;
    private GraphicRaycaster _overrideRaycaster;

    private bool _cardBaseCaptured;
    private Vector2 _cardBaseAnchoredPos;
    private Quaternion _cardBaseLocalRotation;
    private Vector3 _cardBaseLocalScale;

    private Coroutine _flyInRoutine;
    private float _canDismissAtUnscaledTime;
    private bool _pointerOver;
    private Vector2 _lastPointerScreenPos;

    private float _timeScaleBeforePause = 1f;
    private float _fixedDeltaBeforePause = 0.02f;
    private bool _pausedByThisPanel;
    private int _shownAtFrame = -1;
    private Button[] _cachedButtons;

    private void Awake()
    {
        EnsureInitialized();
        CacheButtons();
        HideImmediate();
    }

    private void OnDisable()
    {
        if (isVisible)
        {
            Unpause();
        }
    }

    private void Update()
    {
        if (isVisible)
        {
            EnforcePausedIfNeeded();
            UpdateCardFeel();
        }

        if (!isVisible || !closeOnAnyClickOrKey)
        {
            return;
        }

        if (Time.frameCount == _shownAtFrame)
        {
            return;
        }

        if (WasAnyInputPressedThisFrame())
        {
            RequestHide();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isVisible)
        {
            return;
        }

        if (Time.frameCount == _shownAtFrame)
        {
            return;
        }

        RequestHide();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerOver = true;
        if (eventData != null)
        {
            _lastPointerScreenPos = eventData.position;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerOver = false;
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        _lastPointerScreenPos = eventData.position;
    }

    public void Show(RoundData roundData, int levelIndex)
    {
        if (roundData == null)
        {
            return;
        }

        EnsureInitialized();
        CacheButtons();

        if (levelNumText != null)
        {
            levelNumText.text = $"Level {Mathf.Max(0, levelIndex) + 1}";
        }

        if (modNameText != null)
        {
            modNameText.text = roundData.type == RoundType.Normal ? "No Modifier" : roundData.GetModifierDisplayName();
        }

        if (descriptionText != null)
        {
            descriptionText.text = roundData.type == RoundType.Normal ? "N/A" : roundData.GetModifierDescription();
        }

        ApplyBgForRoundType(roundData.type);
        ShowVisuals();
        Pause();
    }

    public void RequestHide()
    {
        if (!isVisible)
        {
            return;
        }

        if (Time.unscaledTime < _canDismissAtUnscaledTime)
        {
            return;
        }

        Hide();
    }

    public void Hide()
    {
        EnsureInitialized();
        StopFlyInAnimation();
        HideVisuals();
        if (_pausedByThisPanel)
        {
            Unpause();
        }

        if (deactivateGameObjectWhenHidden)
        {
            gameObject.SetActive(false);
        }
    }

    private void ShowVisuals()
    {
        if (canvasGroup == null)
        {
            return;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        isVisible = true;
        _shownAtFrame = Time.frameCount;
        _canDismissAtUnscaledTime = Time.unscaledTime + Mathf.Max(0f, minSecondsBeforeDismiss);

        canvasGroup.alpha = Mathf.Clamp01(flyInStartAlpha);
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        StartFlyInAnimation();
    }

    private void HideVisuals()
    {
        if (canvasGroup == null)
        {
            return;
        }

        isVisible = false;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void HideImmediate()
    {
        EnsureCanvasGroup();
        EnsureInitialized();
        StopFlyInAnimation();
        HideVisuals();

        if (deactivateGameObjectWhenHidden)
        {
            gameObject.SetActive(false);
        }
    }

    private void Pause()
    {
        if (Time.timeScale <= 0f)
        {
            return;
        }

        _timeScaleBeforePause = Time.timeScale;
        _fixedDeltaBeforePause = Time.fixedDeltaTime;

        Time.timeScale = 0f;
        _pausedByThisPanel = true;
    }

    private void Unpause()
    {
        float restored = Mathf.Max(0f, _timeScaleBeforePause);
        if (restored <= 0f)
        {
            restored = 1f;
        }

        Time.timeScale = restored;
        Time.fixedDeltaTime = Mathf.Max(0.0001f, _fixedDeltaBeforePause);
        _pausedByThisPanel = false;
    }

    private void EnforcePausedIfNeeded()
    {
        if (!_pausedByThisPanel)
        {
            return;
        }

        if (!Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 0f;
        }
    }

    private void EnsureInitialized()
    {
        EnsureCanvasGroup();

        if (_cardRoot == null)
        {
            _cardRoot = FindChildRecursive(transform, CardRootName);
        }

        if (_cardRoot == null && modifierCardPrefab != null)
        {
            var instance = Instantiate(modifierCardPrefab, transform, worldPositionStays: false);
            instance.name = CardRootName;
            _cardRoot = instance.transform;
        }

        if (_cardRoot == null)
        {
            _cardRoot = transform;
        }

        if (_cardRect == null)
        {
            _cardRect = _cardRoot as RectTransform;
        }

        if (_parentCanvas == null)
        {
            _parentCanvas = GetComponentInParent<Canvas>();
            _uiCamera = _parentCanvas != null ? _parentCanvas.worldCamera : null;
        }

        EnsureTopmostCanvasIfNeeded();

        if (_cardRect != null && !_cardBaseCaptured)
        {
            _cardBaseCaptured = true;
            _cardBaseAnchoredPos = _cardRect.anchoredPosition;
            _cardBaseLocalRotation = _cardRect.localRotation;
            _cardBaseLocalScale = _cardRect.localScale;
        }

        if (_normalBgObject == null)
        {
            Transform normal = FindChildRecursive(_cardRoot, NormalBgObjectName);
            if (normal != null)
            {
                _normalBgObject = normal.gameObject;
            }
        }

        if (_angelBgObject == null)
        {
            Transform angel = FindChildRecursive(_cardRoot, AngelBgObjectName);
            if (angel != null)
            {
                _angelBgObject = angel.gameObject;
            }
        }

        if (_devilBgObject == null)
        {
            Transform devil = FindChildRecursive(_cardRoot, DevilBgObjectName);
            if (devil != null)
            {
                _devilBgObject = devil.gameObject;
            }
        }

        if (_legacyBgImage == null && (_angelBgObject == null || _devilBgObject == null))
        {
            Transform bg = FindChildRecursive(_cardRoot, LegacyBgObjectName);
            if (bg != null)
            {
                _legacyBgImage = bg.GetComponent<Image>();
            }
        }

        if (_legacyBgImage != null && (_legacyAngelSprite == null || _legacyDevilSprite == null))
        {
            TryAutoResolveLegacyBgSprites();
        }

        if (levelNumText == null)
        {
            levelNumText = FindTmpTextByName(_cardRoot, LevelNumObjectName);
        }

        if (modNameText == null)
        {
            modNameText = FindTmpTextByName(_cardRoot, ModNameObjectName);
        }

        if (descriptionText == null)
        {
            descriptionText = FindTmpTextByName(_cardRoot, DescriptionObjectName);
        }
    }

    private void EnsureTopmostCanvasIfNeeded()
    {
        if (!forceTopmostUi)
        {
            return;
        }

        if (_overrideCanvas == null)
        {
            _overrideCanvas = GetComponent<Canvas>();
        }

        if (_overrideCanvas == null)
        {
            _overrideCanvas = gameObject.AddComponent<Canvas>();
        }

        _overrideCanvas.overrideSorting = true;
        _overrideCanvas.sortingOrder = topmostSortingOrder;

        if (_parentCanvas != null)
        {
            _overrideCanvas.sortingLayerID = _parentCanvas.sortingLayerID;
        }

        if (_overrideRaycaster == null)
        {
            _overrideRaycaster = GetComponent<GraphicRaycaster>();
        }

        if (_overrideRaycaster == null)
        {
            _overrideRaycaster = gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void CacheButtons()
    {
        if (_cachedButtons != null && _cachedButtons.Length > 0)
        {
            return;
        }

        _cachedButtons = GetComponentsInChildren<Button>(includeInactive: true);
        for (int i = 0; i < _cachedButtons.Length; i++)
        {
            Button b = _cachedButtons[i];
            if (b == null)
            {
                continue;
            }

            b.onClick.RemoveListener(RequestHide);
            b.onClick.AddListener(RequestHide);
        }
    }

    private void ApplyBgForRoundType(RoundType type)
    {
        if (_normalBgObject != null)
        {
            _normalBgObject.SetActive(type == RoundType.Normal);
        }

        if (_angelBgObject != null && _devilBgObject != null)
        {
            _angelBgObject.SetActive(type == RoundType.Angel);
            _devilBgObject.SetActive(type == RoundType.Devil);
            return;
        }

        if (_normalBgObject != null)
        {
            return;
        }

        if (_legacyBgImage != null)
        {
            _legacyBgImage.enabled = true;

            if (type == RoundType.Angel && _legacyAngelSprite != null)
            {
                _legacyBgImage.sprite = _legacyAngelSprite;
            }
            else if (type == RoundType.Devil && _legacyDevilSprite != null)
            {
                _legacyBgImage.sprite = _legacyDevilSprite;
            }
        }
    }

    private void TryAutoResolveLegacyBgSprites()
    {
        if (_legacyBgImage != null && _legacyAngelSprite == null && _legacyBgImage.sprite != null)
        {
            _legacyAngelSprite = _legacyBgImage.sprite;
        }

        if (_legacyAngelSprite != null && _legacyDevilSprite != null)
        {
            return;
        }

        Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < allSprites.Length; i++)
        {
            Sprite s = allSprites[i];
            if (s == null)
            {
                continue;
            }

            if (_legacyAngelSprite == null &&
                string.Equals(s.name, AngelCardSpriteName, StringComparison.OrdinalIgnoreCase))
            {
                _legacyAngelSprite = s;
                continue;
            }

            if (_legacyDevilSprite == null &&
                string.Equals(s.name, DevilCardSpriteName, StringComparison.OrdinalIgnoreCase))
            {
                _legacyDevilSprite = s;
            }

            if (_legacyAngelSprite != null && _legacyDevilSprite != null)
            {
                return;
            }
        }
    }

    private static TMP_Text FindTmpTextByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        TMP_Text[] all = root.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        for (int i = 0; i < all.Length; i++)
        {
            TMP_Text t = all[i];
            if (t == null)
            {
                continue;
            }

            if (string.Equals(t.gameObject.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindChildRecursive(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool WasAnyInputPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame))
        {
            return true;
        }

        if (Touchscreen.current != null)
        {
            if (Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }

            foreach (TouchControl t in Touchscreen.current.touches)
            {
                if (t != null && t.press.wasPressedThisFrame)
                {
                    return true;
                }
            }
        }

        if (Gamepad.current != null)
        {
            foreach (InputControl c in Gamepad.current.allControls)
            {
                if (c is ButtonControl b && b.wasPressedThisFrame)
                {
                    return true;
                }
            }
        }

        return false;
#else
        // Legacy input fallback (only used if old Input system is enabled).
        return UnityEngine.Input.anyKeyDown ||
               UnityEngine.Input.GetMouseButtonDown(0) ||
               UnityEngine.Input.GetMouseButtonDown(1) ||
               UnityEngine.Input.GetMouseButtonDown(2);
#endif
    }

    private void UpdateCardFeel()
    {
        if (_cardRect == null || !_cardBaseCaptured)
        {
            return;
        }

        if (_flyInRoutine != null)
        {
            return;
        }

        float t = Time.unscaledTime;

        Vector2 pos = _cardBaseAnchoredPos;
        if (enableFloating)
        {
            float amp = Mathf.Max(0f, floatAmplitudePixels);
            float freq = Mathf.Max(0f, floatFrequencyHz);
            if (amp > 0.01f && freq > 0.001f)
            {
                pos.y += Mathf.Sin(t * (Mathf.PI * 2f) * freq) * amp;
            }
        }

        _cardRect.anchoredPosition = pos;

        if (!enableHoverTilt)
        {
            _cardRect.localRotation = _cardBaseLocalRotation;
            return;
        }

        Vector2 pointer = GetPointerScreenPosition();
        float targetX = 0f;
        float targetY = 0f;
        if (_pointerOver && _parentCanvas != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _cardRect,
                    pointer,
                    _uiCamera,
                    out Vector2 local))
            {
                Rect r = _cardRect.rect;
                float nx = r.width > 0.01f ? Mathf.Clamp(local.x / (r.width * 0.5f), -1f, 1f) : 0f;
                float ny = r.height > 0.01f ? Mathf.Clamp(local.y / (r.height * 0.5f), -1f, 1f) : 0f;
                float max = Mathf.Max(0f, tiltMaxDegrees);
                targetX = -ny * max;
                targetY = nx * max;
            }
        }

        Quaternion targetRot = _cardBaseLocalRotation * Quaternion.Euler(targetX, targetY, 0f);
        float smooth = Mathf.Max(0f, tiltSmoothing);
        float lerp = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
        _cardRect.localRotation = Quaternion.Slerp(_cardRect.localRotation, targetRot, lerp);
    }

    private Vector2 GetPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }

        if (Touchscreen.current != null)
        {
            return Touchscreen.current.primaryTouch.position.ReadValue();
        }
#endif
        return _lastPointerScreenPos;
    }

    private void StartFlyInAnimation()
    {
        if (_cardRect == null || !_cardBaseCaptured)
        {
            return;
        }

        StopFlyInAnimation();

        _cardRect.anchoredPosition = _cardBaseAnchoredPos + flyInOffset;
        _cardRect.localScale = _cardBaseLocalScale * Mathf.Clamp(flyInStartScale, 0.01f, 10f);
        _cardRect.localRotation = _cardBaseLocalRotation;

        _flyInRoutine = StartCoroutine(FlyInRoutine());
    }

    private void StopFlyInAnimation()
    {
        if (_flyInRoutine != null)
        {
            StopCoroutine(_flyInRoutine);
            _flyInRoutine = null;
        }
    }

    private IEnumerator FlyInRoutine()
    {
        float duration = Mathf.Max(0.01f, flyInDurationSeconds);
        float start = Time.unscaledTime;
        float end = start + duration;

        Vector2 fromPos = _cardBaseAnchoredPos + flyInOffset;
        Vector2 toPos = _cardBaseAnchoredPos;
        float fromScale = Mathf.Clamp(flyInStartScale, 0.01f, 10f);
        float toScale = 1f;
        float fromAlpha = Mathf.Clamp01(flyInStartAlpha);
        float toAlpha = 1f;

        while (Time.unscaledTime < end)
        {
            if (!isVisible || _cardRect == null || canvasGroup == null)
            {
                yield break;
            }

            float n = Mathf.InverseLerp(start, end, Time.unscaledTime);
            float eased = EaseOutBack(n);

            _cardRect.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, eased);
            _cardRect.localScale = _cardBaseLocalScale * Mathf.LerpUnclamped(fromScale, toScale, eased);
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(n));

            yield return null;
        }

        if (_cardRect != null)
        {
            _cardRect.anchoredPosition = _cardBaseAnchoredPos;
            _cardRect.localScale = _cardBaseLocalScale;
            _cardRect.localRotation = _cardBaseLocalRotation;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        _flyInRoutine = null;
    }

    private static float EaseOutBack(float t)
    {
        float x = Mathf.Clamp01(t) - 1f;
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + (c3 * x * x * x) + (c1 * x * x);
    }

}

