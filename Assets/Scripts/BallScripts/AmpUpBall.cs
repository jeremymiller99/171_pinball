// Updated with Claude Code (claude-opus-4-7) by jjmil on 2026-04-20.

using UnityEngine;

/// <summary>
/// One-shot ball: on its first component hit, destroys itself and permanently
/// amps up the ball queued behind it in the loadout. An amped-up ball has a
/// <see cref="Ball.ampedUpProcChance"/> chance per component hit to award
/// <see cref="Ball.ampedUpMultReward"/> mult.
/// </summary>
public sealed class AmpUpBall : Ball
{
    [SerializeField] private BallSpawner ballSpawner;

    private bool popped;
    private bool wasUsed;

    private void Awake()
    {
        if (ballSpawner == null)
        {
            ballSpawner = ServiceLocator.Get<BallSpawner>();
        }

        popped = false;
        wasUsed = false;
    }

    protected override void AddScore(
        float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (popped)
        {
            return;
        }

        popped = true;
        wasUsed = true;

        if (ballSpawner == null)
        {
            ballSpawner = ServiceLocator.Get<BallSpawner>();
        }

        if (ballSpawner != null)
        {
            ballSpawner.DespawnBall(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected override bool ShouldScoreBoardComponent(BoardComponent component)
    {
        return component != null && component.GetComponent<Portal>() == null;
    }

    private void OnDestroy()
    {
        if (!wasUsed) return;

        if (ballSpawner == null)
        {
            ballSpawner = ServiceLocator.Get<BallSpawner>();
        }

        if (ballSpawner == null || ballSpawner.HandCount == 0)
        {
            return;
        }

        int slotHint = -1;
        var marker = GetComponent<BallHandSlotMarker>();
        if (marker != null) slotHint = marker.SlotIndex;

        var loadout = ServiceLocator.Get<BallLoadoutController>();
        if (loadout != null)
        {
            loadout.ApplyAmpUpToSlotBehind(slotHint);
            loadout.ConsumeActiveBallFromLoadout(slotHint);
            ServiceLocator.Get<GameRulesManager>()?.RefreshBallsRemaining();
        }

        GameObject nextBallObject = ballSpawner.ActivateNextBall();
        Ball nextBall = nextBallObject != null
            ? nextBallObject.GetComponent<Ball>()
            : null;

        if (nextBall != null)
        {
            nextBall.SetAmpedUp(true);
        }
    }
}
