using UnityEngine;

public class FrozenComponent : BoardComponent
{
    [Header("Frozen")]
    [SerializeField] private float cachedAmountToScore;
    [SerializeField] private int ballHitsToUnfreeze;

    new void Awake()
    {
        base.Awake();
        cachedAmountToScore = amountToScore;
        amountToScore = 0;
    }

    new void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);
        if (ballHits == ballHitsToUnfreeze)
        {
            amountToScore = cachedAmountToScore;
        }
    }
}
