using System.Drawing;
using UnityEngine;

public class LeprechaunBall : MonoBehaviour
{
    [SerializeField] private GameRulesManager grm;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;
    [SerializeField] private Vector3 textOffset;
    [SerializeField] private int coinsToAdd = 1;


    void Awake()
    {
        grm = FindFirstObjectByType<GameRulesManager>();
        floatingTextSpawner = FindFirstObjectByType<FloatingTextSpawner>();
    }


    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<PointAdder>() || collision.collider.GetComponent<MultAdder>())
        {
            grm.AddCoins(1);
            floatingTextSpawner.SpawnText(transform.position + textOffset,"$" + coinsToAdd);
        }
    }
}
