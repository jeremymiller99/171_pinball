using System;
using UnityEngine;

public class UpgradeComponents : MonoBehaviour
{
    [SerializeField] private TypeOfScore typeOfScore;
    [SerializeField] private float defaultScore;
    [SerializeField] private int cost;
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private ShopUIController shopUIController;
    [SerializeField] private GameObject targetedComponent;
    [SerializeField] private GameObject star;

    private void Awake()
    {
        if (!gameRulesManager)
        {
            gameRulesManager = FindFirstObjectByType<GameRulesManager>();
        }
        if (!shopUIController)
        {
            shopUIController = FindFirstObjectByType<ShopUIController>();
        }
    }

    public void Refresh(GameObject gameObject)
    {
        foreach(BoardComponent component in gameObject.GetComponents<BoardComponent>())
        {
            if (component.typeOfScore == typeOfScore)
            {
                star.SetActive(true);
            } else
            {
                star.SetActive(false);
            }
        }
        targetedComponent = gameObject;
    }

    public void UpgradeComponent()
    {
        if (cost > 0 && !gameRulesManager.TrySpendCoins(cost))
        {
            return;
        }

        shopUIController.RefreshUI();
        BoardComponent defaultComponent = targetedComponent.GetComponent<BoardComponent>();
        BoardComponent newComponent = targetedComponent.AddComponent<BoardComponent>();
        newComponent.amountToScore = defaultScore;
        newComponent.typeOfScore = typeOfScore;
        newComponent.upObject = defaultComponent.upObject;
        newComponent.downObject = defaultComponent.downObject;
        newComponent.leftObject = defaultComponent.leftObject;
        newComponent.rightObject = defaultComponent.rightObject;
        newComponent.startingSize = defaultComponent.startingSize;
        newComponent.pulseAmount = defaultComponent.pulseAmount;
        newComponent.maxPulseScale = defaultComponent.maxPulseScale;
        Refresh(targetedComponent);
    }

    
    
}
