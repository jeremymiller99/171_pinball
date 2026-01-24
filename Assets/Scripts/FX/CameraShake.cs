using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3 originalPos;
    
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
        StopAllCoroutines(); 
        StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private System.Collections.IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = originalPos + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Reset back to OG position when done
        transform.localPosition = originalPos;
    }
}
