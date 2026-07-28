using UnityEngine;

public static class ChargeStatusUtility
{

    public static ComponentChargeStatus GetOrAddComponentStatus(BoardComponent component)
    {
        if (component == null)
        {
            return null;
        }

        ComponentChargeStatus status = component.GetComponent<ComponentChargeStatus>();
        if (status == null)
        {
            status = component.gameObject.AddComponent<ComponentChargeStatus>();
        }
        return status;
    }

    public static BallChargeStatus GetOrAddBallStatus(Ball ball)
    {
        if (ball == null)
        {
            return null;
        }

        BallChargeStatus status = ball.GetComponent<BallChargeStatus>();
        if (status == null)
        {
            status = ball.gameObject.AddComponent<BallChargeStatus>();
        }
        return status;
    }

    /// <summary>
    /// Drains every Charge the ball carries into the receiving component.
    /// Each transferred point counts as a Shock, so the receiver's decay
    /// timer resets. Returns how much was moved.
    /// </summary>
    public static int DrainBallInto(Ball ball, ComponentChargeStatus receiver)
    {
        if (ball == null || receiver == null)
        {
            return 0;
        }

        BallChargeStatus ballStatus = ball.GetComponent<BallChargeStatus>();
        if (ballStatus == null || !ballStatus.HasCharge)
        {
            return 0;
        }

        int moved = ballStatus.TakeAllCharge();
        ChargeDebug.Log($"{ball.name} discharges {moved} Charge into {receiver.name}");
        receiver.Shock(moved);
        return moved;
    }
}
