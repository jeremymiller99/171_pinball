using UnityEngine;
using FMODUnity;

public class MultAdder : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float multToAdd;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;

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
        if (collision.collider.CompareTag("Ball"))
        {
            if (scoreManager == null) EnsureRefs();
            FMODUnity.RuntimeManager.PlayOneShot("event:/collide_mult");
            GameObject ball = collision.collider.gameObject;
            var (appliedP, appliedM, isGolfFirstHit) = scoreManager != null ? scoreManager.ApplyScoringHit(ball, 0f, multToAdd) : (0f, 0f, false);
            Vector3 pos = ball.transform.position;
            Vector3 multOffset = (floatingTextSpawner != null && appliedP != 0f && appliedM != 0f) ? floatingTextSpawner.GetSideBySideOffsetForMultText() : Vector3.zero;
            if (appliedP != 0f)
                floatingTextSpawner?.SpawnPointsText(pos, appliedP >= 0f ? "+" + appliedP : appliedP.ToString(), appliedP);
            if (appliedM != 0f)
                floatingTextSpawner?.SpawnMultText(pos + multOffset, appliedM >= 0f ? "x" + appliedM : appliedM + " mult", appliedM);
        }
    }

    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Ball"))
        {
            if (scoreManager == null) EnsureRefs();
            GameObject ball = col.gameObject;
            var (appliedP, appliedM, isGolfFirstHit) = scoreManager != null ? scoreManager.ApplyScoringHit(ball, 0f, multToAdd) : (0f, 0f, false);
            Vector3 pos = ball.transform.position;
            Vector3 multOffset = (floatingTextSpawner != null && appliedP != 0f && appliedM != 0f) ? floatingTextSpawner.GetSideBySideOffsetForMultText() : Vector3.zero;
            if (appliedP != 0f)
                floatingTextSpawner?.SpawnPointsText(pos, appliedP >= 0f ? "+" + appliedP : appliedP.ToString(), appliedP);
            if (appliedM != 0f)
                floatingTextSpawner?.SpawnMultText(pos + multOffset, appliedM >= 0f ? "x" + appliedM : appliedM + " mult", appliedM);
        }
    }

    public void multiplyMultToAdd(float mult)
    {
        multToAdd *= mult;
    }
}
