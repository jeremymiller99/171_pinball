// Modified by Cursor AI (GPT-5.2) for jjmil on 2026-02-15.
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

    [Tooltip("Which centralized action should drive this flipper. Auto tries to infer from the configured key or object name.")]
    [SerializeField] private FlipperInputAction flipperAction = FlipperInputAction.RightFlipper;

    [Header("Input")]
    InputAction flipAction;

    [Header("Motion")]
    public bool invertDirection = false;
    public float flipAngle = 45f;
    public float rotateSpeed = 900f;

    private Quaternion _baseLocalRotation;
    private float _currentOffset;
    private bool _pressed;
    private bool _previousPressed;
    private Rigidbody _rb;
    private GameRulesManager _gameRulesManager;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _baseLocalRotation = transform.localRotation;
        _currentOffset = 0f;
        _previousPressed = false;
        _gameRulesManager = FindFirstObjectByType<GameRulesManager>();
        if (flipperAction == FlipperInputAction.LeftFlipper)
        {
            flipAction = InputSystem.actions.FindAction("LeftFlip");
        } else
        {
            flipAction = InputSystem.actions.FindAction("RightFlip");
        }

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
        _pressed = flipAction.IsPressed();

        if (_pressed && !_previousPressed && !_gameRulesManager.TryConsumeFlipperUse())
        {
            _pressed = false;
        }

        HandleFlipperSfx();
    }

    private void HandleFlipperSfx()
    {
        if (_pressed == _previousPressed)
            return;

        if (_pressed)
        {
            AudioManager.Instance.PlayFlipperUp(transform.position);
            if (HapticManager.Instance != null)
            {
                HapticManager.Instance.PlayFlipperHaptic();
            }
        }
        else
        {
            AudioManager.Instance.PlayFlipperDown(transform.position);
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

}