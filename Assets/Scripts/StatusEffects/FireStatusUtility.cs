public static class FireStatusUtility
{
    public static ComponentFireStatus GetOrAddComponentStatus(BoardComponent component)
    {
        if (component == null)
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
