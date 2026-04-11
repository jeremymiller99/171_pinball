// Generated with Antigravity by jjmil on 2026-04-09.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Listens for level-up events and spawns a random number (3-5) of
/// CFXR firework prefab instances at random positions and random
/// scale inside an attached collider volume.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class LevelUpVFXTrigger : MonoBehaviour
{
    [Header("Firework Prefabs")]
    [SerializeField] private List<GameObject> fireworkPrefabs = new();

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

        if (fireworkPrefabs == null
            || fireworkPrefabs.Count == 0
            || _spawnVolume == null)
        {
            return;
        }

        int count = Random.Range(
            minSpawnCount,
            maxSpawnCount + 1
        );

        StartCoroutine(SpawnStaggered(count));
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
