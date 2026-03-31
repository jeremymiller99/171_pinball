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
    }
}
