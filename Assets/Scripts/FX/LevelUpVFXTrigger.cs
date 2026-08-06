// Generated with Antigravity by jjmil on 2026-04-09.
// Firework SFX hook added by Claude Code (Opus 4.7) for jjmil on 2026-04-21.
// Level-up banner spawn added by Claude Code (Opus 4.8) for jjmil on 2026-06-04.
// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-05 (ball saved VFX spawn).
// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-05 (ball saved VFX pops at the
// spawn volume center, like the banner and Power Surge, instead of at the ball save lamp).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Header("Power Surge VFX")]
    [Tooltip(
        "Power Surge VFX variations. Exactly one (chosen at random) is spawned " +
        "by PowerSurgeManager at a referenced point when Power Surge mode starts. " +
        "Mirrors the level-up banner spawn; lives here so all board VFX " +
        "is configured in one place."
    )]
    [FormerlySerializedAs("frenzyPrefabs")]
    [SerializeField] private List<GameObject> powerSurgePrefabs = new();

    [Tooltip("Uniform scale applied to the spawned Power Surge VFX.")]
    [FormerlySerializedAs("frenzyScale")]
    [SerializeField] private float powerSurgeScale = 1f;

    [Tooltip(
        "Seconds before the spawned Power Surge VFX is destroyed. " +
        "Set to 0 to disable auto-destroy."
    )]
    [FormerlySerializedAs("frenzyLifetime")]
    [SerializeField] private float powerSurgeLifetime = 3f;

    [Header("Ball Save VFX")]
    [Tooltip(
        "Ball saved VFX variations. Exactly one (chosen at random) is spawned " +
        "by DrainHandler at the center of this spawn volume when a drained ball " +
        "is returned to the launcher. Mirrors the level-up banner spawn; lives " +
        "here so all board VFX is configured in one place."
    )]
    [SerializeField] private List<GameObject> ballSavePrefabs = new();

    [Tooltip("Uniform scale applied to the spawned ball saved VFX.")]
    [SerializeField] private float ballSaveScale = 1f;

    [Tooltip(
        "Seconds before the spawned ball saved VFX is destroyed. " +
        "Set to 0 to disable auto-destroy."
    )]
    [SerializeField] private float ballSaveLifetime = 3f;

    private Collider _spawnVolume;
    private GameRulesManager _rules;
    private bool _armed;

    private void Awake()
    {
        _spawnVolume = GetComponent<Collider>();
        ServiceLocator.Register(this);
    }

    private void OnDestroy()
    {
        if (ServiceLocator.Get<LevelUpVFXTrigger>() == this)
        {
            ServiceLocator.Unregister<LevelUpVFXTrigger>();
        }
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
    /// Spawns exactly one Power Surge VFX prefab, chosen at random from the
    /// available variations, at the given world position. Mirrors
    /// <see cref="SpawnBanner"/> except the location is supplied by the
    /// caller (e.g. the portal or abduction point) rather than the spawn
    /// volume center. Called by <see cref="PowerSurgeManager"/> (which lives
    /// in another scene) so all board VFX is owned by this one script.
    /// Returns the spawned instance, or null if none was spawned.
    /// </summary>
    public GameObject SpawnPowerSurgeVFX(Vector3 position)
    {
        if (powerSurgePrefabs == null || powerSurgePrefabs.Count == 0)
        {
            return null;
        }

        GameObject prefab =
            powerSurgePrefabs[Random.Range(0, powerSurgePrefabs.Count)];

        if (prefab == null) return null;

        GameObject fx = Instantiate(
            prefab,
            position,
            Quaternion.identity
        );

        fx.transform.localScale = Vector3.one * powerSurgeScale;

        if (powerSurgeLifetime > 0f)
        {
            Destroy(fx, powerSurgeLifetime);
        }

        return fx;
    }

    /// <summary>
    /// Spawns exactly one ball saved VFX prefab, chosen at random from the
    /// available variations, at the center of the spawn volume — the same place
    /// the level-up banner pops, rather than at the board's ball save lamp.
    /// Mirrors <see cref="SpawnBanner"/>; called by <see cref="DrainHandler"/>,
    /// which lives in another scene, so all board VFX is owned by this one
    /// script. Returns the spawned instance, or null if none was spawned.
    /// </summary>
    public GameObject SpawnBallSaveVFX()
    {
        if (ballSavePrefabs == null || ballSavePrefabs.Count == 0)
        {
            return null;
        }

        GameObject prefab =
            ballSavePrefabs[Random.Range(0, ballSavePrefabs.Count)];

        if (prefab == null) return null;

        GameObject fx = Instantiate(
            prefab,
            SpawnVolumeCenter(),
            Quaternion.identity
        );

        fx.transform.localScale = Vector3.one * ballSaveScale;

        if (ballSaveLifetime > 0f)
        {
            Destroy(fx, ballSaveLifetime);
        }

        return fx;
    }

    // Inspector debug button (component gear menu): spawns one ball saved VFX
    // at the center of the spawn volume so you can confirm it appears without
    // draining a ball. Best used in Play mode.
    [ContextMenu("Debug/Spawn Ball Save VFX")]
    private void DebugSpawnBallSaveVFX()
    {
        GameObject fx = SpawnBallSaveVFX();

        if (fx == null)
        {
            Debug.LogWarning(
                "[LevelUpVFXTrigger] No ball saved VFX spawned — Ball Save Prefabs " +
                "is empty or its entry is missing on this instance.", this);
            return;
        }

        Debug.Log(
            $"[LevelUpVFXTrigger] Spawned ball saved VFX '{fx.name}' at " +
            $"{fx.transform.position} (scale {fx.transform.localScale.x:0.##}).",
            fx);
    }

    // Inspector debug button (component gear menu): spawns one Power Surge
    // VFX at the center of the spawn volume so you can confirm it appears
    // without triggering Power Surge. Best used in Play mode.
    [ContextMenu("Debug/Spawn Power Surge VFX")]
    private void DebugSpawnPowerSurgeVFX()
    {
        GameObject fx = SpawnPowerSurgeVFX(SpawnVolumeCenter());

        if (fx != null)
        {
            Debug.Log(
                $"[LevelUpVFXTrigger] Spawned Power Surge VFX '{fx.name}' at " +
                $"{fx.transform.position} (scale {fx.transform.localScale.x:0.##}).",
                fx);
        }
    }

    /// <summary>
    /// Spawns one firework explosion — a random entry from <see cref="fireworkPrefabs"/>,
    /// the same burst used on level-up — at a world position, with the level-up scale
    /// range and firework SFX. Lets other systems reuse the level-up firework as a
    /// one-off impact effect (e.g. a Fireworks bumper's projectile landing). Returns
    /// the spawned instance, or null if none was spawned.
    ///
    /// <paramref name="scaleOverride"/> sets a fixed uniform scale; leave it at 0 (or
    /// negative) to use the level-up random scale range, which is too large for small
    /// impact effects.
    /// </summary>
    public GameObject SpawnFireworkExplosion(Vector3 position, float scaleOverride = 0f)
    {
        if (fireworkPrefabs == null || fireworkPrefabs.Count == 0)
        {
            return null;
        }

        GameObject prefab =
            fireworkPrefabs[Random.Range(0, fireworkPrefabs.Count)];

        if (prefab == null) return null;

        GameObject fx = Instantiate(
            prefab,
            position,
            Quaternion.identity
        );

        fx.transform.localScale = Vector3.one * (scaleOverride > 0f
            ? scaleOverride
            : Random.Range(minScale, maxScale));

        ServiceLocator.Get<AudioManager>()?.PlayFireworks(position);

        if (fireworkLifetime > 0f)
        {
            Destroy(fx, fireworkLifetime);
        }

        return fx;
    }

    /// <summary>
    /// Center of the spawn volume — where the banner and the ball saved VFX pop.
    /// Resolves the collider on demand so the context-menu debug spawns work even
    /// when <see cref="Awake"/> has not run (edit mode).
    /// </summary>
    private Vector3 SpawnVolumeCenter()
    {
        Collider vol = _spawnVolume != null
            ? _spawnVolume
            : GetComponent<Collider>();

        return vol != null ? vol.bounds.center : transform.position;
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
