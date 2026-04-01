// Updated with Cursor (Composer) by assistant on 2026-03-31.
using UnityEngine;

/// <summary>
/// Board component: when the ball collides with this object, adds $2 to the player (once per target).
/// Just add this script—no Inspector setup. CoinController and FloatingTextSpawner are found at runtime.
/// </summary>
public class CoinAdder : MonoBehaviour
{
    private const int CoinsToAdd = 2;

    [SerializeField] private CoinController coinController;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;

    private bool _hasAwarded;

    private void Awake()
    {
        EnsureRefs();
    }

    private void EnsureRefs()
    {
        if (coinController == null)
            coinController = ServiceLocator.Get<CoinController>();

        if (floatingTextSpawner == null)
            floatingTextSpawner = ServiceLocator.Get<FloatingTextSpawner>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_hasAwarded) return;
        if (!collision.collider.CompareTag("Ball")) return;

        if (coinController == null) EnsureRefs();
        int applied = coinController != null
            ? coinController.AddCoinsScaledDeferredUi(CoinsToAdd)
            : 0;
        if (applied > 0)
        {
            floatingTextSpawner?.SpawnGoldText(
                collision.collider.transform.position,
                "+$" + applied,
                applied,
                () => coinController?.ApplyDeferredCoinsUi(applied));
        }
        _hasAwarded = true;
    }

    void OnTriggerEnter(Collider col)
    {
        if (_hasAwarded) return;
        if (!col.CompareTag("Ball")) return;

        if (coinController == null) EnsureRefs();
        int applied = coinController != null
            ? coinController.AddCoinsScaledDeferredUi(CoinsToAdd)
            : 0;
        if (applied > 0)
        {
            floatingTextSpawner?.SpawnGoldText(
                col.transform.position,
                "+$" + applied,
                applied,
                () => coinController?.ApplyDeferredCoinsUi(applied));
        }
        _hasAwarded = true;
    }
}
