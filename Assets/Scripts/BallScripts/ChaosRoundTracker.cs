using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks the current "chaos round" (e.g. 10 balls, one red). Used by ChaosBall so the red ball
/// can apply a bonus when it's the last one left.
/// </summary>
public static class ChaosRoundTracker
{
    private static readonly List<ChaosBall> _balls = new List<ChaosBall>();
    private static ChaosBall _redBall;

    public static int RemainingCount => _balls.Count;
    public static bool IsRedLastStanding(ChaosBall ball) => _redBall != null && _redBall == ball && _balls.Count == 1;

    public static void Register(ChaosBall ball)
    {
        if (ball == null || _balls.Contains(ball)) return;
        _balls.Add(ball);
    }

    public static void Unregister(ChaosBall ball)
    {
        _balls.Remove(ball);
    }

    public static void SetRed(ChaosBall ball)
    {
        _redBall = ball;
    }

    public static void ClearRound()
    {
        _balls.Clear();
        _redBall = null;
    }
}
