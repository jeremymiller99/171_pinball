using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Balatro-style juicy card hover effect.
/// Add this to card prefabs for satisfying hover interactions.
/// </summary>
public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("Scale")]
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float scaleSpeed = 12f;

    [Header("Rotation (3D Tilt)")]
    [SerializeField] private float maxTiltAngle = 15f;
    [SerializeField] private float tiltSpeed = 8f;

    [Header("Position")]
    [SerializeField] private float hoverLift = 30f;
    [SerializeField] private float liftSpeed = 10f;

    [Header("Shadow")]
    [SerializeField] private bool useShadow = true;
    [SerializeField] private float shadowExpand = 10f;
    [SerializeField] private float shadowAlpha = 0.3f;
    [SerializeField] private Vector2 shadowOffset = new Vector2(8f, -8f);

    [Header("Glow")]
    [SerializeField] private bool useGlow = true;
    [SerializeField] private Color glowColor = new Color(1f, 1f, 1f, 0.15f);

    [Header("Shine Sweep")]
    [SerializeField] private bool useShine = true;
    [SerializeField] private float shineDuration = 0.4f;
    [SerializeField] private float shineWidth = 0.3f;
    [SerializeField] private Color shineColor = new Color(1f, 1f, 1f, 0.4f);

    [Header("Punch Effect")]
    [SerializeField] private float punchScale = 1.08f;
    [SerializeField] private float punchDuration = 0.1f;

    [Header("Wobble")]
    [SerializeField] private float wobbleAmount = 2f;
    [SerializeField] private float wobbleSpeed = 8f;

    private RectTransform _rectTransform;
    private Vector3 _baseScale;
    private Vector3 _basePosition;
    private Quaternion _baseRotation;
    private int _baseSiblingIndex;

    private bool _isHovered;
    private Vector2 _mouseLocalPos;

    // Current animated values
    private float _currentScale = 1f;
    private float _currentLift = 0f;
    private Vector2 _currentTilt = Vector2.zero;
    private float _punchTimer = 0f;

    // Shadow/glow/shine objects
    private GameObject _shadowObj;
    private Image _shadowImage;
    private GameObject _glowObj;
    private Image _glowImage;
    private GameObject _shineObj;
    private Image _shineImage;
    private float _shineTimer = -1f;

    private Canvas _parentCanvas;
    private CanvasGroup _canvasGroup;
    private float _wobbleTime;
    private bool _initialized;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _parentCanvas = GetComponentInParent<Canvas>();

        if (useShadow)
            CreateShadow();
        if (useGlow)
            CreateGlow();
        if (useShine)
            CreateShine();
    }

    private void OnEnable()
    {
        // Reset state when re-enabled
        _isHovered = false;
        _currentScale = 1f;
        _currentLift = 0f;
        _currentTilt = Vector2.zero;
        _punchTimer = 0f;
        _shineTimer = -1f;
        _wobbleTime = 0f;
        _initialized = false;
    }

    private void LateUpdate()
    {
        // Capture base values after first frame (after positioning by parent)
        if (!_initialized && _rectTransform != null)
        {
            _basePosition = _rectTransform.anchoredPosition3D;
            _baseScale = transform.localScale;
            _baseRotation = transform.localRotation;
            _initialized = true;
        }
    }

    private void Update()
    {
        if (!_initialized)
            return;

        float dt = Time.unscaledDeltaTime;

        // Target values
        float targetScale = _isHovered ? hoverScale : 1f;
        float targetLift = _isHovered ? hoverLift : 0f;
        Vector2 targetTilt = _isHovered ? CalculateTilt() : Vector2.zero;

        // Punch effect (on hover enter)
        if (_punchTimer > 0f)
        {
            _punchTimer -= dt;
            float punchT = _punchTimer / punchDuration;
            float punch = Mathf.Sin(punchT * Mathf.PI) * (punchScale - 1f);
            targetScale += punch;
        }

        // Wobble when hovered
        if (_isHovered && wobbleAmount > 0f)
        {
            _wobbleTime += dt * wobbleSpeed;
            float wobble = Mathf.Sin(_wobbleTime) * wobbleAmount;
            targetTilt.y += wobble;
        }
        else
        {
            _wobbleTime = 0f;
        }

        // Smooth interpolation with spring-like feel
        _currentScale = Mathf.Lerp(_currentScale, targetScale, dt * scaleSpeed);
        _currentLift = Mathf.Lerp(_currentLift, targetLift, dt * liftSpeed);
        _currentTilt = Vector2.Lerp(_currentTilt, targetTilt, dt * tiltSpeed);

        // Apply transforms
        ApplyTransforms();

        // Update effects
        UpdateShadow();
        UpdateGlow();
        UpdateShine(dt);
    }

    private Vector2 CalculateTilt()
    {
        if (_rectTransform == null)
            return Vector2.zero;

        // Normalize mouse position to -1 to 1 range
        Vector2 size = _rectTransform.rect.size;
        if (size.x < 1f || size.y < 1f)
            return Vector2.zero;

        float normalizedX = Mathf.Clamp(_mouseLocalPos.x / (size.x * 0.5f), -1f, 1f);
        float normalizedY = Mathf.Clamp(_mouseLocalPos.y / (size.y * 0.5f), -1f, 1f);

        // Tilt away from mouse position for that 3D perspective feel
        return new Vector2(-normalizedY * maxTiltAngle, normalizedX * maxTiltAngle);
    }

    private void ApplyTransforms()
    {
        if (_rectTransform == null)
            return;

        // Scale
        transform.localScale = _baseScale * _currentScale;

        // Position (lift on Z or Y depending on setup)
        Vector3 pos = _basePosition;
        pos.y += _currentLift;
        _rectTransform.anchoredPosition3D = pos;

        // Rotation (3D tilt)
        Quaternion tiltRotation = Quaternion.Euler(_currentTilt.x, _currentTilt.y, 0f);
        transform.localRotation = _baseRotation * tiltRotation;
    }

    private void CreateShadow()
    {
        _shadowObj = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
        _shadowObj.transform.SetParent(transform, false);
        _shadowObj.transform.SetAsFirstSibling();

        var shadowRect = _shadowObj.GetComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.sizeDelta = Vector2.one * shadowExpand * 2f;
        shadowRect.anchoredPosition = shadowOffset;

        _shadowImage = _shadowObj.GetComponent<Image>();
        _shadowImage.color = new Color(0f, 0f, 0f, 0f);
        _shadowImage.raycastTarget = false;

        // Try to copy the card's sprite for shadow shape
        var cardImage = GetComponent<Image>();
        if (cardImage != null && cardImage.sprite != null)
        {
            _shadowImage.sprite = cardImage.sprite;
            _shadowImage.type = cardImage.type;
        }
    }

    private void CreateGlow()
    {
        _glowObj = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        _glowObj.transform.SetParent(transform, false);
        _glowObj.transform.SetAsFirstSibling();

        var glowRect = _glowObj.GetComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.sizeDelta = Vector2.one * 20f;
        glowRect.anchoredPosition = Vector2.zero;

        _glowImage = _glowObj.GetComponent<Image>();
        _glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        _glowImage.raycastTarget = false;

        var cardImage = GetComponent<Image>();
        if (cardImage != null && cardImage.sprite != null)
        {
            _glowImage.sprite = cardImage.sprite;
            _glowImage.type = cardImage.type;
        }
    }

    private void UpdateShadow()
    {
        if (_shadowImage == null)
            return;

        float targetAlpha = _isHovered ? shadowAlpha : 0f;
        Color c = _shadowImage.color;
        c.a = Mathf.Lerp(c.a, targetAlpha, Time.unscaledDeltaTime * 10f);
        _shadowImage.color = c;

        // Shadow grows with lift
        if (_shadowObj != null)
        {
            var shadowRect = _shadowObj.GetComponent<RectTransform>();
            float expand = shadowExpand * (_currentLift / Mathf.Max(1f, hoverLift));
            shadowRect.sizeDelta = Vector2.one * expand * 2f;
            shadowRect.anchoredPosition = shadowOffset * (_currentLift / Mathf.Max(1f, hoverLift));
        }
    }

    private void UpdateGlow()
    {
        if (_glowImage == null)
            return;

        float targetAlpha = _isHovered ? glowColor.a : 0f;
        Color c = _glowImage.color;
        c.a = Mathf.Lerp(c.a, targetAlpha, Time.unscaledDeltaTime * 10f);
        _glowImage.color = c;
    }

    private void CreateShine()
    {
        _shineObj = new GameObject("Shine", typeof(RectTransform), typeof(Image));
        _shineObj.transform.SetParent(transform, false);
        _shineObj.transform.SetAsLastSibling();

        var shineRect = _shineObj.GetComponent<RectTransform>();
        shineRect.anchorMin = new Vector2(0f, 0f);
        shineRect.anchorMax = new Vector2(0f, 1f);
        shineRect.pivot = new Vector2(0.5f, 0.5f);
        shineRect.sizeDelta = new Vector2(100f, 0f);
        shineRect.anchoredPosition = new Vector2(-100f, 0f);

        _shineImage = _shineObj.GetComponent<Image>();
        _shineImage.color = new Color(shineColor.r, shineColor.g, shineColor.b, 0f);
        _shineImage.raycastTarget = false;

        // Add mask to clip shine to card bounds
        var mask = GetComponent<Mask>();
        if (mask == null)
        {
            mask = gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
        }
    }

    private void UpdateShine(float dt)
    {
        if (_shineImage == null || _shineObj == null)
            return;

        if (_shineTimer < 0f)
        {
            _shineImage.color = new Color(shineColor.r, shineColor.g, shineColor.b, 0f);
            return;
        }

        _shineTimer += dt;
        float t = _shineTimer / shineDuration;

        if (t > 1f)
        {
            _shineTimer = -1f;
            _shineImage.color = new Color(shineColor.r, shineColor.g, shineColor.b, 0f);
            return;
        }

        // Move shine across the card
        var shineRect = _shineObj.GetComponent<RectTransform>();
        var cardRect = _rectTransform.rect;
        float width = cardRect.width * shineWidth;
        float startX = -width;
        float endX = cardRect.width + width;
        float currentX = Mathf.Lerp(startX, endX, t);

        shineRect.sizeDelta = new Vector2(width, 0f);
        shineRect.anchoredPosition = new Vector2(currentX, 0f);

        // Fade in then out
        float alpha = Mathf.Sin(t * Mathf.PI) * shineColor.a;
        _shineImage.color = new Color(shineColor.r, shineColor.g, shineColor.b, alpha);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isHovered)
            return;

        _isHovered = true;
        _punchTimer = punchDuration;
        _shineTimer = 0f; // Start shine sweep

        // Bring to front
        _baseSiblingIndex = transform.GetSiblingIndex();
        transform.SetAsLastSibling();

        // Play sound if you have FMOD
        try { FMODUnity.RuntimeManager.PlayOneShot("event:/card_hover"); } catch { }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;

        // Restore sibling order
        if (transform.parent != null && _baseSiblingIndex < transform.parent.childCount)
            transform.SetSiblingIndex(_baseSiblingIndex);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (_rectTransform == null)
            return;

        // Convert screen position to local position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out _mouseLocalPos
        );
    }

    private void OnDisable()
    {
        // Reset transforms when disabled
        if (_rectTransform != null)
        {
            transform.localScale = _baseScale;
            transform.localRotation = _baseRotation;
            _rectTransform.anchoredPosition3D = _basePosition;
        }

        _isHovered = false;
    }

    private void OnDestroy()
    {
        if (_shadowObj != null)
            Destroy(_shadowObj);
        if (_glowObj != null)
            Destroy(_glowObj);
        if (_shineObj != null)
            Destroy(_shineObj);
    }
}
