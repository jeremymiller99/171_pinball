using UnityEngine;

public class AlienController : MonoBehaviour
{
    [SerializeField] float chanceForSpawn;
    [SerializeField] float tryToSpawnEvery;
    [SerializeField] float timeSinceLastSpawn;
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] AlienShip alienShip;

    void Awake()
    {
        alienShip = GetComponentInChildren<AlienShip>();
        gameRulesManager = FindAnyObjectByType<GameRulesManager>();
    }

    void Update()
    {
        timeSinceLastSpawn += Time.deltaTime;
        if (gameRulesManager.IsShopOpen || alienShip.inPlay) return;

        if (timeSinceLastSpawn > tryToSpawnEvery)
        {
            if (Random.Range(0f, 1f) <= chanceForSpawn)
            {
                alienShip.Spawn();
            }
            timeSinceLastSpawn = 0;
        }
    }
}
