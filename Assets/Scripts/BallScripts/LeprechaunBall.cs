// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
using UnityEngine;

public class LeprechaunBall : Ball
{
    [SerializeField] private int coinsToAdd = 1;
    [SerializeField] private int componentHitsPerDollar = 5;

    private int _componentHitsSinceLastDollar;


    void OnCollisionEnter(Collision collision)
    {
        BoardComponent component = collision.collider.GetComponent<BoardComponent>();
        if (!component) return;
        
        AddScore(component.amountToScore, component.typeOfScore, transform);     
        HandleParticles(collision);
        _componentHitsSinceLastDollar++;
        int hitsPerDollar = Mathf.Max(1, componentHitsPerDollar);
        int awardCount = _componentHitsSinceLastDollar / hitsPerDollar;
        _componentHitsSinceLastDollar %= hitsPerDollar;
        if (awardCount <= 0) return;        
        int coinsToAward = coinsToAdd * awardCount;
        AddScore(coinsToAward, TypeOfScore.coins, transform);
        
    }
}
