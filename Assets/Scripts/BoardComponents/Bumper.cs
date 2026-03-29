// Generated with Cursor AI, 2025-03-15.
// Change: add optional bumperCollider for bumpers whose collider is on a child (e.g. visual).
using UnityEngine;

public class Bumper : MonoBehaviour
{
    [Tooltip("Assign when the collider is on a child (e.g. visual). Leave empty if collider is on this GameObject.")]
    [SerializeField] public Collider bumperCollider;

    [SerializeField] private CameraShake camShake;
    [SerializeField] private float bounceForce = 10f;
    private float baseBounceForce;

    [Header("FX")]
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeMagnitude = 0.16f;

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

        camShake = ServiceLocator.Get<CameraShake>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ball"))
        {
            Rigidbody rb = collision.rigidbody;
            Vector3 bumperCenter = bumperCollider != null ? bumperCollider.bounds.center : transform.position;

            AudioManager.Instance.PlayBumperHit(bumperCenter);

            Vector3 forceDir = (collision.transform.position - bumperCenter).normalized;
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