// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
using UnityEngine;

public class LeprechaunBall : Ball
{
    [SerializeField] private int coinsToAdd = 1;
    [SerializeField] private int componentHitsPerDollar = 5;


    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits % componentHitsPerDollar == 0)
        {
            scoreManager.AddScore(coinsToAdd * coinMultiplier, TypeOfScore.coins, pos);
        }
        
        base.AddScore(amount, typeOfScore, pos);
    }
}
