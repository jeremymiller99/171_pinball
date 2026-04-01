// Change: add 10 width black outline to plunger visual to match board components.
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
    [SerializeField] private InputActionReference launchAction;

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
    private Vector3 _visualPullLocalDir = Vector3.back;

    private const float outlineWidth = 6f;
    private const int outlineRenderQueueOffset = 100;
    private const int outlineStencilRef = 2;

    private void Awake()
    {
        if (launchDirection == null)
            launchDirection = transform;

        if (plungerVisual != null)
        {
            _visualStartLocalPos = plungerVisual.localPosition;
            // Pull opposite the launch direction, but expressed in the plunger visual's parent local space.
            Vector3 pullWorldDir = -launchDirection.forward;
            if (plungerVisual.parent != null)
                _visualPullLocalDir = plungerVisual.parent.InverseTransformDirection(pullWorldDir).normalized;
            else
                _visualPullLocalDir = pullWorldDir.normalized;

            EnsurePlungerOutline();
        }
    }

    private void EnsurePlungerOutline()
    {
        if (plungerVisual == null) return;

        Outline outline = plungerVisual.GetComponent<Outline>();
        if (outline == null)
        {
            outline = plungerVisual.gameObject.AddComponent<Outline>();
        }
        outline.RenderQueueOffset = outlineRenderQueueOffset;
        outline.StencilRef = outlineStencilRef;
        outline.OutlineMode = Outline.Mode.OutlineVisible;
        outline.OutlineColor = Color.black;
        outline.OutlineWidth = outlineWidth;
        outline.enabled = true;
    }

    private void Update()
    {
        if (launchAction.action.IsPressed())
        {
            _charge = Mathf.Min(maxLaunchForce, _charge + chargePerSecond * Time.deltaTime);
            UpdateVisual(_charge / Mathf.Max(1f, maxLaunchForce));
        }

        if (launchAction.action.WasReleasedThisFrame())
        {
            if (_ballRb != null)
            {
                Launch();
            }
            else
            {
                _charge = 0f;
                UpdateVisual(0f);
            }
        }
    }

    private void Launch()
    {
        if (_ballRb == null)
            return;

        Vector3 dir = launchDirection.forward.normalized;
        _ballRb.AddForce(dir * _charge, ForceMode.Impulse);
        
        ServiceLocator.Get<AudioManager>()?.PlayLaunch(transform.position);
        ServiceLocator.Get<HapticManager>()?.PlayLaunchHaptic();

        _charge = 0f;
        UpdateVisual(0f);
    }

    private void UpdateVisual(float t01)
    {
        if (plungerVisual == null)
            return;

        // Pull back opposite of launch direction, in the plunger visual's local space.
        plungerVisual.localPosition = _visualStartLocalPos + _visualPullLocalDir * (visualPullDistance * Mathf.Clamp01(t01));
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