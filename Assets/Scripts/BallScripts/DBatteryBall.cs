using UnityEngine;

/// <summary>
/// Tech ball: leaves the plunger already carrying Charge, ready to discharge
/// into the first Charge consumer it reaches.
/// </summary>
public class DBatteryBall : Ball
{
    [SerializeField] private int chargeOnLaunch = 2;

    private void Awake()
    {
        PinballLauncher.BallLaunched += OnBallLaunched;
    }

    private void OnDestroy()
    {
        PinballLauncher.BallLaunched -= OnBallLaunched;
    }

    private void OnBallLaunched(GameObject launched)
    {
        if (launched == gameObject)
        {
            ChargeStatusUtility.GiveCharge(this, chargeOnLaunch);
        }
    }
}
