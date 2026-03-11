using System;
using UnityEngine;

public class UpgradeComponents : MonoBehaviour
{
    private const int BoardComponentUpgradeCost = 25;

    [SerializeField] private TypeOfScore typeOfScore;
    [SerializeField] private float defaultScore;
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private ShopUIController shopUIController;
    [SerializeField] private GameObject targetedComponent;
    [SerializeField] private GameObject star;

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
        targetedComponent = gameObject;

        bool hasUpgrade = TargetHasScoreType(targetedComponent, typeOfScore);
        if (!hasUpgrade)
        {
            var session = GameSession.Instance;
            if (session != null)
            {
                hasUpgrade = session.HasBoardComponentUpgrade(targetedComponent, typeOfScore);
            }
        }

        if (star != null)
        {
            star.SetActive(hasUpgrade);
        }
    }

    public void UpgradeComponent()
    {
        if (targetedComponent == null || gameRulesManager == null)
        {
            return;
        }

        if (TargetHasScoreType(targetedComponent, typeOfScore))
        {
            Refresh(targetedComponent);
            return;
        }

        var session = GameSession.Instance;
        if (session != null && session.HasBoardComponentUpgrade(targetedComponent, typeOfScore))
        {
            EnsureBoardComponentScoreType(targetedComponent);
            Refresh(targetedComponent);
            return;
        }

        if (!gameRulesManager.TrySpendCoins(BoardComponentUpgradeCost))
        {
            return;
        }

        AudioManager.Instance.PlayPurchase();

        if (session != null)
        {
            session.RegisterBoardComponentUpgrade(targetedComponent, typeOfScore, defaultScore);
        }

        EnsureBoardComponentScoreType(targetedComponent);

        if (shopUIController != null)
        {
            shopUIController.RefreshUI();
        }

        Refresh(targetedComponent);
    }

    private bool TargetHasScoreType(GameObject target, TypeOfScore scoreType)
    {
        if (target == null)
        {
            return false;
        }

        BoardComponent[] components = target.GetComponents<BoardComponent>();
        for (int i = 0; i < components.Length; i++)
        {
            BoardComponent bc = components[i];
            if (bc != null && bc.typeOfScore == scoreType)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureBoardComponentScoreType(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        BoardComponent[] components = target.GetComponents<BoardComponent>();
        for (int i = 0; i < components.Length; i++)
        {
            BoardComponent bc = components[i];
            if (bc != null && bc.typeOfScore == typeOfScore)
            {
                bc.amountToScore = defaultScore;
                return;
            }
        }

        BoardComponent template = target.GetComponent<BoardComponent>();
        if (template == null)
        {
            return;
        }

        BoardComponent newComponent = target.AddComponent<BoardComponent>();
        newComponent.amountToScore = defaultScore;
        newComponent.typeOfScore = typeOfScore;
        newComponent.upObject = template.upObject;
        newComponent.downObject = template.downObject;
        newComponent.leftObject = template.leftObject;
        newComponent.rightObject = template.rightObject;
        newComponent.startingSize = template.startingSize;
        newComponent.pulseAmount = template.pulseAmount;
        newComponent.maxPulseScale = template.maxPulseScale;
    }
}