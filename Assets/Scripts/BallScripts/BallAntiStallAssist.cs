// Generated with Cursor (GPT-5.2) by OpenAI assistant for jjmil on 2026-02-17.
using UnityEngine;

/// <summary>
/// Prevents long "dead time" when a pinball slows down too much.
/// Disables sleeping on the ball rigidbody and applies a continuous assist force in global -Z.
/// The assist force is stronger at low speeds and weaker at high speeds.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class BallAntiStallAssist : MonoBehaviour
{
    [Header("Assist force (global)")]
    [Min(0f)]
    [Tooltip("Assist acceleration applied when moving at or below speedForMaxAssistMps.")]
    [SerializeField] private float maxAssistAcceleration = 20f;

    [Min(0f)]
    [Tooltip("Assist acceleration applied when moving at or above speedForMinAssistMps.")]
    [SerializeField] private float minAssistAcceleration = 0f;

    [Min(0f)]
    [Tooltip("When speed is at/below this value, assist uses maxAssistAcceleration.")]
    [SerializeField] private float speedForMaxAssistMps = 0.5f;

    [Min(0f)]
    [Tooltip("When speed is at/above this value, assist uses minAssistAcceleration.")]
    [SerializeField] private float speedForMinAssistMps = 20f;

    [Header("Board reference (optional)")]
    [Tooltip("Not required for global -Z assist; kept for optional future tuning/debugging.")]
    [SerializeField] private BoardRoot boardRoot;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ResolveBoardRootIfNeeded();

        if (rb != null)
        {
            // Per-ball: prevent sleeping so a slow ball doesn't get stuck "asleep" indefinitely.
            rb.sleepThreshold = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        if (rb.isKinematic || !rb.useGravity)
        {
            return;
        }

        ResolveBoardRootIfNeeded();

        float speedMps = GetSpeedMps(rb);
        float assistAccel = EvaluateAssistAcceleration(speedMps);
        if (assistAccel <= 0f)
        {
            return;
        }

        if (rb.IsSleeping())
        {
            rb.WakeUp();
        }

        rb.AddForce(Vector3.back * assistAccel, ForceMode.Acceleration);
    }

    private float EvaluateAssistAcceleration(float speedMps)
    {
        float maxA = Mathf.Max(0f, maxAssistAcceleration);
        float minA = Mathf.Max(0f, minAssistAcceleration);

        if (maxA <= 0f && minA <= 0f)
        {
            return 0f;
        }

        // Inverse scaling:
        // - speed <= speedForMaxAssistMps => maxAssistAcceleration
        // - speed >= speedForMinAssistMps => minAssistAcceleration
        // Using InverseLerp with (a > b) intentionally produces the desired inversion.
        float a = Mathf.Max(0f, speedForMinAssistMps);
        float b = Mathf.Max(0f, speedForMaxAssistMps);

        if (Mathf.Approximately(a, b))
        {
            return maxA;
        }

        float t = Mathf.Clamp01(Mathf.InverseLerp(a, b, Mathf.Max(0f, speedMps)));
        return Mathf.Lerp(minA, maxA, t);
    }

    private void ResolveBoardRootIfNeeded()
    {
        if (boardRoot != null)
        {
            return;
        }

#if UNITY_2022_2_OR_NEWER
        boardRoot = FindFirstObjectByType<BoardRoot>();
#else
        boardRoot = FindObjectOfType<BoardRoot>();
#endif
        if (boardRoot != null)
        {
            return;
        }

        // Fallback if the scene is unconventional.
        GameObject board = GameObject.Find("Board");
        if (board != null)
        {
            boardRoot = board.GetComponent<BoardRoot>();
        }
    }

    private static float GetSpeedMps(Rigidbody body)
    {
        return GetVelocity(body).magnitude;
    }

    private static Vector3 GetVelocity(Rigidbody body)
    {
#if UNITY_6000_0_OR_NEWER
        return body != null ? body.linearVelocity : Vector3.zero;
#else
        return body != null ? body.velocity : Vector3.zero;
#endif
    }
}

