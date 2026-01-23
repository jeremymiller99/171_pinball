using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Subtle "alive" motion for a VISUAL-ONLY board transform (mesh parent).
/// Attach this to a GameObject that parents only renderers (no colliders/rigidbodies),
/// so gameplay/physics remain unchanged.
/// </summary>
[DisallowMultipleComponent]
public sealed class VisualSwayByPointer : MonoBehaviour
{
    [Header("Rotation (degrees)")]
    public float maxYaw = 0.6f;
    public float maxPitch = 0.35f;
    public float maxRoll = 0.25f;

    [Header("Position (local units)")]
    [Tooltip("Small local position offset for a bit of parallax. Keep tiny (e.g. 0.01 - 0.05).")]
    public Vector3 maxLocalOffset = new Vector3(0.02f, 0.01f, 0.0f);

    [Header("Smoothing")]
    [Tooltip("Higher is snappier. Lower is floatier.")]
    public float response = 8f;

    [Header("Time")]
    public bool useUnscaledTime = true;

    private Quaternion _baseLocalRotation;
    private Vector3 _baseLocalPosition;

    private Vector3 _posVelocity;
    private Vector3 _rotVelocity;
    private Vector3 _currentEuler;

    private void Awake()
    {
        _baseLocalRotation = transform.localRotation;
        _baseLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        _baseLocalRotation = transform.localRotation;
        _baseLocalPosition = transform.localPosition;
        _posVelocity = Vector3.zero;
        _rotVelocity = Vector3.zero;
        _currentEuler = Vector3.zero;
    }

    private void LateUpdate()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        Vector2 pointer01 = GetPointerNormalized01();
        Vector2 fromCenter = (pointer01 - new Vector2(0.5f, 0.5f)) * 2f; // [-1..1]

        float yaw = fromCenter.x * maxYaw;
        float pitch = -fromCenter.y * maxPitch;
        float roll = -fromCenter.x * maxRoll;

        Vector3 targetEuler = new Vector3(pitch, yaw, roll);
        Vector3 targetPos = _baseLocalPosition + new Vector3(
            fromCenter.x * maxLocalOffset.x,
            fromCenter.y * maxLocalOffset.y,
            0f * maxLocalOffset.z
        );

        float smoothTime = Mathf.Max(0.0001f, 1f / Mathf.Max(0.01f, response));
        _currentEuler = Vector3.SmoothDamp(_currentEuler, targetEuler, ref _rotVelocity, smoothTime, Mathf.Infinity, dt);
        transform.localRotation = _baseLocalRotation * Quaternion.Euler(_currentEuler);
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, targetPos, ref _posVelocity, smoothTime, Mathf.Infinity, dt);
    }

    private static Vector2 GetPointerNormalized01()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 p = mouse.position.ReadValue();
            float w = Mathf.Max(1f, Screen.width);
            float h = Mathf.Max(1f, Screen.height);
            return new Vector2(Mathf.Clamp01(p.x / w), Mathf.Clamp01(p.y / h));
        }
        return new Vector2(0.5f, 0.5f);
#else
        Vector3 p = Input.mousePosition;
        float w = Mathf.Max(1f, Screen.width);
        float h = Mathf.Max(1f, Screen.height);
        return new Vector2(Mathf.Clamp01(p.x / w), Mathf.Clamp01(p.y / h));
#endif
    }
}

