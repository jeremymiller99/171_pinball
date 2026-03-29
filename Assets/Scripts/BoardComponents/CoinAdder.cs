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
            gameRulesManager = ServiceLocator.Get<GameRulesManager>();
        }

        if (floatingTextSpawner == null)
        {
            floatingTextSpawner = ServiceLocator.Get<FloatingTextSpawner>();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_hasAwarded) return;
        if (!collision.collider.CompareTag("Ball")) return;

        if (gameRulesManager == null) EnsureRefs();
        int applied = gameRulesManager != null
            ? gameRulesManager.AddCoinsScaledDeferredUi(CoinsToAdd)
            : 0;
        if (applied > 0)
        {
            floatingTextSpawner?.SpawnGoldText(
                collision.collider.transform.position,
                "+$" + applied,
                applied,
                () => gameRulesManager.ApplyDeferredCoinsUi(applied));
        }
        _hasAwarded = true;
    }

    void OnTriggerEnter(Collider col)
    {
        if (_hasAwarded) return;
        if (!col.CompareTag("Ball")) return;

        if (gameRulesManager == null) EnsureRefs();
        int applied = gameRulesManager != null
            ? gameRulesManager.AddCoinsScaledDeferredUi(CoinsToAdd)
            : 0;
        if (applied > 0)
        {
            floatingTextSpawner?.SpawnGoldText(
                col.transform.position,
                "+$" + applied,
                applied,
                () => gameRulesManager.ApplyDeferredCoinsUi(applied));
        }
        _hasAwarded = true;
    }
}
