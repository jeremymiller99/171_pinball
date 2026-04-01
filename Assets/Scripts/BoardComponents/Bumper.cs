// Updated with Cursor (Composer) by assistant on 2026-03-31.
// Phase 9: Bumper extends BoardComponent; camera shake via ServiceLocator;
// base OnCollisionEnter first for hit count / popups; score via typeOfScore / amountToScore.
// Change: add optional bumperCollider for bumpers whose collider is on a child (e.g. visual).
using UnityEngine;

public class Bumper : BoardComponent
{
    [Tooltip("Assign when the collider is on a child (e.g. visual). " +
        "Leave empty if collider is on this GameObject.")]
    [SerializeField] public Collider bumperCollider;

    [SerializeField] private float bounceForce = 10f;
    private float baseBounceForce;

    [Header("FX")]
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeMagnitude = 0.16f;

    protected override void Awake()
    {
        base.Awake();
        baseBounceForce = bounceForce;
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        if (collision.collider.GetComponent<Ball>() == null)
        {
            return;
        }

        Rigidbody rb = collision.rigidbody;
        Vector3 bumperCenter = bumperCollider != null
            ? bumperCollider.bounds.center
            : transform.position;

        ServiceLocator.Get<AudioManager>()?.PlayBumperHit(bumperCenter);

        Vector3 forceDir = (collision.transform.position - bumperCenter).normalized;
        rb.AddForce(forceDir * baseBounceForce, ForceMode.Impulse);

        if (amountToScore != 0f)
        {
            AddScore();
        }

        CameraShake camShake = ServiceLocator.Get<CameraShake>();
        if (camShake != null && camShake.isActiveAndEnabled)
        {
            camShake.Shake(shakeDuration, shakeMagnitude);
        }
    }

    public void MultiplyBounceForce(float multiplier)
    {
        // Intentionally a no-op: bumper upgrades should not change bounce strength.
    }
}
