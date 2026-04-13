// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using UnityEngine;

public class FrozenComponent : Bumper
{
    [Header("Frozen")]
    [SerializeField] private float cachedAmountToScore;
    [SerializeField] private int ballHitsToUnfreeze;
    private bool _unfrozen;

    new void Awake()
    {
        base.Awake();
        cachedAmountToScore = amountToScore;
        amountToScore = 0;
    }

    new void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);
        if (!_unfrozen && ballHits <= ballHitsToUnfreeze)
        {
            SpawnBoardHitCountPopup(ballHits, ballHitsToUnfreeze);
        }
        if (ballHits == ballHitsToUnfreeze)
        {
            amountToScore = cachedAmountToScore;
            _unfrozen = true;
            ballHits = 0;
        }
    }

    new void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);
        if (!_unfrozen && ballHits <= ballHitsToUnfreeze)
        {
            SpawnBoardHitCountPopup(ballHits, ballHitsToUnfreeze);
        }
        if (ballHits == ballHitsToUnfreeze)
        {
            amountToScore = cachedAmountToScore;
            _unfrozen = true;
            ballHits = 0;
        }
    }
}
