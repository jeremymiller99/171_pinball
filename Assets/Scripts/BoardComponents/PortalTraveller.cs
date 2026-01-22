using UnityEngine;

public class PortalTraveller : MonoBehaviour
{
    public float teleportCooldown = 0.1f;

    [HideInInspector]
    public float lastTeleportTime = -999f;
}
