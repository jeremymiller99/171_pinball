using UnityEngine;

/// <summary>
/// Ball of Minor Inconvenience (BOML): scores +50% on normal hits, but every 5th scoring hit
/// the score is inverted and subtracted instead of added.
/// Attach to the BOML ball prefab (must have Ball as base; this overrides PointsAwardMultiplier).
/// </summary>
public class BOMLBall : Ball
{
    [Tooltip("Points multiplier on normal hits (e.g. 1.5 = +50%).")]
    [SerializeField] private float bonusMultiplier = 1.5f;

    [Tooltip("Every Nth scoring hit is a penalty: that hit's score is subtracted instead of added.")]
    [SerializeField] private int penaltyInterval = 5;

    [Header("Debug")]
    [Tooltip("Log to Console on each penalty hit so you can confirm points are being subtracted.")]
    [SerializeField] private bool logPenaltyHits = true;

    private int _scoringHitCount;

    /// <summary>
    /// Called once per scoring hit. Returns 1.5 for hits 1–4, 6–9, etc.; returns -1.5 for hits 5, 10, 15, …
    /// so the score is added normally (with bonus) or subtracted (inverted).
    /// </summary>
    public override float PointsAwardMultiplier
    {
        get
        {
            _scoringHitCount++;
            bool isPenaltyHit = penaltyInterval > 0 && (_scoringHitCount % penaltyInterval == 0);
            if (isPenaltyHit && logPenaltyHits)
                Debug.Log($"[BOML] Penalty hit #{_scoringHitCount} — points will be subtracted (mult = {-bonusMultiplier}).", this);
            return isPenaltyHit ? -bonusMultiplier : bonusMultiplier;
        }
    }

    /// <summary>Current scoring hit count (for UI/debug).</summary>
    public int ScoringHitCount => _scoringHitCount;

    /// <summary>True when the next hit will be a penalty (inverted/subtracted).</summary>
    public bool IsNextHitPenalty => penaltyInterval > 0 && ((_scoringHitCount + 1) % penaltyInterval == 0);
}
