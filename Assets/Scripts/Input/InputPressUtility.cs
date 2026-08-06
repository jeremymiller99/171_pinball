using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// "Did the player press anything at all this frame?" — across keyboard, mouse,
/// touch and gamepad. Used by transient UI that should get out of the way on any
/// input rather than only on a click, e.g. the inspect tooltip.
/// </summary>
public static class InputPressUtility
{
    public static bool WasAnyInputPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame))
        {
            return true;
        }

        if (Touchscreen.current != null)
        {
            if (Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }

            foreach (TouchControl t in Touchscreen.current.touches)
            {
                if (t != null && t.press.wasPressedThisFrame)
                {
                    return true;
                }
            }
        }

        if (Gamepad.current != null)
        {
            foreach (InputControl c in Gamepad.current.allControls)
            {
                if (c is ButtonControl b && b.wasPressedThisFrame)
                {
                    return true;
                }
            }
        }

        return false;
#else
        // Legacy input fallback (only used if the old Input system is enabled).
        return UnityEngine.Input.anyKeyDown ||
               UnityEngine.Input.GetMouseButtonDown(0) ||
               UnityEngine.Input.GetMouseButtonDown(1) ||
               UnityEngine.Input.GetMouseButtonDown(2);
#endif
    }
}
