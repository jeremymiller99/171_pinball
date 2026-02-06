using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Locked target: first hit unlocks it, second hit awards points, later hits act as a bumper (bounce, no points).
/// Attach to the same GameObject that has the Collider the ball hits.
/// </summary>
public class LockedTarget : MonoBehaviour
{
    [Header("Scoring")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private float pointsToAdd = 50f;
    [SerializeField] private FloatingTextSpawner floatingTextSpawner;

    [Header("Bumper behavior (after unlock)")]
    [Tooltip("Force applied on every hit so it bounces like a bumper. 0 = no bounce.")]
    [SerializeField] private float bounceForce = 10f;
    [SerializeField] private CameraShake camShake;

    [Header("Locked look")]
    [Tooltip("Material used when locked (e.g. black). Assign a black material. On first hit, the object restores its regular material.")]
    [SerializeField] private Material lockedMaterial;

    [Header("Unlock")]
    [Tooltip("Optional: fired when the first hit unlocks the target. Hook up extra visuals, sound, etc.")]
    [SerializeField] private UnityEvent onUnlocked;

    private int _hitCount; // 0=locked, 1=unlocked, 2+=gave points, later hits = bumper only
    private Renderer[] _renderers;
    private Material[] _unlockedMaterials; // one per renderer, cached at Start

    private void Awake()
    {
        EnsureRefs();
    }

    private void Start()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        if (_renderers != null && _renderers.Length > 0 && lockedMaterial != null)
        {
            _unlockedMaterials = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _unlockedMaterials[i] = _renderers[i].sharedMaterial;
                    _renderers[i].material = lockedMaterial;
                }
            }
        }
    }

    private void EnsureRefs()
    {
        if (scoreManager == null)
        {
#if UNITY_2022_2_OR_NEWER
            scoreManager = FindFirstObjectByType<ScoreManager>();
#else
            scoreManager = FindObjectOfType<ScoreManager>();
#endif
        }
        if (floatingTextSpawner == null)
        {
#if UNITY_2022_2_OR_NEWER
            floatingTextSpawner = FindFirstObjectByType<FloatingTextSpawner>();
#else
            floatingTextSpawner = FindObjectOfType<FloatingTextSpawner>();
#endif
        }
        if (camShake == null)
        {
            camShake = CameraShake.Instance;
            if (camShake == null)
            {
#if UNITY_2022_2_OR_NEWER
                camShake = FindFirstObjectByType<CameraShake>();
#else
                camShake = FindObjectOfType<CameraShake>();
#endif
            }
        }
    }

    private void ApplyUnlockedLook()
    {
        if (_renderers == null || _unlockedMaterials == null) return;
        for (int i = 0; i < _renderers.Length && i < _unlockedMaterials.Length; i++)
        {
            if (_renderers[i] != null && _unlockedMaterials[i] != null)
                _renderers[i].material = _unlockedMaterials[i];
        }
    }

    private void ApplyBounce(Collision collision)
    {
        if (bounceForce <= 0f) return;
        Rigidbody rb = collision.rigidbody;
        if (rb == null) return;
        Vector3 forceDir = (collision.transform.position - transform.position).normalized;
        rb.AddForce(forceDir * bounceForce, ForceMode.Impulse);
        camShake?.Shake(0.2f, 0.1f);
    }

    private void ApplyBounce(Collider col)
    {
        if (bounceForce <= 0f) return;
        Rigidbody rb = col.attachedRigidbody;
        if (rb == null) return;
        Vector3 forceDir = (col.transform.position - transform.position).normalized;
        rb.AddForce(forceDir * bounceForce, ForceMode.Impulse);
        camShake?.Shake(0.2f, 0.1f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Ball")) return;

        ApplyBounce(collision);

        if (_hitCount == 0)
        {
            _hitCount = 1;
            ApplyUnlockedLook();
            onUnlocked?.Invoke();
            return;
        }
        if (_hitCount == 1)
        {
            _hitCount = 2;
            if (scoreManager == null) EnsureRefs();
            scoreManager?.AddPoints(pointsToAdd);
            Vector3 pos = collision.collider.transform.position;
            floatingTextSpawner?.SpawnText(pos, "+" + Mathf.RoundToInt(pointsToAdd));
        }
    }

    private void OnTriggerEnter(Collider col)
    {
        if (!col.CompareTag("Ball")) return;

        ApplyBounce(col);

        if (_hitCount == 0)
        {
            _hitCount = 1;
            ApplyUnlockedLook();
            onUnlocked?.Invoke();
            return;
        }
        if (_hitCount == 1)
        {
            _hitCount = 2;
            if (scoreManager == null) EnsureRefs();
            scoreManager?.AddPoints(pointsToAdd);
            floatingTextSpawner?.SpawnText(col.transform.position, "+" + Mathf.RoundToInt(pointsToAdd));
        }
    }
}
