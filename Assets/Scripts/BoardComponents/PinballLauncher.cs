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
    [Header("Mouse Drag Input (simple)")]
    [Tooltip("Collider used as the clickable/hoverable zone for winding the launcher. If null, uses this GameObject's collider.")]
    public Collider clickZone;

    [Tooltip("Dragging this many pixels along Pull Screen Direction equals full charge.")]
    public float pixelsToFullPull = 180f;

    [Tooltip("Direction (in screen space) that counts as 'pulling back'. Default is down (drag down to pull back).")]
    public Vector2 pullScreenDirection = new Vector2(0f, -1f);

    [Tooltip("Log when the mouse enters/exits the click zone.")]
    public bool debugHover = true;

    [Tooltip("Log when clicking/dragging/releasing so we know it's working.")]
    public bool debugDrag = true;

    [Header("Keyboard Input (optional)")]
    [Tooltip("If true, Space (or the key below) also charges/launches.")]
    public bool allowKeyboardLaunch = false;

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
    private Vector3 _visualPullLocalDir = Vector3.back;

    private bool _isHovering;
    private bool _isDragging;
    private Vector2 _dragStartMousePos;
    private float _nextDragLogTime;

    private void Awake()
    {
        if (launchDirection == null)
            launchDirection = transform;

        if (clickZone == null)
            clickZone = GetComponent<Collider>();

        if (plungerVisual != null)
        {
            _visualStartLocalPos = plungerVisual.localPosition;
            // Pull opposite the launch direction, but expressed in the plunger visual's parent local space.
            Vector3 pullWorldDir = -launchDirection.forward;
            if (plungerVisual.parent != null)
                _visualPullLocalDir = plungerVisual.parent.InverseTransformDirection(pullWorldDir).normalized;
            else
                _visualPullLocalDir = pullWorldDir.normalized;
        }
    }

    private void Update()
    {
        UpdateHoverState();

        // Keep the visual at rest when idle and no keyboard charging is enabled.
        if (!_isDragging && !allowKeyboardLaunch && _ballRb == null)
        {
            _charge = 0f;
            UpdateVisual(0f);
        }

        // Start dragging when clicking the click zone.
        if (!_isDragging && IsLeftMouseDownThisFrame() && IsPointerOverClickZone())
        {
            if (debugDrag)
                Debug.Log($"[PinballLauncher] CLICK on click zone '{name}'.", this);

            _isDragging = true;
            _dragStartMousePos = GetMouseScreenPosition();
            _nextDragLogTime = 0f;

            if (debugDrag)
                Debug.Log($"[PinballLauncher] Click + drag start on '{name}'. Ball present: {_ballRb != null}", this);
        }

        // While dragging, the charge is directly driven by the drag amount.
        if (_isDragging)
        {
            Vector2 now = GetMouseScreenPosition();
            Vector2 delta = now - _dragStartMousePos;

            Vector2 pullDir = pullScreenDirection.sqrMagnitude > 0.0001f ? pullScreenDirection.normalized : Vector2.down;
            float pullPixels = Mathf.Max(0f, Vector2.Dot(delta, pullDir));

            float t01 = pixelsToFullPull > 0.0001f ? Mathf.Clamp01(pullPixels / pixelsToFullPull) : 0f;
            _charge = t01 * maxLaunchForce;
            UpdateVisual(t01);

            if (debugDrag && Time.unscaledTime >= _nextDragLogTime)
            {
                _nextDragLogTime = Time.unscaledTime + 0.10f;
                Debug.Log($"[PinballLauncher] Dragging '{name}'. pull={t01:0.00} charge={_charge:0}", this);
            }

            if (IsLeftMouseUpThisFrame())
            {
                float firedCharge = _charge;
                _isDragging = false;

                if (_ballRb != null)
                {
                    Launch();
                }
                else
                {
                    // No ball to launch; still snap the visual back.
                    _charge = 0f;
                    UpdateVisual(0f);
                }

                if (debugDrag)
                    Debug.Log($"[PinballLauncher] Drag release on '{name}'. Fired charge={firedCharge:0}. Ball present: {_ballRb != null}", this);
            }

            return;
        }

        // Optional keyboard fallback (kept off by default).
        if (allowKeyboardLaunch)
        {
            if (GetLaunchHeld())
            {
                _charge = Mathf.Min(maxLaunchForce, _charge + chargePerSecond * Time.deltaTime);
                UpdateVisual(_charge / Mathf.Max(1f, maxLaunchForce));
            }

            if (GetLaunchReleasedThisFrame())
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
    }

    private void UpdateHoverState()
    {
        bool over = IsPointerOverClickZone();
        if (over == _isHovering)
            return;

        _isHovering = over;
        if (debugHover && _isHovering)
            Debug.Log($"[PinballLauncher] HOVER over click zone '{name}'.", this);
    }

    private bool IsPointerOverClickZone()
    {
        if (clickZone == null)
            return false;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(GetMouseScreenPosition());
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Collide))
            return false;

        return hit.collider == clickZone;
    }

    private static Vector2 GetMouseScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        return m != null ? m.position.ReadValue() : (Vector2)Input.mousePosition;
#else
        return Input.mousePosition;
#endif
    }

    private static bool IsLeftMouseDownThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        return m != null ? m.leftButton.wasPressedThisFrame : Input.GetMouseButtonDown(0);
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static bool IsLeftMouseUpThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        return m != null ? m.leftButton.wasReleasedThisFrame : Input.GetMouseButtonUp(0);
#else
        return Input.GetMouseButtonUp(0);
#endif
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


