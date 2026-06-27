// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using UnityEngine;
using UnityEngine.XR;

public class LeprechaunBall : Ball
{
    [SerializeField] private int coinsToAdd = 1;
    [SerializeField] private float chanceForCoins = .2f;


    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (Random.value <= chanceForCoins)
        {
            componentHits = 0;
            base.AddScore(coinsToAdd, TypeOfScore.coins, pos);
        }
        
        base.AddScore(amount, typeOfScore, pos);
    }
}
