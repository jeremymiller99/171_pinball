// CrossEyedBall swaps LeftFlip/RightFlip while that ball is active.
// Fix: flipper up/down SFX now triggers for centralized bindings + new input system.
// Change: add 10 width black outline to match board components.

using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PinballFlipper : MonoBehaviour
{
    private const float outlineWidth = 6f;
    private const int outlineRenderQueueOffset = 100;
    private const int outlineStencilRef = 2;
    public enum FlipperInputAction
    {
        LeftFlipper,
        RightFlipper
    }

    [SerializeField] protected int amountOfFlips = 0;
    [SerializeField] protected Ball latestBallHit;

    [Tooltip("Which centralized action should drive this flipper. Auto tries to infer from the configured key or object name.")]
    [SerializeField] public FlipperInputAction flipperAction = FlipperInputAction.RightFlipper;

    [Header("Input")]
    InputAction flipAction;
    InputAction leftFlipAction;
    InputAction rightFlipAction;

    [Header("Motion")]
    public bool invertDirection = false;
    public float flipAngle = 45f;
    public float rotateSpeed = 900f;

    private Quaternion _baseLocalRotation;
    private float _currentOffset;
    private bool _pressed;
    private bool _previousPressed;
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _baseLocalRotation = transform.localRotation;
        _currentOffset = 0f;
        _previousPressed = false;
#if ENABLE_INPUT_SYSTEM
        var actions = InputSystem.actions;
        if (actions != null)
        {
            leftFlipAction = actions.FindAction("LeftFlip");
            rightFlipAction = actions.FindAction("RightFlip");
        }

        if (flipperAction == FlipperInputAction.LeftFlipper)
        {
            flipAction = leftFlipAction;
        }
        else
        {
            flipAction = rightFlipAction;
        }
#endif

        EnsureOutline();
    }

    private void EnsureOutline()
    {
        Outline outline = GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
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
#if ENABLE_INPUT_SYSTEM
        if (leftFlipAction != null && rightFlipAction != null)
        {
            bool physicalLeft =
                flipperAction == FlipperInputAction.LeftFlipper;

            if (CrossEyedBall.IsFlipInputSwappedForGameplay())
            {
                physicalLeft = !physicalLeft;
            }

            InputAction act = physicalLeft ? leftFlipAction : rightFlipAction;
            _pressed = act != null && act.IsPressed();
        }
        else if (flipAction != null)
        {
            _pressed = flipAction.IsPressed();
        }
        else
        {
            _pressed = false;
        }
#else
        _pressed = false;
#endif

        if (_pressed && !_previousPressed)
        {
            amountOfFlips++;
        }

        HandleFlipperSfx();
    }

    private void HandleFlipperSfx()
    {
        if (_pressed == _previousPressed)
            return;

        if (_pressed)
        {
            ServiceLocator.Get<AudioManager>()?.PlayFlipperUp(transform.position);
            ServiceLocator.Get<HapticManager>()?.PlayFlipperHaptic();
        }
        else
        {
            ServiceLocator.Get<AudioManager>()?.PlayFlipperDown(transform.position);
        }

        _previousPressed = _pressed;
    }

    private void FixedUpdate()
    {
        float dir = invertDirection ? -1f : 1f;
        float targetOffset = _pressed ? (flipAngle * dir) : 0f;
        

        _currentOffset = Mathf.MoveTowards(
            _currentOffset,
            targetOffset,
            rotateSpeed * Time.deltaTime
        );

        Quaternion desiredLocal = _baseLocalRotation * Quaternion.AngleAxis(_currentOffset, Vector3.up);
        Quaternion desiredWorld = transform.parent != null
            ? transform.parent.rotation * desiredLocal
            : desiredLocal;

        _rb.MoveRotation(desiredWorld);
    }

    virtual protected void OnCollisionEnter(Collision collision)
    {
        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball)
        {
            latestBallHit = ball;
        }
    }

    public void CopyFlipperProperties(PinballFlipper flipper)
    {
        flipAction = flipper.flipAction;
        leftFlipAction = flipper.leftFlipAction;
        rightFlipAction = flipper.rightFlipAction;
        flipperAction = flipper.flipperAction;
        invertDirection = flipper.invertDirection;
        _baseLocalRotation = flipper.transform.localRotation;
    }

}