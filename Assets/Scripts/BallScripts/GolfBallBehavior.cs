using UnityEngine;

/// <summary>
/// Attach to the Golf Ball prefab. Points and mult only (no coins or $).
/// First hit on a board component: +500 points and 2x mult. Every later hit on board components: -10 points, -0.1 mult.
/// Wall bounces do not count (only real scoring components like bumpers/targets).
/// </summary>
public class GolfBallBehavior : MonoBehaviour
{
    [Tooltip("Points awarded on the first scoring-component hit.")]
    [SerializeField] private float firstHitPoints = 500f;

    [Tooltip("Mult added on first hit (e.g. 1 = go to x2).")]
    [SerializeField] private float firstHitMult = 1f;

    [Tooltip("Points lost on each hit after the first (use positive value; applied as negative).")]
    [SerializeField] private float penaltyPoints = 10f;

    [Tooltip("Mult lost on each hit after the first (use positive value; applied as negative).")]
    [SerializeField] private float penaltyMult = 0.1f;

    private bool _firstHitDone;

    /// <summary>True until the first scoring hit has been applied (for text offset / UI).</summary>
    public bool IsFirstHit => !_firstHitDone;

    /// <summary>
    /// Called by ScoreManager on each scoring hit. Returns points and mult to apply (negative for penalties).
    /// </summary>
    public void GetAwardForHit(out float points, out float mult)
    {
        if (!_firstHitDone)
        {
            _firstHitDone = true;
            points = firstHitPoints;
            mult = firstHitMult;
            return;
        }

        points = -penaltyPoints;
        mult = -penaltyMult;
    }
}
