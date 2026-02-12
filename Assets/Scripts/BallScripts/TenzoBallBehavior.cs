using UnityEngine;

/// <summary>
/// Attach to the Tenzo Ball prefab. Every 10th scoring-component hit awards 100 points and +0.2 mult.
/// All other hits award nothing. Other balls ignore this; only the ball with this component uses Tenzo rules.
/// </summary>
public class TenzoBallBehavior : MonoBehaviour
{
    [Tooltip("Award this many points on every 10th hit.")]
    [SerializeField] private float pointsEveryTenth = 100f;

    [Tooltip("Add this much to mult on every 10th hit.")]
    [SerializeField] private float multEveryTenth = 0.2f;

    [Tooltip("Award on every Nth hit (1 = every hit, 10 = every 10th).")]
    [SerializeField] private int hitInterval = 10;

    private int _hitCount;

    /// <summary>
    /// Called by ScoreManager when a scoring component is hit. Returns true only on the Nth hit;
    /// then awards pointsEveryTenth and multEveryTenth. Resets count after awarding.
    /// </summary>
    public bool TryRecordHit(out float pointsToAward, out float multToAward)
    {
        _hitCount++;
        pointsToAward = 0f;
        multToAward = 0f;

        if (_hitCount >= hitInterval)
        {
            _hitCount = 0;
            pointsToAward = pointsEveryTenth;
            multToAward = multEveryTenth;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Current hit count toward the next award (0 to hitInterval-1). For UI if needed.
    /// </summary>
    public int HitCountTowardNext => _hitCount;
}
