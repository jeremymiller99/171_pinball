// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
using UnityEngine;
using UnityEngine.XR;

public class LeprechaunBall : Ball
{
    [SerializeField] private int coinsToAdd = 1;
    [SerializeField] private int componentHitsPerDollar = 5;
    [SerializeField] private bool handledOnHitEffect = false;


    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits % componentHitsPerDollar == 0 && !handledOnHitEffect)
        {
            scoreManager.AddScore(coinsToAdd * coinMultiplier, TypeOfScore.coins, pos);
            handledOnHitEffect = true;
        }
        
        base.AddScore(amount, typeOfScore, pos);
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.collider.GetComponent<BoardComponent>())
        {
            handledOnHitEffect = false;
        }
    }
}
