using System;
using System.Drawing;
using UnityEngine;

public class UpgradeComponents : MonoBehaviour
{
    [SerializeField] private String tagToUpgrade;
    [SerializeField] private float multiplyPointsBy;
    [SerializeField] private int cost;
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private ShopUIController shopUIController;

    private void EnsureRefs()
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

    public void UpgradeComponentByTag()
    {
        EnsureRefs();

        if (gameRulesManager.Coins < cost)
        {
            return;
        }

        gameRulesManager.AddCoins(-cost);
        shopUIController.RefreshUI();

        GameObject[] objects = GameObject.FindGameObjectsWithTag(tagToUpgrade);
        foreach (GameObject obj in objects)
        {
            PointAdder pointAdder = obj.GetComponent<PointAdder>();
            MultAdder multAdder = obj.GetComponent<MultAdder>();
            if(pointAdder)
            {
                pointAdder.multiplyPointsToAdd(multiplyPointsBy);
            }

            if(multAdder)
            {
                multAdder.multiplyMultToAdd(multiplyPointsBy);
            }
        }
    }
}
