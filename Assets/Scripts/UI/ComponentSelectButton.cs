using UnityEngine;

public class ComponentSelectButton : MonoBehaviour
{
    [SerializeField] private UIScript uiScript;
    [SerializeField] private ComponentUIController componentUIController;

    void Awake()
    {
        uiScript = FindAnyObjectByType<UIScript>();
        componentUIController = FindAnyObjectByType<ComponentUIController>();
    }
    public void OnSelect()
    {
        uiScript.SelectComponents();
        componentUIController.Refresh();
    }
}
