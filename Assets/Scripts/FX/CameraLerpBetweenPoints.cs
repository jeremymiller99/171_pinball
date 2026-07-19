using UnityEngine;

/// <summary>
/// Lerps this object (e.g. the main menu camera) between several transforms:
/// a default point, a second point, a third point, a fourth point, and a fifth
/// point. The object snaps to the default position/rotation when the scene starts.
///
/// Assign the empty GameObjects in the inspector. Call GoToSecond() /
/// GoToThird() / GoToFourth() / GoToFifth() / GoToDefault() (or a Toggle* method) to move —
/// e.g. from a UI button. Movement always starts from wherever the object
/// currently is, so you can jump between any of the points at any time.
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraLerpBetweenPoints : MonoBehaviour
{
    [Header("Points")]
    [Tooltip("Where the camera starts and returns to.")]
    public Transform defaultPoint;

    [Tooltip("The second position to move to (e.g. Play).")]
    public Transform secondPoint;

    [Tooltip("The third position to move to (e.g. Progression).")]
    public Transform thirdPoint;

    [Tooltip("The fourth position to move to (e.g. Settings).")]
    public Transform fourthPoint;

    [Tooltip("The fifth position to move to (reachable from anywhere with N).")]
    public Transform fifthPoint;

    [Header("Movement")]
    [Tooltip("Seconds to travel between points.")]
    public float duration = 1f;

    [Tooltip("Shapes the lerp over time (ease in/out by default).")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("If true, also lerp rotation to match the target point.")]
    public bool lerpRotation = true;

    [Header("Time")]
    [Tooltip("If true, ignores Time.timeScale (keeps moving while paused).")]
    public bool useUnscaledTime = true;

    // Current move: lerp from a captured start pose to the target pose.
    private Vector3 _fromPos, _toPos;
    private Quaternion _fromRot, _toRot;
    private float _t = 1f;        // 0 = at start pose, 1 = arrived at target.
    private bool _moving;
    private Transform _current;   // The point we're at (or heading to).

    /// <summary>True while a transition is in progress.</summary>
    public bool IsMoving => _moving;

    private void Start()
    {
        // Always begin in the default position.
        if (defaultPoint != null)
        {
            transform.position = defaultPoint.position;
            if (lerpRotation) transform.rotation = defaultPoint.rotation;
        }

        _current = defaultPoint;
        _t = 1f;
        _moving = false;
    }

    private void Update()
    {
        if (!_moving)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float step = (duration > 0f) ? dt / duration : 1f;

        _t = Mathf.Clamp01(_t + step);

        float eased = ease.Evaluate(_t);
        transform.position = Vector3.LerpUnclamped(_fromPos, _toPos, eased);
        if (lerpRotation)
            transform.rotation = Quaternion.SlerpUnclamped(_fromRot, _toRot, eased);

        // Stop once we reach the target.
        if (_t >= 1f)
            _moving = false;
    }

    /// <summary>Lerp from the current pose toward the given point.</summary>
    public void GoToPoint(Transform target)
    {
        if (target == null)
            return;

        _fromPos = transform.position;
        _fromRot = transform.rotation;
        _toPos = target.position;
        _toRot = target.rotation;
        _current = target;
        _t = 0f;
        _moving = true;
    }

    /// <summary>Move back toward the default point.</summary>
    public void GoToDefault() => GoToPoint(defaultPoint);

    /// <summary>Move toward the second point.</summary>
    public void GoToSecond() => GoToPoint(secondPoint);

    /// <summary>Move toward the third point.</summary>
    public void GoToThird() => GoToPoint(thirdPoint);

    /// <summary>Move toward the fourth point.</summary>
    public void GoToFourth() => GoToPoint(fourthPoint);

    /// <summary>Move toward the fifth point.</summary>
    public void GoToFifth() => GoToPoint(fifthPoint);

    /// <summary>Toggle between the default and second points.</summary>
    public void ToggleTarget() => GoToPoint(_current == secondPoint ? defaultPoint : secondPoint);

    /// <summary>Toggle between the default and third points (e.g. Progression).</summary>
    public void ToggleThird() => GoToPoint(_current == thirdPoint ? defaultPoint : thirdPoint);

    /// <summary>Toggle between the default and fourth points (e.g. Settings).</summary>
    public void ToggleFourth() => GoToPoint(_current == fourthPoint ? defaultPoint : fourthPoint);

    /// <summary>Toggle between the default and fifth points.</summary>
    public void ToggleFifth() => GoToPoint(_current == fifthPoint ? defaultPoint : fifthPoint);
}
