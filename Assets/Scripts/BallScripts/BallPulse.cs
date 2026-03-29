// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-27.
// Change: add pulsing scale animation for hand balls during shop selection.
// Change: add hover scale multiplier so hovered/clicked balls appear larger.
using UnityEngine;

/// <summary>
/// Oscillates a ball's scale between a larger and smaller size,
/// matching the pulse behaviour used by <see cref="BoardComponent"/>
/// when board pieces are selected in the shop. Also supports a
/// hover multiplier that scales the ball up when individually
/// highlighted (stacks with the pulse when both are active).
/// </summary>
public sealed class BallPulse : MonoBehaviour
{
    [SerializeField] private float maxPulseScale = 1.15f;
    [SerializeField] private float pulseAmount = 0.01f;

    private Vector3 startingSize;
    private int directionOfPulse = 1;
    private bool isPulsing;
    private bool hasRecordedStartingSize;
    private float hoverMultiplier = 1f;

    private Vector3 EffectiveBase => startingSize * hoverMultiplier;

    public void StartPulse()
    {
        if (isPulsing)
        {
            return;
        }

        RecordStartingSize();
        directionOfPulse = 1;
        isPulsing = true;
    }

    public void StopPulse()
    {
        if (!isPulsing)
        {
            return;
        }

        isPulsing = false;
        transform.localScale = EffectiveBase;
    }

    public void SetHoverMultiplier(float multiplier)
    {
        RecordStartingSize();
        hoverMultiplier = multiplier;

        if (!isPulsing)
        {
            transform.localScale = EffectiveBase;
        }
    }

    /// <summary>
    /// Stops pulse, clears hover, and restores the original scale.
    /// </summary>
    public void ResetAll()
    {
        isPulsing = false;
        hoverMultiplier = 1f;

        if (hasRecordedStartingSize)
        {
            transform.localScale = startingSize;
        }
    }

    private void RecordStartingSize()
    {
        if (!hasRecordedStartingSize)
        {
            startingSize = transform.localScale;
            hasRecordedStartingSize = true;
        }
    }

    private void FixedUpdate()
    {
        if (!isPulsing)
        {
            return;
        }

        Vector3 baseScale = EffectiveBase;
        Vector3 target;

        if (directionOfPulse == 1)
        {
            target = baseScale * maxPulseScale;
            transform.localScale = Vector3.MoveTowards(
                transform.localScale, target, pulseAmount);

            if (transform.localScale == target)
            {
                directionOfPulse = -1;
            }
        }
        else
        {
            target = baseScale * (1f / maxPulseScale);
            transform.localScale = Vector3.MoveTowards(
                transform.localScale, target, pulseAmount);

            if (transform.localScale == target)
            {
                directionOfPulse = 1;
            }
        }
    }

    private void OnDisable()
    {
        if (isPulsing)
        {
            ResetAll();
        }
    }
}
