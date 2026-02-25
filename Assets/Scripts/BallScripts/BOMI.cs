// Updated with Cursor (claude-4.6-opus) by assistant on 2026-02-25.
using UnityEngine;

public class BOMI : Ball
{
    [SerializeField] private int componentHitsToInvert = 10;


    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits % componentHitsToInvert == 0)
        {
            base.AddScore(-amount, typeOfScore, pos);
        } else {   
            base.AddScore(amount, typeOfScore, pos);
        }
        
    }
}
