using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIScript : MonoBehaviour
{
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private bool selectingComponents = false;
    [SerializeField] private GameObject selectedObject;

    void Awake()
    {
        eventSystem = GetComponent<EventSystem>();
    }

    void Update()
    {
        InputAction inputAction = InputSystem.actions.FindAction("UIMovement");
        if (inputAction.ReadValue<Vector2>() != Vector2.zero)
        {
            Debug.Log("moving");
        }
        if (eventSystem.currentSelectedGameObject && eventSystem.currentSelectedGameObject.activeInHierarchy)
        {
            selectedObject = eventSystem.currentSelectedGameObject;
            return;
        } 
        if (!selectingComponents && (Gamepad.all.Count != 0 && Gamepad.current.wasUpdatedThisFrame || Keyboard.current.wasUpdatedThisFrame))
        {
            SelectButton();
        }
    }

    public void SelectButton()
    {

        eventSystem.sendNavigationEvents = true;
        selectingComponents = false;
        Button firstButton = FindFirstObjectByType<Button>();
        if (!firstButton) return;
        firstButton.enabled = true;
        eventSystem.SetSelectedGameObject(firstButton.gameObject);
    }

    public void SelectComponents()
    {
        selectingComponents = true;
        eventSystem.sendNavigationEvents = false;
    }

}
