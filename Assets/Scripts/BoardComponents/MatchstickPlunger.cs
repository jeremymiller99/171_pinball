// Updated by Claude Code (claude-opus-5) for jjmil on 2026-07-28.
// Change: Flammable gating removed — every launched ball now catches.

using UnityEngine;

/// <summary>
/// Catalyst plunger upgrade: attach to the launcher (or anywhere on the board) and
/// every ball is lit on Fire as it launches.
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
        if (ball == null)
        {
            return;
        }

        FireDebug.Log($"Matchstick strikes {launched.name} at launch");
        FireStatusUtility.LightBall(ball);
    }
}
