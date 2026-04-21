using UnityEngine;

/// <summary>
/// While this ball is in <see cref="BallSpawner.ActiveBalls"/>, left/right flipper inputs are swapped.
/// Mult gained from board scoring (TypeOfScore.mult) is multiplied by <see cref="multScoreScale"/> (default 2).
/// </summary>
public sealed class CrossEyedBall : Ball
{
    [Header("Scoring")]
    [Tooltip("Multiplier applied to mult-type board awards (flat doubling = 2).")]
    [Min(0f)]
    [SerializeField] private float multScoreScale = 2f;


    private void Awake()
    {
        multMultiplier = multScoreScale;
    }

    /// <summary>
    /// True when any <see cref="CrossEyedBall"/> is currently in the spawner's active ball list.
    /// Used by <see cref="PinballFlipper"/> to swap paddle input.
    /// </summary>
    public static bool IsFlipInputSwappedForGameplay()
    {
        if (!ServiceLocator.TryGet<BallSpawner>(out BallSpawner spawner))
        {
            return false;
        }

        var active = spawner.ActiveBalls;
        if (active == null)
        {
            return false;
        }

        for (int i = 0; i < active.Count; i++)
        {
            GameObject go = active[i];
            if (go == null)
            {
                continue;
            }

            if (go.GetComponent<CrossEyedBall>() != null)
            {
                return true;
            }
        }

        return false;
    }
}
