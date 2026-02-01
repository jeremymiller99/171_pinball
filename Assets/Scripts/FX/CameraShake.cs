using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Shake Feel")]
    [Tooltip("How aggressively the shake decays. Lower = smoother, longer sustain. Higher = snappier punch.")]
    [SerializeField] private float decayExponent = 2f;
    
    [Tooltip("Initial kick multiplier - makes the first frame extra punchy.")]
    [SerializeField] private float initialKickMultiplier = 1.8f;
    
    [Tooltip("How fast the shake oscillates. Lower = slower, smoother waves. Higher = more frantic.")]
    [SerializeField] private float shakeFrequency = 12f;
    
    [Tooltip("How smoothly the camera lerps to each shake position. Higher = smoother motion.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float smoothing = 0.4f;

    private Vector3 originalPos;
    private Coroutine _activeShake;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            // Keep the first instance (should live in GameplayCore).
            // Don't destroy to avoid surprises; just don't register.
        }
        originalPos = transform.localPosition;
    }

    public void Shake(float duration, float magnitude)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }
        
        // If already shaking, combine the new shake additively for juicy stacking.
        if (_activeShake != null)
        {
            StopCoroutine(_activeShake);
            // Reset position before starting new shake to avoid drift.
            transform.localPosition = originalPos;
        }
        
        _activeShake = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private System.Collections.IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        bool firstFrame = true;
        Vector3 currentOffset = Vector3.zero;
        Vector3 targetOffset = Vector3.zero;
        float nextTargetTime = 0f;

        while (elapsed < duration)
        {
            // Ease-out decay: starts strong, fades smoothly.
            float t = elapsed / duration;
            float decay = Mathf.Pow(1f - t, decayExponent);
            
            // Extra kick on first frame for that satisfying pop.
            float kickMult = firstFrame ? initialKickMultiplier : 1f;
            firstFrame = false;
            
            float currentMag = magnitude * decay * kickMult;

            // Generate new target offset based on frequency (less often = smoother, less twitchy).
            if (elapsed >= nextTargetTime)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                targetOffset = new Vector3(
                    Mathf.Cos(angle) * currentMag,
                    Mathf.Sin(angle) * currentMag,
                    0f
                );
                nextTargetTime = elapsed + (1f / shakeFrequency);
            }
            else
            {
                // Scale existing target by current magnitude (so it decays smoothly).
                targetOffset = targetOffset.normalized * currentMag;
            }

            // Smoothly interpolate toward target for less twitchy motion.
            float lerpSpeed = smoothing * 60f * Time.unscaledDeltaTime;
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, lerpSpeed);

            transform.localPosition = originalPos + currentOffset;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Smooth return to original position.
        float returnTime = 0.08f;
        float returnElapsed = 0f;
        Vector3 endPos = transform.localPosition;
        
        while (returnElapsed < returnTime)
        {
            returnElapsed += Time.unscaledDeltaTime;
            float rt = returnElapsed / returnTime;
            rt = 1f - Mathf.Pow(1f - rt, 2f); // Ease-out.
            transform.localPosition = Vector3.Lerp(endPos, originalPos, rt);
            yield return null;
        }

        transform.localPosition = originalPos;
        _activeShake = null;
    }
}
