using UnityEngine;

/// <summary>
/// Catalyst plunger upgrade: every ball is Ignited as it launches. Attach to
/// the launcher (or anywhere on the board); only Flammable balls actually
/// catch, per the usual Ignite rules.
/// </summary>
public sealed class MatchstickPlunger : MonoBehaviour
{
    private void OnEnable()
    {
        PinballLauncher.BallLaunched += OnBallLaunched;
    }

    private void OnDisable()
    {
        PinballLauncher.BallLaunched -= OnBallLaunched;
    }

    private void OnBallLaunched(GameObject launched)
    {
        Ball ball = launched != null ? launched.GetComponent<Ball>() : null;
        BallFireStatus status = FireStatusUtility.GetOrAddBallStatus(ball);
        if (status == null)
        {
            return;
        }

        if (status.IsFlammable && !status.IsOnFire)
        {
            FireDebug.Log($"Matchstick strikes {launched.name} at launch");
            status.Ignite();
        }
        else if (!status.IsOnFire)
        {
            FireDebug.Log($"Matchstick: {launched.name} has no Flammable stacks, no light");
        }
    }
}
