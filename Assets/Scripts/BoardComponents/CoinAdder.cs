using UnityEngine;

/// <summary>
/// Board component: when the ball collides with this object, adds $2 to the player (once per target).
/// Just add this script—no Inspector setup. GameRulesManager and FloatingTextSpawner are found at runtime.
/// </summary>
public class CoinAdder : MonoBehaviour
{
    private const int CoinsToAdd = 2;

    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;

    private bool _hasAwarded;

    private void Awake()
    {
        EnsureRefs();
    }

    private void EnsureRefs()
    {
        if (gameRulesManager == null)
        {
#if UNITY_2022_2_OR_NEWER
            gameRulesManager = FindFirstObjectByType<GameRulesManager>();
#else
            gameRulesManager = FindObjectOfType<GameRulesManager>();
#endif
        }

        if (floatingTextSpawner == null)
        {
#if UNITY_2022_2_OR_NEWER
            floatingTextSpawner = FindFirstObjectByType<FloatingTextSpawner>();
#else
            floatingTextSpawner = FindObjectOfType<FloatingTextSpawner>();
#endif
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_hasAwarded) return;
        if (!collision.collider.CompareTag("Ball")) return;
        if (collision.collider.GetComponent<GolfBallBehavior>() != null || collision.collider.GetComponent<TenzoBallBehavior>() != null) return;

        if (gameRulesManager == null) EnsureRefs();
        gameRulesManager?.AddCoins(CoinsToAdd);
        floatingTextSpawner?.SpawnText(collision.collider.transform.position, "+$" + CoinsToAdd);
        _hasAwarded = true;
    }

    void OnTriggerEnter(Collider col)
    {
        if (_hasAwarded) return;
        if (!col.CompareTag("Ball")) return;
        if (col.GetComponent<GolfBallBehavior>() != null || col.GetComponent<TenzoBallBehavior>() != null) return;

        if (gameRulesManager == null) EnsureRefs();
        gameRulesManager?.AddCoins(CoinsToAdd);
        floatingTextSpawner?.SpawnText(col.transform.position, "+$" + CoinsToAdd);
        _hasAwarded = true;
    }
}
