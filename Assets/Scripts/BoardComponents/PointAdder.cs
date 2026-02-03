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
            float applied = scoreManager != null ? scoreManager.AddPointsScaled(pointsToAdd) : 0f;
            // Spawn blue points text at the ball's position
            floatingTextSpawner?.SpawnPointsText(collision.collider.transform.position, "+" + applied, applied);
        }
    }

    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Ball"))
        {
            if (scoreManager == null) EnsureRefs();
            float applied = scoreManager != null ? scoreManager.AddPointsScaled(pointsToAdd) : 0f;
            // Spawn blue points text at the ball's position
            floatingTextSpawner?.SpawnPointsText(col.transform.position, "+" + applied, applied);
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
    /// </summary>
    public void AddPoints(Vector3 spawnPosition)
    {
        if (scoreManager == null) EnsureRefs();
        float applied = scoreManager != null ? scoreManager.AddPointsScaled(pointsToAdd) : 0f;
        floatingTextSpawner?.SpawnPointsText(spawnPosition, "+" + applied, applied);
    }

    public void multiplyPointsToAdd(float mult)
    {
        pointsToAdd *= mult;
    }
}
