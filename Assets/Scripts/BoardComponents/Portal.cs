// Generated with Cursor AI (GPT-5.2), by OpenAI, 2026-02-17.
// Change: add configurable portal exit speed boost.
// Change: fixed spawn position and predictable exit velocity (entry direction/speed ignored).
// Updated with Cursor (Composer) by assistant, for jjmil, on 2026-04-01 (optional BoardLight toggle
// on entrance / after exit teleport).
// Updated with Cursor (Composer) by assistant, for jjmil, on 2026-04-01 (BoardLight flash N times
// then off).
// Updated by Claude (Opus 4.8), for jjmil, on 2026-06-04 (configurable delay: ball is held hidden
// inside the portal before exiting).
using System.Collections;
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

    [Tooltip("Seconds the ball is held (hidden + frozen) inside the portal before it exits. Use 0 for instant teleport.")]
    [Min(0f)]
    public float teleportDelay = 2f;

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

        // Capture the entry speed now, before we freeze the ball, so it can exit with the same speed.
        float entrySpeed = rb.linearVelocity.magnitude;

        // Claim the teleport now so the ball can't re-trigger this (or the exit) portal while held.
        traveller.lastTeleportTime = Time.time;

        // Flash both lights immediately on entry: the entrance to confirm the ball went in, and the
        // exit to warn the player a ball is about to come out (while it waits inside the portal).
        if (entranceBoardLight != null)
        {
            entranceBoardLight.FlashLitVersusOffThenOff(
                boardLightFlashCycles,
                boardLightFlashFullCycleSeconds);
        }

        if (exitBoardLight != null)
        {
            exitBoardLight.FlashLitVersusOffThenOff(
                boardLightFlashCycles,
                boardLightFlashFullCycleSeconds);
        }

        ServiceLocator.Get<AudioManager>()?.PlayPortal(transform.position);

        if (teleportDelay > 0.001f)
        {
            StartCoroutine(TeleportAfterDelay(other, rb, traveller, entrySpeed));
        }
        else
        {
            CompleteTeleport(other, rb, traveller, entrySpeed);
        }
    }

    private IEnumerator TeleportAfterDelay(Collider other, Rigidbody rb, PortalTraveller traveller, float entrySpeed)
    {
        // Hide and freeze the ball while it's "inside" the portal.
        Renderer[] renderers = other.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.enabled = false;
        }

        bool wasKinematic = rb.isKinematic;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        float elapsed = 0f;
        while (elapsed < teleportDelay)
        {
            // Bail out if the ball was destroyed/disabled while waiting.
            if (rb == null || other == null || !other.gameObject.activeInHierarchy)
            {
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        foreach (Renderer r in renderers)
        {
            if (r != null)
            {
                r.enabled = true;
            }
        }

        rb.isKinematic = wasKinematic;
        CompleteTeleport(other, rb, traveller, entrySpeed);
    }

    private void CompleteTeleport(Collider other, Rigidbody rb, PortalTraveller traveller, float entrySpeed)
    {
        // --- POSITION: Fixed spawn at exit center (entry position ignored for predictability) ---
        float spawnDist = exitOffset > 0.001f ? exitOffset : 0.5f;
        Vector3 newWorldPos = portalExit.position + portalExit.forward * spawnDist;
        other.transform.position = newWorldPos;

        // --- ROTATION: Align with exit portal ---
        Quaternion portalDeltaRot = portalExit.rotation * Quaternion.Inverse(transform.rotation);
        other.transform.rotation = portalDeltaRot * other.transform.rotation;

        // --- VELOCITY: Exit straight out of the exit portal, at the same speed the ball entered with
        // (entry direction is ignored, only the speed magnitude is preserved). ---
        float finalSpeed = entrySpeed;

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

        // Refresh the cooldown stamp so the ball doesn't immediately re-enter the exit portal.
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
    }
}