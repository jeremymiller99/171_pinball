using UnityEngine;

/// <summary>
/// The "Project" keyword: creates a <see cref="Holoball"/> at a location. When no explicit
/// position is given, the Holoball appears at a random point on a small radius around the
/// origin object. Each caller supplies the Holoball's longevity (activations or time) and
/// may add its own juice (velocity, colour) to the returned instance.
///
/// The Holoball prefab is loaded once from <c>Resources/Holoball</c>, so items that Project
/// don't each need to carry a prefab reference.
/// </summary>
public static class Projector
{
    private const string HoloballResourcePath = "Holoball";
    private const float DefaultSpawnRadius = 0.18f;

    // Approx Holoball radius used to test a candidate spot for component overlap.
    private const float OverlapCheckRadius = 0.25f;
    // Small upward nudge so Holoballs spawn just above the surface, not clipped into it.
    private const float SpawnHeightOffset = 0.15f;
    // How many spots to try before giving up on finding one clear of components.
    private const int PlacementAttempts = 8;

    private static GameObject _cachedPrefab;

    private static GameObject HoloballPrefab
    {
        get
        {
            if (_cachedPrefab == null)
            {
                _cachedPrefab = Resources.Load<GameObject>(HoloballResourcePath);
            }
            return _cachedPrefab;
        }
    }

    /// <summary>
    /// Projects a Holoball from <paramref name="origin"/>. <paramref name="position"/>
    /// defaults to a random point on a small radius around the origin. Returns the spawned
    /// Holoball (or null if the prefab is missing), so callers can add their own velocity
    /// or colour before it starts moving.
    /// </summary>
    public static GameObject Project(
        GameObject origin,
        HoloballLifetime lifetime,
        Vector3? position = null,
        float radius = DefaultSpawnRadius)
    {
        GameObject prefab = HoloballPrefab;
        if (prefab == null)
        {
            Debug.LogWarning(
                $"[Projector] Holoball prefab not found at Resources/{HoloballResourcePath}.");
            return null;
        }

        Vector3 spawnPos = position ?? RandomPointAround(origin, radius);
        GameObject holoball = Object.Instantiate(prefab, spawnPos, Quaternion.identity);

        // Independent materials so recolouring/destroying one Holoball doesn't affect others.
        Ball.EnsureOwnMaterials(holoball);

        Holoball holo = holoball.GetComponent<Holoball>();
        if (holo != null)
        {
            holo.Initialize(lifetime);
        }

        // Track it as an active ball so drain/clear logic sees it like any other.
        BallSpawner spawner = ServiceLocator.Get<BallSpawner>();
        if (spawner != null)
        {
            spawner.ActiveBalls.Add(holoball);
        }

        return holoball;
    }

    /// <summary>
    /// Random point on a horizontal ring of roughly <paramref name="radius"/> around the
    /// origin, biased slightly upward. Retries a few spots to find one that doesn't overlap
    /// a board component, so a Holoball never spawns embedded in one; falls back to the last
    /// candidate if the surrounding area is too crowded to place cleanly.
    /// </summary>
    private static Vector3 RandomPointAround(GameObject origin, float radius)
    {
        Vector3 center = origin != null ? origin.transform.position : Vector3.zero;

        Vector3 candidate = center;
        for (int i = 0; i < PlacementAttempts; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = radius * Random.Range(1f, 1.35f);
            candidate = center + new Vector3(
                Mathf.Cos(angle) * distance, SpawnHeightOffset, Mathf.Sin(angle) * distance);

            if (!OverlapsComponent(candidate))
            {
                return candidate;
            }
        }

        return candidate;
    }

    /// <summary>True if a Holoball placed at <paramref name="position"/> would overlap a board component.</summary>
    private static bool OverlapsComponent(Vector3 position)
    {
        Collider[] hits = Physics.OverlapSphere(position, OverlapCheckRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i].GetComponentInParent<BoardComponent>() != null)
            {
                return true;
            }
        }
        return false;
    }
}
