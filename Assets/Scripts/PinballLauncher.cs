using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple "plunger" style launcher:
/// - Put a trigger collider at the end of your launch lane (on the launcher object or a child).
/// - When the ball is in the trigger, hold Launch Key to charge, release to launch.
/// </summary>
public sealed class PinballLauncher : MonoBehaviour
{
    [Header("Input")]
#if ENABLE_INPUT_SYSTEM
    public Key launchKey = Key.Space;
#else
    public KeyCode launchKey = KeyCode.Space;
#endif

    [Header("Launch")]
    [Tooltip("Direction the ball will be pushed. If null, uses this transform.")]
    public Transform launchDirection;

    [Tooltip("Force at full charge.")]
    public float maxLaunchForce = 1200f;

    [Tooltip("How fast the launcher charges per second.")]
    public float chargePerSecond = 900f;

    [Header("Optional visuals")]
    [Tooltip("If set, this object will slide backward while charging and forward on release.")]
    public Transform plungerVisual;

    [Tooltip("How far (units) the visual pulls back at full charge.")]
    public float visualPullDistance = 0.15f;

    private Rigidbody _ballRb;
    private float _charge;
    private Vector3 _visualStartLocalPos;

    private void Awake()
    {
        if (launchDirection == null)
            launchDirection = transform;

        if (plungerVisual != null)
            _visualStartLocalPos = plungerVisual.localPosition;
    }

    private void Update()
    {
        if (_ballRb == null)
        {
            _charge = 0f;
            UpdateVisual(0f);
            return;
        }

        if (GetLaunchHeld())
        {
            _charge = Mathf.Min(maxLaunchForce, _charge + chargePerSecond * Time.deltaTime);
            UpdateVisual(_charge / Mathf.Max(1f, maxLaunchForce));
        }

        if (GetLaunchReleasedThisFrame())
        {
            Launch();
        }
    }

    private bool GetLaunchHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && kb[launchKey].isPressed;
#else
        return Input.GetKey(launchKey);
#endif
    }

    private bool GetLaunchReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && kb[launchKey].wasReleasedThisFrame;
#else
        return Input.GetKeyUp(launchKey);
#endif
    }

    private void Launch()
    {
        if (_ballRb == null)
            return;

        Vector3 dir = launchDirection.forward.normalized;
        _ballRb.AddForce(dir * _charge, ForceMode.Impulse);

        _charge = 0f;
        UpdateVisual(0f);
    }

    private void UpdateVisual(float t01)
    {
        if (plungerVisual == null)
            return;

        // Pull back opposite of launch direction (local -Z is a common choice if modeled that way).
        plungerVisual.localPosition = _visualStartLocalPos + Vector3.back * (visualPullDistance * Mathf.Clamp01(t01));
    }

    // Put this on a GameObject with a Trigger collider in the launch lane.
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball"))
            return;

        if (other.attachedRigidbody != null)
            _ballRb = other.attachedRigidbody;
    }

    private void OnTriggerExit(Collider other)
    {
        if (_ballRb == null)
            return;

        if (other.attachedRigidbody == _ballRb)
        {
            _ballRb = null;
            _charge = 0f;
            UpdateVisual(0f);
        }
    }
}


