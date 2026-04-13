
using UnityEngine;

/// <summary>
/// One-shot ball: when drained, grants a permanent flat mult bonus to the ball
/// queued behind it in the loadout (next index after this ball).
/// </summary>
public sealed class AmpUpBall : Ball
{
    [SerializeField, Tooltip(
        "Added to every mult-type board score from the ball behind this one "
        + "in loadout order once Amp Up is drained.")]
    private float flatMultBonusForBallBehind = 0.25f;

    public float FlatMultBonusForBallBehind => flatMultBonusForBallBehind;
}
