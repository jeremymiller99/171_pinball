using UnityEngine;

public class Bumper : MonoBehaviour
{
    [SerializeField] private CameraShake camShake;
    [SerializeField] private float bounceForce = 10f;

    private void Awake()
    {
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

#if UNITY_2022_2_OR_NEWER
        camShake = FindFirstObjectByType<CameraShake>();
#else
        camShake = FindObjectOfType<CameraShake>();
#endif
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ball")){
            Rigidbody rb = collision.rigidbody;

            Vector3 forceDir = (collision.transform.position - transform.position).normalized;
            rb.AddForce(forceDir * bounceForce, ForceMode.Impulse);

            if (camShake == null || !camShake.isActiveAndEnabled)
            {
                ResolveCameraShake();
            }
            camShake?.Shake(0.2f, 0.1f);
        }
    }   
}
