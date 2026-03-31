// Generated with Cursor on 2026-03-15.
using UnityEngine;

[System.Serializable]
public class BoidShipPrefabEntry
{
    [Tooltip("Ship prefab to spawn.")]
    public GameObject prefab;
    [Tooltip("Scale multiplier (1 = normal, 0.6 = small, 0.4 = smaller).")]
    [Range(0.1f, 2f)]
    public float scaleMultiplier = 1f;
}

public class BoidShipSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private BoidShipPrefabEntry[] shipPrefabs = new BoidShipPrefabEntry[3];

    [Header("Area")]
    [Tooltip("Collider whose bounds define where ships spawn and fly. Use a BoxCollider or mesh collider.")]
    [SerializeField] private Collider moveableArea;

    [Header("Spawn")]
    [SerializeField] private int shipCount = 10;

    [Header("Movement")]
    [SerializeField] private float shipSpeedMin = 1f;
    [SerializeField] private float shipSpeedMax = 2.5f;

    private void Start()
    {
        if (!HasValidPrefabs() || moveableArea == null)
        {
            return;
        }

        for (int i = 0; i < shipCount; i++)
        {
            SpawnShip();
        }
    }

    private bool HasValidPrefabs()
    {
        if (shipPrefabs == null)
        {
            return false;
        }

        foreach (BoidShipPrefabEntry entry in shipPrefabs)
        {
            if (entry != null && entry.prefab != null)
            {
                return true;
            }
        }

        return false;
    }

    private void SpawnShip()
    {
        Vector3 spawnPos = RandomPointInBounds(moveableArea.bounds);

        BoidShipPrefabEntry entry = GetRandomPrefabEntry();
        if (entry == null || entry.prefab == null)
        {
            return;
        }

        GameObject ship = Instantiate(entry.prefab, spawnPos, Quaternion.identity, transform);
        ship.transform.localScale = ship.transform.localScale * entry.scaleMultiplier;

        BoidShip boidShip = ship.GetComponent<BoidShip>();
        if (boidShip != null)
        {
            float speed = Random.Range(shipSpeedMin, shipSpeedMax);
            boidShip.SetFlyingArea(moveableArea, speed);
        }

        IgnoreCollisionsWithOtherShips(ship);
    }

    private BoidShipPrefabEntry GetRandomPrefabEntry()
    {
        int validCount = 0;
        BoidShipPrefabEntry firstValid = null;

        foreach (BoidShipPrefabEntry entry in shipPrefabs)
        {
            if (entry != null && entry.prefab != null)
            {
                if (firstValid == null)
                {
                    firstValid = entry;
                }

                validCount++;
            }
        }

        if (validCount == 0)
        {
            return firstValid;
        }

        int index = Random.Range(0, validCount);
        int current = 0;

        foreach (BoidShipPrefabEntry entry in shipPrefabs)
        {
            if (entry != null && entry.prefab != null)
            {
                if (current == index)
                {
                    return entry;
                }

                current++;
            }
        }

        return firstValid;
    }

    private void IgnoreCollisionsWithOtherShips(GameObject newShip)
    {
        Collider[] newColliders = newShip.GetComponentsInChildren<Collider>();
        if (newColliders.Length == 0)
        {
            return;
        }

        BoidShip[] allShips = FindObjectsByType<BoidShip>(FindObjectsSortMode.None);
        foreach (BoidShip other in allShips)
        {
            if (other == null || other.gameObject == newShip)
            {
                continue;
            }

            Collider[] otherColliders = other.GetComponentsInChildren<Collider>();
            foreach (Collider a in newColliders)
            {
                foreach (Collider b in otherColliders)
                {
                    if (a != b && a.enabled && b.enabled)
                    {
                        Physics.IgnoreCollision(a, b, true);
                    }
                }
            }
        }
    }

    private static Vector3 RandomPointInBounds(Bounds bounds)
    {
        return new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z));
    }
}
