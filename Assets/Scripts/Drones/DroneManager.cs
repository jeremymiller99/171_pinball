using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class DroneManager : MonoBehaviour
{
    // Put ONLY unupgraded drones in this list.
    [SerializeField] private List<DroneDefinition> droneDefinitions = new List<DroneDefinition>();
    [SerializeField] private List<GameObject> drones = new List<GameObject>();
    [SerializeField] private Collider droneIdleArea;
    private void Awake()
    {
        ServiceLocator.Register(this);
        ServiceLocator.Get<GameRulesManager>().LevelChanged += Reinforce;
    }

    public void Reinforce()
    {
        if (drones.Count > 0 && Random.Range(0f, 1f) < 0.5f) {
            foreach (GameObject drone in drones)
            {
                if (drone.GetComponent<DroneDefinitionLink>().TryGetDefinition(out DroneDefinition link))
                {
                    if (link.Upgrade != null)
                    {
                        Transform temp = drone.transform;
                        drones.Remove(drone);
                        Destroy(drone);
                        GameObject newDrone = Instantiate(link.Upgrade.Prefab, transform);
                        newDrone.transform.position = temp.position;
                        newDrone.GetComponent<Drone>().SetBox(droneIdleArea);
                        newDrone.transform.rotation = temp.rotation;
                        drones.Add(newDrone);
                        return;
                    }
                }
            }
        }

        while (true)
        {
            DroneDefinition randomDroneDef = droneDefinitions[Random.Range(0, droneDefinitions.Count)];
            if (randomDroneDef.Upgrade)
            {
                GameObject newDrone = Instantiate(randomDroneDef.Prefab, transform);
                newDrone.transform.position = transform.position;
                newDrone.transform.rotation = transform.rotation;
                newDrone.GetComponent<Drone>().SetBox(droneIdleArea);
                drones.Add(newDrone);
                break;
            }
        }
    }
}
