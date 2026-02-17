// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
using UnityEngine;

public class LeprechaunBall : Ball
{
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;
    [SerializeField] private Vector3 textOffset;
    [SerializeField] private int coinsToAdd = 1;
    [SerializeField] private int componentHitsPerDollar = 5;

    private int _componentHitsSinceLastDollar;


    void Awake()
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
        base.OnCollisionEnter(collision);
        if (collision.collider.GetComponent<PointAdder>() || collision.collider.GetComponent<MultAdder>())
        {
            _componentHitsSinceLastDollar++;

            int hitsPerDollar = Mathf.Max(1, componentHitsPerDollar);
            int awardCount = _componentHitsSinceLastDollar / hitsPerDollar;
            _componentHitsSinceLastDollar %= hitsPerDollar;
            if (awardCount <= 0) return;

            EnsureRefs();
            int token = 0;
            int coinsToAward = coinsToAdd * awardCount;
            int applied = gameRulesManager != null
                ? gameRulesManager.AddCoinsScaledDeferredUi(coinsToAward, out token)
                : 0;

            if (floatingTextSpawner != null)
            {
                if (applied > 0)
                {
                    floatingTextSpawner.SpawnGoldText(
                        transform.position + textOffset,
                        "+$" + applied,
                        applied,
                        () => gameRulesManager.ApplyDeferredCoinsUi(applied, token));
                }
            }
        }
    }
}
