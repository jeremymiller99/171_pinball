// Generated with Antigravity by jjmil on 2026-04-09.
// Firework SFX hook added by Claude Code (Opus 4.7) for jjmil on 2026-04-21.
// Level-up banner spawn added by Claude Code (Opus 4.8) for jjmil on 2026-06-04.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listens for level-up events and spawns a random number (3-5) of
/// CFXR firework prefab instances at random positions and random
/// scale inside an attached collider volume. In addition, exactly one
/// "Level Up" banner prefab (chosen at random from a list of variations)
/// is spawned at the center of the same volume on every level up.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class LevelUpVFXTrigger : MonoBehaviour
{
    [Header("Firework Prefabs")]
    [SerializeField] private List<GameObject> fireworkPrefabs = new();

    [Header("Level-Up Banner")]
    [Tooltip(
        "\"Level Up\" banner variations. Exactly one (chosen at random) " +
        "is spawned at the center of the spawn volume on every level up."
    )]
    [SerializeField] private List<GameObject> bannerPrefabs = new();

    [Tooltip("Uniform scale applied to the spawned banner.")]
    [SerializeField] private float bannerScale = 1f;

    [Tooltip(
        "Seconds before the spawned banner is destroyed. " +
        "Set to 0 to disable auto-destroy."
    )]
    [SerializeField] private float bannerLifetime = 3f;

    [Header("Spawn Count")]
    [SerializeField] private int minSpawnCount = 3;
    [SerializeField] private int maxSpawnCount = 5;

    [Header("Random Scale")]
    [SerializeField] private float minScale = 2f;
    [SerializeField] private float maxScale = 3f;

    [Header("Stagger")]
    [Tooltip(
        "Maximum random delay before each firework spawns."
    )]
    [SerializeField] private float maxLaunchDelay = 1f;

    [Header("Lifetime")]
    [Tooltip(
        "Seconds before each spawned firework is destroyed. " +
        "Set to 0 to disable auto-destroy."
    )]
    [SerializeField] private float fireworkLifetime = 5f;

    private Collider _spawnVolume;
    private GameRulesManager _rules;
    private bool _armed;

    private void Awake()
    {
        _spawnVolume = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        _armed = false;
        ResolveRules();

        if (_rules != null)
        {
            _rules.LevelChanged += OnLevelUp;
            _rules.RoundStarted += OnRoundStarted;
        }
    }

    private void OnDisable()
    {
        if (_rules != null)
        {
            _rules.LevelChanged -= OnLevelUp;
            _rules.RoundStarted -= OnRoundStarted;
        }
    }

    private void OnRoundStarted()
    {
        _armed = true;
    }

    private void ResolveRules()
    {
        if (_rules != null) return;

        _rules = ServiceLocator.Get<GameRulesManager>();
    }

    private void OnLevelUp()
    {
        if (!_armed) return;

        if (_spawnVolume == null) return;

        // Always pop one "Level Up" banner at the center of the volume,
        // independent of the firework burst.
        SpawnBanner();

        if (fireworkPrefabs == null
            || fireworkPrefabs.Count == 0)
        {
            return;
        }

        int count = Random.Range(
            minSpawnCount,
            maxSpawnCount + 1
        );

        StartCoroutine(SpawnStaggered(count));
    }

    /// <summary>
    /// Spawns exactly one banner prefab, chosen at random from the
    /// available variations, at the center of the spawn volume.
    /// </summary>
    private void SpawnBanner()
    {
        if (bannerPrefabs == null || bannerPrefabs.Count == 0)
        {
            return;
        }

        GameObject prefab =
            bannerPrefabs[Random.Range(0, bannerPrefabs.Count)];

        if (prefab == null) return;

        Vector3 center = _spawnVolume.bounds.center;

        GameObject banner = Instantiate(
            prefab,
            center,
            Quaternion.identity
        );

        banner.transform.localScale = Vector3.one * bannerScale;

        if (bannerLifetime > 0f)
        {
            Destroy(banner, bannerLifetime);
        }
    }

    private IEnumerator SpawnStaggered(int count)
    {
        for (int i = 0; i < count; i++)
        {
            float delay = Random.Range(0f, maxLaunchDelay);

            yield return new WaitForSeconds(delay);

            GameObject prefab =
                fireworkPrefabs[Random.Range(
                    0, fireworkPrefabs.Count
                )];

            if (prefab == null) continue;

            Vector3 point = RandomPointInCollider(_spawnVolume);
            float scale = Random.Range(minScale, maxScale);

            GameObject instance = Instantiate(
                prefab,
                point,
                Quaternion.identity
            );

            instance.transform.localScale =
                Vector3.one * scale;

            ServiceLocator.Get<AudioManager>()?.PlayFireworks(point);

            if (fireworkLifetime > 0f)
            {
                Destroy(instance, fireworkLifetime);
            }
        }
    }

    /// <summary>
    /// Returns a random world-space point inside the bounds of the
    /// given collider.
    /// </summary>
    private static Vector3 RandomPointInCollider(Collider col)
    {
        Bounds bounds = col.bounds;

        Vector3 point = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z)
        );

        return point;
    }
}
