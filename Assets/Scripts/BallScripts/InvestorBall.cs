using UnityEngine;

public class InvestorBall : Ball
{
    [SerializeField] private int coinsToAdd = 1;
    [SerializeField] private int componentHitsPerDollar = 12;
    [SerializeField] private bool _handledBonusThisContact;

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits > 0 && componentHits % componentHitsPerDollar == 0 && !_handledBonusThisContact)
        {
            _handledBonusThisContact = true;
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
