// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using UnityEngine;

public class BOMI : Ball
{
    [SerializeField] private int componentHitsToInvert = 10;

    protected override int HitIntervalForPopup => componentHitsToInvert;

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits % componentHitsToInvert == 0)
        {
            componentHits = 0;
            base.AddScore(-amount, typeOfScore, pos);
        } else {   
            base.AddScore(amount, typeOfScore, pos);
        }
        
    }
}
