// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using UnityEngine;

public class BlueTwo : Ball
{
    [SerializeField] private int hitInterval = 2;
    [SerializeField] private float multiplierOnInterval = 2f;

    protected override int HitIntervalForPopup => hitInterval;

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (typeOfScore == TypeOfScore.points && componentHits > 0 && componentHits % hitInterval == 0)
        {
            base.AddScore(multiplierOnInterval * amount, typeOfScore, pos);
            componentHits = 0;
        }
        else
        {
            base.AddScore(amount, typeOfScore, pos);
        }

    }
}