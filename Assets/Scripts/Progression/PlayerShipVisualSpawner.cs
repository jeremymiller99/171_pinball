using UnityEngine;

/// <summary>
/// Place this in the board scene where you want the player's active ship to spawn.
/// It reads the active ship from GameSession and spawns its prefab.
/// </summary>
public class PlayerShipVisualSpawner : MonoBehaviour
{
    private void Start()
    {
        if (GameSession.Instance == null || GameSession.Instance.ActiveShip == null)
        {
            return;
        }

        PlayerShipDefinition activeShip = GameSession.Instance.ActiveShip;
        if (activeShip.shipModelPrefab == null)
        {
            return;
        }

        GameObject spawned = Instantiate(activeShip.shipModelPrefab, transform.position, transform.rotation, transform);

        PlayerShipVisual visual = spawned.GetComponent<PlayerShipVisual>();
        if (visual == null)
        {
            visual = spawned.AddComponent<PlayerShipVisual>();
        }

        visual.Init(activeShip);

        // If a flight controller is present on this spawn point, hand off the ship so it
        // can fly the entry path and animate to/from the shop. Otherwise the ship just
        // hovers in place (legacy behavior).
        PlayerShipFlightController flight = GetComponent<PlayerShipFlightController>();
        if (flight != null)
        {
            flight.Bind(spawned.transform, visual);
        }
    }
}
