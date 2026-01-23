using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PinballFlipper : MonoBehaviour
{
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

    private Quaternion _baseLocalRotation;
    private float _currentOffset;
    private bool _pressed;
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _baseLocalRotation = transform.localRotation;
        _currentOffset = 0f;
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        _pressed = GetPressed_InputSystem();
#else
        _pressed = GetPressed_LegacyInput();
#endif
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

