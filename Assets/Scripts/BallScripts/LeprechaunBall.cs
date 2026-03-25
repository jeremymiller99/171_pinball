// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using UnityEngine;
using UnityEngine.XR;

public class LeprechaunBall : Ball
{
    [SerializeField] private int coinsToAdd = 1;
    [SerializeField] private int componentHitsPerDollar = 5;
    [SerializeField] private bool handledOnHitEffect = false;

    protected override int HitIntervalForPopup => componentHitsPerDollar;

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits % componentHitsPerDollar == 0 && !handledOnHitEffect)
        {
            componentHits = 0;
            base.AddScore(coinsToAdd, TypeOfScore.coins, pos);
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
