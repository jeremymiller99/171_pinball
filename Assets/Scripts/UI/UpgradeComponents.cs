using System;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class UpgradeComponents : MonoBehaviour, IPointerEnterHandler, ISelectHandler
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
        buyingComponentText.text = boardComponentDefinition.Description;
        uiScript.SelectComponents();
        componentUIController.Refresh();
    }

    public void OnSelect(BaseEventData eventData)
    {
        buyingComponentText.text = boardComponentDefinition.Description;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        buyingComponentText.text = boardComponentDefinition.Description;
    }
}