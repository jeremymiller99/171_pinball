using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIScript : MonoBehaviour
{
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private bool selectingShop = false;
    [SerializeField] private GameObject selectedObject;
    [SerializeField] private string firstButtonTag = "FirstButton";
    [SerializeField] private UnifiedShopController shopController;

    void Awake()
    {
        eventSystem = GetComponent<EventSystem>();
    }

    private void EnsureShopController()
    {
        if (shopController == null)
        {
            shopController = FindAnyObjectByType<UnifiedShopController>();
        }
    }

    void Update()
    {
        if (eventSystem.currentSelectedGameObject && eventSystem.currentSelectedGameObject.activeInHierarchy)
        {
            selectedObject = eventSystem.currentSelectedGameObject;
        } else if (!selectingShop && 
            (Gamepad.all.Count != 0 && Gamepad.current.wasUpdatedThisFrame || Keyboard.current.wasUpdatedThisFrame))
        {
            eventSystem.sendNavigationEvents = false;
            SelectButton();
        }
    }


    // This has to be done in the late update function in order to avoid telling the event system
    // to target a different game object twice in the same frame. This way, the navigation event
    // gets sent in the next frame instead of the same frame it's being told to select the first
    // button object. 
    // -Drew
    private void LateUpdate()
    {
        if (!selectingShop && !eventSystem.sendNavigationEvents)
        {
            eventSystem.sendNavigationEvents = true;

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
    }

    public void SelectButton()
    {
        EnsureShopController();
        if (shopController)
        {
            shopController.SelectingButtons = true;
        }
        selectingShop = false;

    }

    public void SelectShop()
    {
        EnsureShopController();
        selectingShop = true;
        eventSystem.sendNavigationEvents = false;
        shopController.SelectShop();
    }

    public void SelectBalls()
    {
        EnsureShopController();
        selectingShop = true;
        eventSystem.sendNavigationEvents = false;
        shopController.SelectBalls();
    }

}
