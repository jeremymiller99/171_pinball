using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class TempTutorialPanelToggle : MonoBehaviour
{
    // Put this on an always-active object (NOT the panel itself), then assign the panel root.
    [SerializeField] private GameObject tutorialPanelRoot;

    private void Update()
    {
        if (!WasTogglePressedThisFrame())
        {
            return;
        }

        if (tutorialPanelRoot == null)
        {
            return;
        }

        tutorialPanelRoot.SetActive(!tutorialPanelRoot.activeSelf);
    }

    private static bool WasTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.T);
#endif
    }
}
