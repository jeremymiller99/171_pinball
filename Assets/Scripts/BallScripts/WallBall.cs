using System.Drawing;
using UnityEngine;

public class WallBall : MonoBehaviour
{
    [SerializeField] private PhysicsMaterial wallMaterial;
    [SerializeField] private PointAdder pa;
    [SerializeField] private CameraShake camShake;
    [SerializeField] private float bounceForce = 10f;


    void Awake()
    {
        pa = GetComponent<PointAdder>();
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
        if (collision.collider.sharedMaterial == wallMaterial)
        {
            FMODUnity.RuntimeManager.PlayOneShot("event:/collide_points");

            Vector3 forceDir = (-collision.transform.position + collision.collider.transform.position).normalized;
            GetComponent<Rigidbody>().AddForce(forceDir * bounceForce, ForceMode.Impulse);

            if (camShake == null || !camShake.isActiveAndEnabled)
            {
                ResolveCameraShake();
            }
            camShake?.Shake(0.2f, 0.1f);

            pa.AddScore(transform);
        }
    }

}
