using UnityEngine;

/// <summary>
/// Drives the star map's camera: slides and zooms the content so a chosen point
/// sits in the middle of the viewport at a chosen scale.
///
/// Used for both levels of detail — zoomed out to frame every region, and zoomed
/// in to fill the view with one region's stars. Movement is clamped to the
/// content bounds at the target scale, so the map can never expose empty space
/// beyond its own edge.
/// </summary>
public class StarMapFocuser : MonoBehaviour
{
    [SerializeField] RectTransform _viewport;
    [SerializeField] RectTransform _content;

    [Header("Feel")]
    [Tooltip("Seconds to travel to the focused point.")]
    [SerializeField] float _duration = 0.45f;
    [Tooltip("Eases the slide and zoom. Left as ease-in-out by default.")]
    [SerializeField] AnimationCurve _ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    Vector2 _fromPosition, _toPosition;
    float _fromScale, _toScale;
    float _elapsed;
    bool _animating;

    public bool IsAnimating { get { return _animating; } }
    public float CurrentScale { get { return _content != null ? _content.localScale.x : 1f; } }

    public void Configure(RectTransform viewport, RectTransform content)
    {
        _viewport = viewport;
        _content = content;
        _animating = false;
    }

    void Awake()
    {
        if (_viewport == null) _viewport = GetComponent<RectTransform>();
    }

    /// <summary>Ease the given content-local point to the viewport centre at the given zoom.</summary>
    public void FocusOn(Vector2 contentLocalPoint, float scale)
    {
        if (_content == null) return;

        _toScale = Mathf.Max(0.0001f, scale);
        _toPosition = ClampPosition(-contentLocalPoint * _toScale, _toScale);

        _fromPosition = _content.anchoredPosition;
        _fromScale = _content.localScale.x;
        _elapsed = 0f;

        bool alreadyThere = (_toPosition - _fromPosition).sqrMagnitude < 0.0001f &&
                            Mathf.Abs(_toScale - _fromScale) < 0.0001f;

        if (_duration <= 0f || alreadyThere)
        {
            Apply(_toPosition, _toScale);
            _animating = false;
            return;
        }

        _animating = true;
    }

    /// <summary>Jump straight there with no animation (e.g. initial framing).</summary>
    public void FocusImmediate(Vector2 contentLocalPoint, float scale)
    {
        if (_content == null) return;
        scale = Mathf.Max(0.0001f, scale);
        Apply(ClampPosition(-contentLocalPoint * scale, scale), scale);
        _animating = false;
    }

    void Update()
    {
        if (!_animating || _content == null) return;

        // Unscaled: the menu may be running at timeScale 0.
        _elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        float eased = _ease.Evaluate(t);

        float scale = Mathf.LerpUnclamped(_fromScale, _toScale, eased);
        Vector2 position = Vector2.LerpUnclamped(_fromPosition, _toPosition, eased);

        // Re-clamp mid-flight: the bounds shrink as the content zooms out, so
        // the interpolated position can otherwise drift outside them.
        Apply(ClampPosition(position, scale), scale);

        if (t >= 1f)
        {
            Apply(_toPosition, _toScale);
            _animating = false;
        }
    }

    void Apply(Vector2 position, float scale)
    {
        _content.localScale = new Vector3(scale, scale, 1f);
        _content.anchoredPosition = position;
    }

    /// <summary>Snap the content back inside its bounds (after a rebuild/resize).</summary>
    public void ClampToBounds()
    {
        if (_content == null || _viewport == null) return;
        float scale = _content.localScale.x;
        _content.anchoredPosition = ClampPosition(_content.anchoredPosition, scale);
    }

    /// <summary>
    /// Keeps the scaled content's edges outside the viewport's, so drilled-in
    /// views can't slide off into nothing.
    ///
    /// When the scaled content is smaller than the viewport on an axis there is
    /// no meaningful bound, so the requested position is honoured as-is. That
    /// matters for the zoomed-out region view: the whole map fits on screen, and
    /// forcing it to the content's centre would shove an off-centre cluster of
    /// territories out of frame.
    /// </summary>
    Vector2 ClampPosition(Vector2 position, float scale)
    {
        Vector2 limit = (_content.rect.size * scale - _viewport.rect.size) * 0.5f;

        return new Vector2(
            limit.x > 0f ? Mathf.Clamp(position.x, -limit.x, limit.x) : position.x,
            limit.y > 0f ? Mathf.Clamp(position.y, -limit.y, limit.y) : position.y);
    }
}
