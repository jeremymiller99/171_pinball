using System;
using UnityEngine;
using FMODUnity;

public class UpgradeComponents : MonoBehaviour
{
    [SerializeField] private TypeOfScore typeOfScore;
    [SerializeField] private float defaultScore;
    [SerializeField] private int cost;
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private ShopUIController shopUIController;
    [SerializeField] private GameObject targetedComponent;
    [SerializeField] private GameObject star;

    [Header("Audio")]
    [SerializeField] private EventReference purchaseSound;

    private void Awake()
    {
        if (!gameRulesManager)
        {
#if UNITY_2022_2_OR_NEWER
            gameRulesManager = FindFirstObjectByType<GameRulesManager>();
#else
            gameRulesManager = FindObjectOfType<GameRulesManager>();
#endif
        }
        if (!shopUIController)
        {
#if UNITY_2022_2_OR_NEWER
            shopUIController = FindFirstObjectByType<ShopUIController>();
#else
            shopUIController = FindObjectOfType<ShopUIController>();
#endif
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

        AudioManager.Instance.PlayOneShot(purchaseSound);

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