using UnityEngine;

public class PointAdder : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float pointsToAdd;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;

    public float PointsToAdd => pointsToAdd;

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
            GameObject ball = collision.collider.gameObject;
            var (appliedP, appliedM, isGolfFirstHit) = scoreManager != null ? scoreManager.ApplyScoringHit(ball, pointsToAdd, 0f) : (0f, 0f, false);
            Vector3 pos = ball.transform.position;
            Vector3 multOffset = (floatingTextSpawner != null && appliedP != 0f && appliedM != 0f) ? floatingTextSpawner.GetSideBySideOffsetForMultText() : Vector3.zero;
            if (appliedP != 0f)
                floatingTextSpawner?.SpawnPointsText(pos, appliedP >= 0f ? "+" + appliedP : appliedP.ToString(), appliedP);
            if (appliedM != 0f)
                floatingTextSpawner?.SpawnMultText(pos + multOffset, appliedM >= 0f ? "+" + appliedM + " mult" : appliedM + " mult", appliedM);
        }
    }

    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Ball"))
        {
            if (scoreManager == null) EnsureRefs();
            GameObject ball = col.gameObject;
            var (appliedP, appliedM, isGolfFirstHit) = scoreManager != null ? scoreManager.ApplyScoringHit(ball, pointsToAdd, 0f) : (0f, 0f, false);
            Vector3 pos = ball.transform.position;
            Vector3 multOffset = (floatingTextSpawner != null && appliedP != 0f && appliedM != 0f) ? floatingTextSpawner.GetSideBySideOffsetForMultText() : Vector3.zero;
            if (appliedP != 0f)
                floatingTextSpawner?.SpawnPointsText(pos, appliedP >= 0f ? "+" + appliedP : appliedP.ToString(), appliedP);
            if (appliedM != 0f)
                floatingTextSpawner?.SpawnMultText(pos + multOffset, appliedM >= 0f ? "+" + appliedM + " mult" : appliedM + " mult", appliedM);
        }
    }

    /// <summary>
    /// Adds this component's configured points amount, and spawns floating text at the given transform.
    /// Useful for board elements that award points without a collision/trigger (e.g. spinner ticks).
    /// </summary>
    public void AddPoints(Transform spawnAt)
    {
        if (spawnAt == null) return;
        AddPoints(spawnAt.position);
    }

    /// <summary>
    /// Adds this component's configured points amount, and spawns floating text at the given position.
    /// Passes null for ball so Tenzo uses active ball if applicable (e.g. spinner).
    /// </summary>
    public void AddPoints(Vector3 spawnPosition)
    {
        if (scoreManager == null) EnsureRefs();
        var (appliedP, appliedM, isGolfFirstHit) = scoreManager != null ? scoreManager.ApplyScoringHit(null, pointsToAdd, 0f) : (0f, 0f, false);
        Vector3 pos = spawnPosition;
        Vector3 multOffset = (floatingTextSpawner != null && appliedP != 0f && appliedM != 0f) ? floatingTextSpawner.GetSideBySideOffsetForMultText() : Vector3.zero;
        if (appliedP != 0f)
            floatingTextSpawner?.SpawnPointsText(pos, appliedP >= 0f ? "+" + appliedP : appliedP.ToString(), appliedP);
        if (appliedM != 0f)
            floatingTextSpawner?.SpawnMultText(pos + multOffset, appliedM >= 0f ? "+" + appliedM + " mult" : appliedM + " mult", appliedM);
    }

    public void multiplyPointsToAdd(float mult)
    {
        pointsToAdd *= mult;
    }

    /// <summary>
    /// Adds points and spawns text. Use countForTenzo = false for wall bounces (WallBall) so they don't count toward Tenzo's 10.
    /// </summary>
    public void AddScore(Transform pos, bool countForTenzo = true)
    {
        if (pos == null) return;
        if (scoreManager == null) EnsureRefs();
        var (appliedP, appliedM, isGolfFirstHit) = scoreManager != null ? scoreManager.ApplyScoringHit(null, pointsToAdd, 0f, countForTenzo) : (0f, 0f, false);
        Vector3 worldPos = pos.position;
        Vector3 multOffset = (floatingTextSpawner != null && appliedP != 0f && appliedM != 0f) ? floatingTextSpawner.GetSideBySideOffsetForMultText() : Vector3.zero;
        if (appliedP != 0f)
            floatingTextSpawner?.SpawnText(worldPos, appliedP >= 0f ? "+" + appliedP : appliedP.ToString());
        if (appliedM != 0f)
            floatingTextSpawner?.SpawnMultText(worldPos + multOffset, appliedM >= 0f ? "x" + appliedM : appliedM + " mult", appliedM);
    }
}
