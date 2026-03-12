using System;
using UnityEngine;
using TMPro;

public class UpgradeComponents : MonoBehaviour
{
    public BoardComponentDefinition boardComponentDefinition;
    [SerializeField] private ComponentUIController componentUIController;
    [SerializeField] private TextMeshProUGUI buyingComponentText;
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private UIScript uiScript;

    void Awake()
    {
        componentUIController = FindAnyObjectByType<ComponentUIController>();
        uiScript = FindAnyObjectByType<UIScript>();
    }

    public void Refresh()
    {
        buttonText.text = boardComponentDefinition.DisplayName;
        costText.text = "$" + boardComponentDefinition.Price;
    }

    public void OnClick()
    {
        componentUIController.buyingComponentDefinition = boardComponentDefinition;
        uiScript.SelectComponents();
        componentUIController.Refresh();
        Refresh();
    }

    public void OnHover()
    {
        buyingComponentText.text = boardComponentDefinition.Description;
    }
}