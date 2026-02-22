// Modified by Cursor AI (GPT-5.2) for jjmil on 2026-02-15.
// Fix: flipper up/down SFX now triggers for centralized bindings + new input system.

using UnityEngine;
using FMODUnity;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PinballFlipper : MonoBehaviour
{
    public enum FlipperInputAction
    {
        Auto = 0,
        LeftFlipper = 1,
        RightFlipper = 2,
    }

    [Header("Centralized Bindings (recommended)")]
    [Tooltip("If true, reads input from ControlsBindingsService instead of per-object keys.")]
    [SerializeField] private bool useCentralBindings = true;

    [Tooltip("Which centralized action should drive this flipper. Auto tries to infer from the configured key or object name.")]
    [SerializeField] private FlipperInputAction flipperAction = FlipperInputAction.Auto;

    [Header("Input")]
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Primary key for this flipper.")]
    public Key key = Key.RightArrow;

    [Tooltip("Optional extra keys that also activate this flipper.")]
    public Key[] extraKeys;
#else
    [Tooltip("Primary key for this flipper.")]
    public KeyCode key = KeyCode.RightArrow;

    [Tooltip("Optional extra keys that also activate this flipper.")]
    public KeyCode[] extraKeys;
#endif

    [Tooltip("Also activate this flipper while the mouse left button is held.")]
    public bool useMouseLeftButton = false;

    [Tooltip("Also activate this flipper while the mouse right button is held.")]
    public bool useMouseRightButton = false;

    [Tooltip("Also activate this flipper while the mouse middle button is held.")]
    public bool useMouseMiddleButton = false;

    [Header("Motion")]
    public bool invertDirection = false;
    public float flipAngle = 45f;
    public float rotateSpeed = 900f;

    [Header("Audio")]
    [SerializeField] private EventReference flipperUpSound;
    [SerializeField] private EventReference flipperDownSound;

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
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        _pressed = useCentralBindings ? GetPressed_Centralized() : GetPressed_InputSystem();
#else
        _pressed = useCentralBindings ? GetPressed_Centralized() : GetPressed_LegacyInput();
#endif

        if (_pressed && !_previousPressed)
        {
            if (_gameRulesManager == null)
                _gameRulesManager = FindFirstObjectByType<GameRulesManager>();
            if (_gameRulesManager != null && !_gameRulesManager.TryConsumeFlipperUse())
                _pressed = false;
        }

        HandleFlipperSfx();
    }

    private bool GetPressed_Centralized()
    {
        ControlAction action = ResolveCentralAction();
        return ControlsBindingsService.IsHeld(action);
    }

    private ControlAction ResolveCentralAction()
    {
        if (flipperAction == FlipperInputAction.LeftFlipper)
            return ControlAction.LeftFlipper;
        if (flipperAction == FlipperInputAction.RightFlipper)
            return ControlAction.RightFlipper;

        // Auto: infer from current configured key or name.
#if ENABLE_INPUT_SYSTEM
        if (key == Key.LeftArrow) return ControlAction.LeftFlipper;
        if (key == Key.RightArrow) return ControlAction.RightFlipper;
#else
        if (key == KeyCode.LeftArrow) return ControlAction.LeftFlipper;
        if (key == KeyCode.RightArrow) return ControlAction.RightFlipper;
#endif

        string n = name != null ? name.ToLowerInvariant() : "";
        if (n.Contains("left")) return ControlAction.LeftFlipper;
        if (n.Contains("right")) return ControlAction.RightFlipper;
        return ControlAction.RightFlipper;
    }

#if ENABLE_INPUT_SYSTEM
    private bool GetPressed_InputSystem()
    {
        bool pressed = false;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb[key].isPressed)
                pressed = true;

            if (!pressed && extraKeys != null)
            {
                for (int i = 0; i < extraKeys.Length; i++)
                {
                    if (kb[extraKeys[i]].isPressed)
                    {
                        pressed = true;
                        break;
                    }
                }
            }
        }

        if (!pressed)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (useMouseLeftButton && mouse.leftButton.isPressed) pressed = true;
                else if (useMouseRightButton && mouse.rightButton.isPressed) pressed = true;
                else if (useMouseMiddleButton && mouse.middleButton.isPressed) pressed = true;
            }
        }
        return pressed;
    }
#else
    private bool GetPressed_LegacyInput()
    {
        if (Input.GetKey(key))
            return true;

        if (extraKeys != null)
        {
            for (int i = 0; i < extraKeys.Length; i++)
            {
                if (Input.GetKey(extraKeys[i]))
                    return true;
            }
        }

        if (useMouseLeftButton && Input.GetMouseButton(0)) return true;
        if (useMouseRightButton && Input.GetMouseButton(1)) return true;
        if (useMouseMiddleButton && Input.GetMouseButton(2)) return true;

        return false;
    }
#endif

    private void HandleFlipperSfx()
    {
        if (_pressed == _previousPressed)
            return;

        if (_pressed)
            AudioManager.Instance.PlayOneShot(flipperUpSound, transform.position);
        else
            AudioManager.Instance.PlayOneShot(flipperDownSound, transform.position);

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