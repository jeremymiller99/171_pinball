using UnityEngine;

public class WallBall : Ball
{
    [SerializeField] private PhysicsMaterial wallMaterial;
    [SerializeField] private CameraShake camShake;
    [SerializeField] private float bounceForce = 10f;
    [SerializeField] private float pointsOnWall;

    [Header("FX")]
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeMagnitude = 0.16f;

    void Awake()
    {
        base.Awake();
        ResolveCameraShake();
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

            camShake.Shake(shakeDuration, shakeMagnitude);

            scoreManager.AddScore(pointsOnWall, TypeOfScore.points, transform);
        } 
    }

}
