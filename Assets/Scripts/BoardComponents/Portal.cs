// Generated with Cursor AI (GPT-5.2), by OpenAI, 2026-02-17.
// Change: add configurable portal exit speed boost.
// Change: fixed spawn position and predictable exit velocity (entry direction/speed ignored).
// Updated with Cursor (Composer) by assistant, for jjmil, on 2026-04-01 (optional BoardLight toggle
// on entrance / after exit teleport).
// Updated with Cursor (Composer) by assistant, for jjmil, on 2026-04-01 (BoardLight flash N times
// then off).
using UnityEngine;
public class Portal : MonoBehaviour
{
    [SerializeField] private CameraShake camShake;
    public Transform portalExit;
    [SerializeField] private bool canTeleportFromThisPortal = true;

    [Header("Settings")]
    [Tooltip("How far in front of the exit portal to place the object after teleport. Ball always spawns at exit center + this offset (entry position ignored).")]
    public float exitOffset = 0.5f;

    [Tooltip("Maximum random angle (in degrees) to deviate from the exact forward direction. Use 0 for fully predictable exit.")]
    public float exitRandomAngle = 0f;

    [Tooltip("If true, use fixed exit speed (entry speed ignored). Recommended for predictable behavior.")]
    public bool overrideExitSpeed = true;

    [Tooltip("Speed to use when overrideExitSpeed is true.")]
    public float exitSpeed = 10f;

    [Tooltip("Additional speed added when overrideExitSpeed is false (entry speed + this). Ignored when override is true.")]
    [SerializeField] private float extraExitSpeed = 5f;

    [Header("FX")]
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeMagnitude = 0.16f;

    [Header("Board lights (optional)")]
    [Tooltip("Flashes when the ball enters this portal (before teleport). Ends off.")]
    [SerializeField] private BoardLight entranceBoardLight;

    [Tooltip("Flashes after the ball is placed at portalExit. Ends off.")]
    [SerializeField] private BoardLight exitBoardLight;

    [Tooltip("How many full off+on cycles each portal flash runs.")]
    [Min(1)]
    [SerializeField] private int boardLightFlashCycles = 3;

    [Tooltip("Seconds per full off+on cycle (halves each phase). Uses each BoardLight's unscaled " +
             "flash setting.")]
    [Min(0.01f)]
    [SerializeField] private float boardLightFlashFullCycleSeconds = 0.24f;

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

        camShake = ServiceLocator.Get<CameraShake>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canTeleportFromThisPortal)
        {
            return;
        }

        
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        PortalTraveller traveller = other.GetComponent<PortalTraveller>();
        if (traveller == null) return;

        if (Time.time - traveller.lastTeleportTime < traveller.teleportCooldown)
            return;

        if (entranceBoardLight != null)
        {
            entranceBoardLight.FlashLitVersusOffThenOff(
                boardLightFlashCycles,
                boardLightFlashFullCycleSeconds);
        }

        // --- POSITION: Fixed spawn at exit center (entry position ignored for predictability) ---
        float spawnDist = exitOffset > 0.001f ? exitOffset : 0.5f;
        Vector3 newWorldPos = portalExit.position + portalExit.forward * spawnDist;
        if (other.transform.position != newWorldPos)
        {
            ServiceLocator.Get<AudioManager>()?.PlayPortal(transform.position);
        }
        other.transform.position = newWorldPos;

        // --- ROTATION: Align with exit portal ---
        Quaternion portalDeltaRot = portalExit.rotation * Quaternion.Inverse(transform.rotation);
        other.transform.rotation = portalDeltaRot * other.transform.rotation;

        // --- VELOCITY: Fixed direction and speed (entry direction and speed ignored) ---
        float finalSpeed = overrideExitSpeed ? exitSpeed : Mathf.Max(0f, rb.linearVelocity.magnitude + extraExitSpeed);

        // Direction: straight out of exit portal, with optional random spread
        Vector3 exitDir = portalExit.forward;
        if (exitRandomAngle > 0.001f)
        {
            float yaw = Random.Range(-exitRandomAngle, exitRandomAngle);
            exitDir = Quaternion.Euler(0f, yaw, 0f) * exitDir;
        }
        exitDir.Normalize();

        rb.linearVelocity = exitDir * finalSpeed;

        // Zero angular velocity for predictable trajectory (no spin from entry)
        rb.angularVelocity = Vector3.zero;

        traveller.lastTeleportTime = Time.time;

        IPortalTeleportListener listener = other.GetComponent<IPortalTeleportListener>();
        if (listener != null)
        {
            listener.OnTeleportedThroughPortal();
        }

        if (camShake == null || !camShake.isActiveAndEnabled)
        {
            ResolveCameraShake();
        }
        camShake?.Shake(shakeDuration, shakeMagnitude);

        if (exitBoardLight != null)
        {
            exitBoardLight.FlashLitVersusOffThenOff(
                boardLightFlashCycles,
                boardLightFlashFullCycleSeconds);
        }
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