using UnityEngine;

public class WallBall : Ball
{
    [SerializeField] private PhysicsMaterial wallMaterial;
    [SerializeField] private PointAdder pointAdder;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;
    [SerializeField] private CameraShake camShake;
    [SerializeField] private float bounceForce = 10f;

    [Header("FX")]
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeMagnitude = 0.16f;

    void Awake()
    {
        pointAdder = GetComponent<PointAdder>();
        EnsureScoreRefs();
        ResolveCameraShake();
    }

    private void EnsureScoreRefs()
    {
        if (scoreManager == null)
        {
            scoreManager = FindFirstObjectByType<ScoreManager>();
        }

        if (floatingTextSpawner == null)
        {
            floatingTextSpawner = FindFirstObjectByType<FloatingTextSpawner>();
        }
    }

    private void ResolveCameraShake()
    {
        if (camShake != null && camShake.isActiveAndEnabled)
        {
            return;
        }

        camShake = CameraShake.Instance;
        if (camShake != null && camShake.isActiveAndEnabled)
        {
            return;
        }

        camShake = FindFirstObjectByType<CameraShake>();
    }

    void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);
        if (collision.collider.sharedMaterial == wallMaterial)
        {
            FMODUnity.RuntimeManager.PlayOneShot("event:/collide_points");

            Vector3 forceDir = (-collision.transform.position + collision.collider.transform.position).normalized;
            GetComponent<Rigidbody>().AddForce(forceDir * bounceForce, ForceMode.Impulse);

            if (camShake == null || !camShake.isActiveAndEnabled)
            {
                ResolveCameraShake();
            }
            camShake?.Shake(shakeDuration, shakeMagnitude);

            if (scoreManager == null || floatingTextSpawner == null)
            {
                EnsureScoreRefs();
            }

            if (scoreManager != null && pointAdder != null)
            {
                int token = scoreManager.PointsAndMultUiToken;
                float applied = scoreManager.AddPointsScaledDeferredUi(pointAdder.PointsToAdd);

                if (floatingTextSpawner != null)
                {
                    floatingTextSpawner.SpawnPointsText(
                        transform.position,
                        "+" + applied,
                        applied,
                        () => scoreManager.ApplyDeferredPointsUi(applied, token));
                }
                else
                {
                    scoreManager.ApplyDeferredPointsUi(applied, token);
                }
            }
            else
            {
                pointAdder?.AddScore(transform);
            }
        }
    }

}
