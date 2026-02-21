using UnityEngine;

public class BOMI : Ball
{
    [SerializeField] private int componentHitsToInvert;


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
