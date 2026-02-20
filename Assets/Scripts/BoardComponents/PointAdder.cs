// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
/*
using UnityEngine;

public class PointAdder : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float pointsToAdd;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;

    private float basePointsToAdd;
    private int upgradeCount;
    private float pointsMultiplier = 1f;

    public float PointsToAdd => GetEffectivePointsToAdd();

    private void Awake()
    {
        basePointsToAdd = pointsToAdd;

        EnsureRefs();
    }

    private float GetEffectivePointsToAdd()
    {
        return (basePointsToAdd * (1 + upgradeCount)) * pointsMultiplier;
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

    private static float GetBallPointsAwardMultiplier(Component ballCollider)
    {
        if (ballCollider == null) return 1f;

        Ball ball = ballCollider.GetComponent<Ball>();
        if (ball == null)
        {
            ball = ballCollider.GetComponentInParent<Ball>();
        }

        if (ball == null) return 1f;

        float m = ball.PointsAwardMultiplier;
        if (m <= 0f) return 0f;
        return m;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ball"))
        {
            if (scoreManager == null) EnsureRefs();
            if (scoreManager == null)
                return;

            float ballMult = GetBallPointsAwardMultiplier(collision.collider);
            float applied = scoreManager.AddPointsScaledDeferredUi(GetEffectivePointsToAdd() * ballMult);

            // Spawn blue points text at the ball's position; only increment HUD when the popup arrives.
            if (floatingTextSpawner != null)
            {
                floatingTextSpawner.SpawnPointsText(
                    collision.collider.transform.position,
                    "+" + applied,
                    applied,
                    () => scoreManager.ApplyDeferredPointsUi(applied));
            }
            else
            {
                scoreManager.ApplyDeferredPointsUi(applied);
            }
        }
    }

    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Ball"))
        {
            if (scoreManager == null) EnsureRefs();

            float ballMult = GetBallPointsAwardMultiplier(col);
            float applied = scoreManager.AddPointsScaledDeferredUi(GetEffectivePointsToAdd() * ballMult);

            // Spawn blue points text at the ball's position; only increment HUD when the popup arrives.
            if (floatingTextSpawner != null)
            {
                floatingTextSpawner.SpawnPointsText(
                    col.transform.position,
                    "+" + applied,
                    applied,
                    () => scoreManager.ApplyDeferredPointsUi(applied));
            }
            else
            {
                scoreManager.ApplyDeferredPointsUi(applied);
            }
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
        float applied = scoreManager.AddPointsScaledDeferredUi(GetEffectivePointsToAdd());

        if (floatingTextSpawner != null)
        {
            floatingTextSpawner.SpawnPointsText(
                spawnPosition,
                "+" + applied,
                applied,
                () => scoreManager.ApplyDeferredPointsUi(applied));
        }
        else
        {
            scoreManager.ApplyDeferredPointsUi(applied);
        }
    }

    public void multiplyPointsToAdd(float mult)
    {
        pointsMultiplier *= mult;
    }

    public void UpgradeAddBaseValue()
    {
        upgradeCount++;
    }

    public void AddScore(Transform pos)
    {
        if (scoreManager == null) EnsureRefs();
        float ballMult = GetBallPointsAwardMultiplier(pos);
        float applied = scoreManager != null ? scoreManager.AddPointsScaled(GetEffectivePointsToAdd() * ballMult) : 0f;
        // Spawn text at the ball's position.
        // Use SpawnPointsText so values compact/round consistently with score UI.
        floatingTextSpawner?.SpawnPointsText(pos.position, "+" + applied, applied);
    }
}
*/