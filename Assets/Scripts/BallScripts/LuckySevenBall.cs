// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using UnityEngine;

public class LuckySevenBall : Ball
{
    [SerializeField] private float multToAdd = 0.7f;
    [SerializeField] private float chanceForMult = 0.14f;


    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (Random.value <= chanceForMult)
        {
            componentHits = 0;
            base.AddScore(multToAdd, TypeOfScore.coins, pos);
        }

        base.AddScore(amount, typeOfScore, pos);
    }
}
