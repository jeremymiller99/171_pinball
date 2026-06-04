// Updated 2026-03-31: hover uses small position drift only; no yaw spin (Cursor, user: jjmil).
using UnityEngine;

/// <summary>
/// Attached to the spawned player ship in the board scene.
/// Handles a simple hovering animation and provides the ship definition
/// to the tooltip system.
/// </summary>
public class PlayerShipVisual : MonoBehaviour
{
    [SerializeField] private float hoverFrequency = 1.5f;
    [SerializeField] private float hoverAmplitude = 0.5f;
    [SerializeField] private float driftAmplitude = 0.12f;

    public PlayerShipDefinition ShipDef { get; private set; }

    private Vector3 _startLocalPos;
    private float _timeOffset;
    private bool _hoverEnabled = true;

    public void Init(PlayerShipDefinition def)
    {
        ShipDef = def;
        _startLocalPos = transform.localPosition;
        _timeOffset = Random.Range(0f, 10f);

        EnsureCollider();
    }

    /// <summary>
    /// Enables/disables the idle hover. The flight controller suspends hover while
    /// the ship is flying along a path and re-enables it once parked.
    /// </summary>
    public void SetHoverEnabled(bool enabled)
    {
        _hoverEnabled = enabled;
    }

    /// <summary>
    /// Re-anchors the hover to the ship's current local position. Call after the ship
    /// has been moved to a new resting/parked spot so it bobs around that spot.
    /// </summary>
    public void ReanchorHere()
    {
        _startLocalPos = transform.localPosition;
    }

    private void Update()
    {
        if (!_hoverEnabled)
        {
            return;
        }

        float t = (Time.time + _timeOffset) * hoverFrequency;
        float bobY = Mathf.Sin(t) * hoverAmplitude;
        float driftX = Mathf.Sin(t * 0.73f + 1.1f) * driftAmplitude;
        float driftZ = Mathf.Cos(t * 0.61f + 2.4f) * driftAmplitude;
        transform.localPosition = _startLocalPos + new Vector3(driftX, bobY, driftZ);
    }

    private void EnsureCollider()
    {
        Collider col = GetComponentInChildren<Collider>();
        if (col == null)
        {
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var box = rend.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
            }
            else
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(2f, 2f, 2f);
            }
        }
    }
}
