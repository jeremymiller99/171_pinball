using System.Drawing;
using UnityEngine;

public class LeprechaunBall : Ball
{
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;
    [SerializeField] private Vector3 textOffset;
    [SerializeField] private int coinsToAdd = 1;


    void Awake()
    {
        gameRulesManager = FindFirstObjectByType<GameRulesManager>();
        floatingTextSpawner = FindFirstObjectByType<FloatingTextSpawner>();
    }


    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<PointAdder>() || collision.collider.GetComponent<MultAdder>())
        {
            gameRulesManager.AddCoins(coinsToAdd);
            floatingTextSpawner.SpawnText(transform.position + textOffset,"$" + coinsToAdd);
        }
    }
}
