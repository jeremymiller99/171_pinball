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
    [SerializeField] private string firstButtonTag = "FirstButton";

    void Awake()
    {
        eventSystem = GetComponent<EventSystem>();
    }

    void Update()
    {
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
        GameObject firstButtonObject = GameObject.FindWithTag(firstButtonTag);
        Button firstButton = FindAnyObjectByType<Button>();
        if (firstButtonObject)
        {
            firstButton = firstButtonObject.GetComponent<Button>();
        }

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
