using UnityEngine;
using FMODUnity;

public class MultAdder : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private float multToAdd;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;
    [Tooltip("When multiplier is disabled (e.g. Cursed Multiplier), red '0' spawns this far to the left of the hit so it appears left of the points text.")]
    [SerializeField] private float blockedMultTextOffset = 0.7f;

    public float MultToAdd => multToAdd;

    private void Awake()
    {
        EnsureRefs();
    }

    private void EnsureRefs()
    {
        if (scoreManager == null)
        {
#if UNITY_2022_2_OR_NEWER
            scoreManager = FindFirstObjectByType<ScoreManager>();
#else
            scoreManager = FindObjectOfType<ScoreManager>();
#endif
        }

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

    private bool IsMultGainBlocked()
    {
        if (gameRulesManager == null) EnsureRefs();
        return gameRulesManager != null && gameRulesManager.IsMultiplierDisabled();
    }

    private Vector3 GetBlockedMultSpawnPosition(Vector3 hitPosition)
    {
        Camera cam = Camera.main;
        float offset = Mathf.Max(0f, blockedMultTextOffset);
        Vector3 left = cam != null ? -cam.transform.right : Vector3.left;
        return hitPosition + left * offset;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ball"))
        {
            Vector3 pos = collision.collider.transform.position;
            if (scoreManager == null) EnsureRefs();
            FMODUnity.RuntimeManager.PlayOneShot("event:/collide_mult");
            scoreManager?.AddMult(multToAdd);
            bool blocked = IsMultGainBlocked();
            if (blocked)
                floatingTextSpawner?.SpawnMultText(GetBlockedMultSpawnPosition(pos), "x0", 0f);
            else if (multToAdd > 0f)
                floatingTextSpawner?.SpawnMultText(pos, "x" + multToAdd, multToAdd);
        }
    }

    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Ball"))
        {
            Vector3 pos = col.transform.position;
            if (scoreManager == null) EnsureRefs();
            scoreManager?.AddMult(multToAdd);
            bool blocked = IsMultGainBlocked();
            if (blocked)
                floatingTextSpawner?.SpawnMultText(GetBlockedMultSpawnPosition(pos), "x0", 0f);
            else if (multToAdd > 0f)
                floatingTextSpawner?.SpawnMultText(pos, "x" + multToAdd, multToAdd);
        }
    }

    public void multiplyMultToAdd(float mult)
    {
        multToAdd *= mult;
    }
}
