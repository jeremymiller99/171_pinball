using UnityEngine;

public class Portal : MonoBehaviour
{
    [SerializeField] private CameraShake camShake;
    [SerializeField] private PointAdder pa;
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

    private void Awake()
    {
        ResolveCameraShake();
        pa = GetComponent<PointAdder>();
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
        camShake?.Shake(0.2f, 0.1f);

        // Give points
        if (other.CompareTag("Ball"))
        {
            pa.AddPoints(other.transform);
        }
    }
}
