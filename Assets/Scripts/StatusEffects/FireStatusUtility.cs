using UnityEngine;

public static class FireStatusUtility
{
    private static readonly BoardComponentType[] nonFlammableTypes =
    {
        BoardComponentType.Flipper,
        BoardComponentType.Portal
    };

    /// <summary>
    /// Flippers and portals are never Flammable: the player needs them to stay
    /// readable, and re-activating them every burn tick would fire teleports
    /// and flips on their own.
    /// </summary>
    public static bool CanCatchFire(BoardComponent component)
    {
        if (component == null)
        {
            return false;
        }

        foreach (BoardComponentType type in nonFlammableTypes)
        {
            if (component.componentType == type)
            {
                return false;
            }
        }

        return true;
    }

    public static ComponentFireStatus GetOrAddComponentStatus(BoardComponent component)
    {
        if (!CanCatchFire(component))
        {
            return null;
        }

        ComponentFireStatus status = component.GetComponent<ComponentFireStatus>();
        if (status == null)
        {
            status = component.gameObject.AddComponent<ComponentFireStatus>();
        }
        return status;
    }

    public static BallFireStatus GetOrAddBallStatus(Ball ball)
    {
        if (ball == null)
        {
            return null;
        }

        BallFireStatus status = ball.GetComponent<BallFireStatus>();
        if (status == null)
        {
            status = ball.gameObject.AddComponent<BallFireStatus>();
        }
        return status;
    }

    /// <summary>
    /// Number of balls and components currently On Fire. Statuses only exist
    /// on objects that have been Fueled or launched burning, so scene-wide
    /// lookup stays cheap.
    /// </summary>
    public static int CountObjectsOnFire()
    {
        FireStatus[] statuses = Object.FindObjectsByType<FireStatus>(
            FindObjectsSortMode.None);

        int count = 0;
        foreach (FireStatus status in statuses)
        {
            if (status.IsOnFire)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Fuels every component on the board and every ball in play. Flippers
    /// and portals are skipped through the usual CanCatchFire filter.
    /// </summary>
    public static void FuelAllObjectsOnBoard(int amount)
    {
        BoardComponent[] components = Object.FindObjectsByType<BoardComponent>(
            FindObjectsSortMode.None);
        foreach (BoardComponent component in components)
        {
            GetOrAddComponentStatus(component)?.Fuel(amount);
        }

        BallSpawner spawner = ServiceLocator.Get<BallSpawner>();
        if (spawner == null)
        {
            return;
        }

        foreach (GameObject ballObject in spawner.ActiveBalls)
        {
            Ball ball = ballObject != null ? ballObject.GetComponent<Ball>() : null;
            GetOrAddBallStatus(ball)?.Fuel(amount);
        }
    }

    /// <summary>
    /// Fire only advances during live play: burn countdowns and activation
    /// ticks freeze while no run is active, the shop is open, or a drain
    /// tally is in progress.
    /// </summary>
    public static bool CanTickNow()
    {
        GameRulesManager rules = ServiceLocator.Get<GameRulesManager>();
        if (rules == null || !rules.RunActive || rules.IsShopOpen)
        {
            return false;
        }

        DrainHandler drainHandler = ServiceLocator.Get<DrainHandler>();
        return drainHandler == null || !drainHandler.IsDrainProcessing;
    }
}
