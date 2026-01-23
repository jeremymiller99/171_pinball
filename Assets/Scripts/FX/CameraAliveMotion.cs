using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Subtle "alive" camera motion inspired by hover/parallax UI.
/// - Reacts to pointer position (tiny pitch/yaw/roll)
/// - Adds gentle procedural noise
/// - Optionally adds slight FOV breathing
///
/// Designed to layer safely with your existing CameraShake (which writes localPosition):
/// this script writes ONLY localRotation (and optionally Camera.fieldOfView).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraAliveMotion : MonoBehaviour
{
    [Header("Pointer parallax (degrees)")]
    [Tooltip("Max yaw (left/right) in degrees at screen edges.")]
    public float maxYaw = 1.25f;

    [Tooltip("Max pitch (up/down) in degrees at screen edges.")]
    public float maxPitch = 0.9f;

    [Tooltip("Max roll (tilt) in degrees at screen edges.")]
    public float maxRoll = 0.35f;

    [Header("Noise (degrees)")]
    [Tooltip("Overall noise strength in degrees.")]
    public float noiseAmount = 0.25f;

    [Tooltip("How fast the noise evolves.")]
    public float noiseSpeed = 0.35f;

    [Header("Smoothing")]
    [Tooltip("Higher is snappier. Lower is floatier.")]
    public float response = 10f;

    [Header("Optional FOV breathing")]
    public bool animateFov = false;
    [Tooltip("FOV change in degrees (very small).")]
    public float fovAmount = 0.25f;
    [Tooltip("Cycles per second.")]
    public float fovSpeed = 0.12f;

    [Header("Time")]
    [Tooltip("If true, ignores Time.timeScale (still animates while paused).")]
    public bool useUnscaledTime = true;

    private Camera _cam;
    private Quaternion _baseLocalRotation;
    private float _baseFov;

    // SmoothDamp state for Euler offsets
    private Vector3 _eulerVelocity;
    private Vector3 _currentEuler;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _baseLocalRotation = transform.localRotation;
        _baseFov = _cam.fieldOfView;
    }

    private void OnEnable()
    {
        _baseLocalRotation = transform.localRotation;
        _baseFov = _cam.fieldOfView;
        _eulerVelocity = Vector3.zero;
        _currentEuler = Vector3.zero;
    }

    private void LateUpdate()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = useUnscaledTime ? Time.unscaledTime : Time.time;

        Vector2 pointer01 = GetPointerNormalized01();
        Vector2 fromCenter = (pointer01 - new Vector2(0.5f, 0.5f)) * 2f; // [-1..1]

        // Pointer-driven angles
        float yaw = fromCenter.x * maxYaw;
        float pitch = -fromCenter.y * maxPitch;
        float roll = -fromCenter.x * maxRoll;

        // Gentle noise (Perlin is stable + non-jittery)
        float nx = (Mathf.PerlinNoise(10.123f, t * noiseSpeed) * 2f - 1f);
        float ny = (Mathf.PerlinNoise(20.456f, t * noiseSpeed) * 2f - 1f);
        float nz = (Mathf.PerlinNoise(30.789f, t * noiseSpeed) * 2f - 1f);

        Vector3 targetEuler = new Vector3(
            pitch + ny * noiseAmount,
            yaw + nx * noiseAmount,
            roll + nz * noiseAmount
        );

        // Smoothly approach target. Convert response to a smooth time.
        float smoothTime = Mathf.Max(0.0001f, 1f / Mathf.Max(0.01f, response));
        _currentEuler = Vector3.SmoothDamp(_currentEuler, targetEuler, ref _eulerVelocity, smoothTime, Mathf.Infinity, dt);

        transform.localRotation = _baseLocalRotation * Quaternion.Euler(_currentEuler);

        if (animateFov && !_cam.orthographic)
        {
            _cam.fieldOfView = _baseFov + Mathf.Sin(t * Mathf.PI * 2f * Mathf.Max(0f, fovSpeed)) * fovAmount;
        }
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

