// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using UnityEngine;

public class InvestorBall : Ball
{
    [SerializeField] private int coinsToAdd = 1;
    [SerializeField] private int componentHitsPerDollar = 12;
    [SerializeField] private bool _handledBonusThisContact;

    protected override int HitIntervalForPopup => componentHitsPerDollar;

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits > 0 && componentHits % componentHitsPerDollar == 0 && !_handledBonusThisContact)
        {
            _handledBonusThisContact = true;
            componentHits = 0;
            base.AddScore(coinsToAdd, TypeOfScore.coins, pos);
        }
        base.AddScore(amount, typeOfScore, pos);
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.collider.GetComponent<BoardComponent>() != null)
            _handledBonusThisContact = false;
    }

    void OnTriggerExit(Collider col)
    {
        if (col.GetComponent<BoardComponent>() != null)
            _handledBonusThisContact = false;
    }
}
