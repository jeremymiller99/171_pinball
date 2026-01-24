using UnityEngine;

public class MultAdder : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float multToAdd;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;

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
            scoreManager?.AddMult(multToAdd);
            // Spawn text at the ball's position
            floatingTextSpawner?.SpawnText(collision.collider.transform.position, "x" + multToAdd);
        }
    }

    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("Ball"))
        {
            if (scoreManager == null) EnsureRefs();
            scoreManager?.AddMult(multToAdd);
            // Spawn text at the ball's position
            floatingTextSpawner?.SpawnText(col.transform.position, "x" + multToAdd);
        }
    }

    public void multiplyMultToAdd(float mult)
    {
        multToAdd *= mult;
    }
}
