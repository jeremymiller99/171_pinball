using UnityEngine;
using FMODUnity;

public class Bumper : MonoBehaviour
{
    [SerializeField] private CameraShake camShake;
    [SerializeField] private float bounceForce = 10f;
    private float baseBounceForce;

    [Header("FX")]
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeMagnitude = 0.16f;

    [Header("Audio")]
    [SerializeField] private EventReference hitSound;

    private void Awake()
    {
        ResolveCameraShake();
        baseBounceForce = bounceForce;
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
        if (collision.collider.CompareTag("Ball"))
        {
            Rigidbody rb = collision.rigidbody;
            
            AudioManager.Instance.PlayOneShot(hitSound, transform.position);

            Vector3 forceDir = (collision.transform.position - transform.position).normalized;
            rb.AddForce(forceDir * baseBounceForce, ForceMode.Impulse);

            if (camShake == null || !camShake.isActiveAndEnabled)
            {
                ResolveCameraShake();
            }
            camShake?.Shake(shakeDuration, shakeMagnitude);
        }
    }   

    public void MultiplyBounceForce(float multiplier)
    {
        // Intentionally a no-op: bumper upgrades should not change bounce strength.
    }
}