#pragma warning disable CS0618 // Internal references to deprecated types
using UnityEngine;

/// <summary>
/// DEPRECATED -- part of the old tabbed shop system.
/// </summary>
[System.Obsolete("Use UnifiedShopController instead.")]
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
        //uiScript.SelectComponents();
        componentUIController.Refresh();
    }
}
