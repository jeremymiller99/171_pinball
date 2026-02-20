using System;
using UnityEngine;

public class UpgradeComponents : MonoBehaviour
{
    [SerializeField] private string tagToUpgrade;
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

        if (gameRulesManager == null || shopUIController == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(tagToUpgrade))
        {
            return;
        }

        if (cost > 0 && gameRulesManager.Coins < cost)
        {
            return;
        }

        if (cost > 0 && !gameRulesManager.TrySpendCoins(cost))
        {
            return;
        }

        shopUIController.RefreshUI();

        float mult = multiplyPointsBy;
        if (mult <= 0f)
        {
            mult = 1f;
        }

        UpgradePointAddersByTag(tagToUpgrade, mult);
        UpgradeMultAddersByTag(tagToUpgrade, mult);
        UpgradePortalsByTag(tagToUpgrade, mult);
    }

    private static void UpgradePointAddersByTag(string tag, float mult)
    {
        /*
        PointAdder[] adders = Resources.FindObjectsOfTypeAll<PointAdder>();
        for (int i = 0; i < adders.Length; i++)
        {
            PointAdder adder = adders[i];
            if (!IsSceneInstance(adder))
            {
                continue;
            }

            if (!HierarchyContainsTag(adder.transform, tag))
            {
                continue;
            }

            adder.multiplyPointsToAdd(mult);
        }
        */
    }

    private static void UpgradeMultAddersByTag(string tag, float mult)
    {
        /*
        MultAdder[] adders = Resources.FindObjectsOfTypeAll<MultAdder>();
        for (int i = 0; i < adders.Length; i++)
        {
            MultAdder adder = adders[i];
            if (!IsSceneInstance(adder))
            {
                continue;
            }

            if (!HierarchyContainsTag(adder.transform, tag))
            {
                continue;
            }

            adder.multiplyMultToAdd(mult);
        }
        */
    }

    private static void UpgradePortalsByTag(string tag, float mult)
    {
        /*
        Portal[] portals = Resources.FindObjectsOfTypeAll<Portal>();
        for (int i = 0; i < portals.Length; i++)
        {
            Portal portal = portals[i];
            if (!IsSceneInstance(portal))
            {
                continue;
            }

            if (!HierarchyContainsTag(portal.transform, tag))
            {
                continue;
            }

            portal.MultiplyExitSpeed(mult);
        }
        */
    }

    private static bool IsSceneInstance(Component c)
    {
        if (c == null)
        {
            return false;
        }

        GameObject go = c.gameObject;
        if (go == null)
        {
            return false;
        }

        return go.scene.IsValid();
    }

    private static bool HierarchyContainsTag(Transform root, string tag)
    {
        if (root == null)
        {
            return false;
        }

        Transform t = root;
        while (t != null)
        {
            if (t.CompareTag(tag))
            {
                return true;
            }
            t = t.parent;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform child = transforms[i];
            if (child == null)
            {
                continue;
            }
            if (child.CompareTag(tag))
            {
                return true;
            }
        }

        return false;
    }
}
