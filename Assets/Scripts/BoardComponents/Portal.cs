// Generated with Cursor AI (GPT-5.2), by OpenAI, 2026-02-17.
// Change: add configurable portal exit speed boost.
using UnityEngine;

public class Portal : MonoBehaviour
{
    [SerializeField] private CameraShake camShake;
    public Transform portalExit;

    [Header("Settings")]
    [Tooltip("How far in front of the exit portal to place the object after teleport.")]
    public float exitOffset = 1f;

    [Tooltip("Maximum random angle (in degrees) to deviate from the exact forward direction of the exit portal.")]
    public float exitRandomAngle = 5f;

    [Tooltip("If true, use this fixed exit speed instead of preserving the incoming speed.")]
    public bool overrideExitSpeed = false;

    [Tooltip("Speed to use when overrideExitSpeed is true.")]
    public float exitSpeed = 10f;

    [Tooltip("Additional speed added when exiting the portal.")]
    [SerializeField] private float extraExitSpeed = 5f;

    [Header("FX")]
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeMagnitude = 0.16f;

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

        camShake = FindFirstObjectByType<CameraShake>();
    }

    private void OnTriggerEnter(Collider other)
    {
        
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        PortalTraveller traveller = other.GetComponent<PortalTraveller>();
        if (traveller == null) return;

        if (Time.time - traveller.lastTeleportTime < traveller.teleportCooldown)
            return;

        // --- POSITION ---
        Vector3 localPos = transform.InverseTransformPoint(other.transform.position);
        Vector3 newWorldPos = portalExit.TransformPoint(localPos);
        newWorldPos += portalExit.forward * exitOffset;
        if (other.transform.position != newWorldPos)
        {
            FMODUnity.RuntimeManager.PlayOneShot("event:/effect_portal");
        }
        other.transform.position = newWorldPos;

        // --- ROTATION (optional � keeps orientation relative to portals) ---
        Quaternion portalDeltaRot = portalExit.rotation * Quaternion.Inverse(transform.rotation);
        other.transform.rotation = portalDeltaRot * other.transform.rotation;

        // --- VELOCITY: ALWAYS SHOOT FORWARD WITH SMALL RANDOM SPREAD ---
        // Get current speed (magnitude) to preserve momentum
        float currentSpeed = rb.linearVelocity.magnitude;
        if (overrideExitSpeed)
        {
            currentSpeed = exitSpeed;
        }

        currentSpeed = Mathf.Max(0f, currentSpeed + extraExitSpeed);

        // Base direction is straight out of the exit portal
        Vector3 baseDir = portalExit.forward;

        // Random small spread in local yaw/pitch, but always generally forward
        float yaw = Random.Range(-exitRandomAngle, exitRandomAngle);     // left/right

        // Apply the random yaw/pitch around the exit portal's local axes
        Quaternion spreadRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 exitDir = spreadRot * baseDir;
        exitDir.Normalize();

        // Final velocity
        rb.linearVelocity = exitDir * currentSpeed;

        // Optional: still rotate angular velocity to match portal orientation
        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);
        Vector3 newWorldAngularVel = portalExit.TransformDirection(localAngularVel);
        rb.angularVelocity = newWorldAngularVel;

        traveller.lastTeleportTime = Time.time;

        if (camShake == null || !camShake.isActiveAndEnabled)
        {
            ResolveCameraShake();
        }
        camShake?.Shake(shakeDuration, shakeMagnitude);
    }

    public void MultiplyExitSpeed(float multiplier)
    {
        if (multiplier <= 0f)
        {
            return;
        }

        exitSpeed = Mathf.Max(0f, exitSpeed * multiplier);
    }
}
