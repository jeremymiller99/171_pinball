/*
Modified code from:
https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/api/UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation.html
- Drew
*/
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class ControlChanger : MonoBehaviour
{
    [SerializeField] private InputActionReference actionReference; // Reference to an action to rebind.
    [SerializeField] private int bindingIndex; // Index into m_Action.bindings for binding to rebind.
    [SerializeField] private TextMeshProUGUI displayText; // Text in UI that receives the binding display string.
    private InputActionRebindingExtensions.RebindingOperation rebind;
    [SerializeField] private string noInputText = "N/A";

    public void OnEnable()
    {
        UpdateDisplayText();
    }

    public void OnDisable()
    {
        rebind?.Dispose();
    }

    public void OnClick()
    {
        actionReference.action.Disable();
        var l_rebind = actionReference.action.PerformInteractiveRebinding().WithTargetBinding(bindingIndex).OnComplete(_ => UpdateDisplayText());
        l_rebind.Start();
        displayText.text = noInputText;
    }

    private void UpdateDisplayText()
    {
        displayText = GetComponentInChildren<TextMeshProUGUI>();
        displayText.text = actionReference.action.GetBindingDisplayString(bindingIndex);
        actionReference.action.Enable();
    }

    void Update()
    {
        if (Gamepad.all.Count == 0) {
            bindingIndex = 0;
            return;
        }

        foreach (InputControl control in Gamepad.current.allControls)
        {
            if (control.IsPressed() != false)
            {
                bindingIndex = 1;
            }
        }

        foreach (InputControl control in Keyboard.current.allControls)
        {
            if (control.IsPressed() != false)
            {
                bindingIndex = 0;
            }
        }

        if (displayText.text != noInputText)
        {
            UpdateDisplayText();
        }
    }
}
