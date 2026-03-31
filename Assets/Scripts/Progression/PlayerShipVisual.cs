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
    [SerializeField] private float rotationSpeed = 10f;

    public PlayerShipDefinition ShipDef { get; private set; }

    private Vector3 _startLocalPos;
    private float _timeOffset;

    public void Init(PlayerShipDefinition def)
    {
        ShipDef = def;
        _startLocalPos = transform.localPosition;
        _timeOffset = Random.Range(0f, 10f);

        EnsureCollider();
    }

    private void Update()
    {
        float newY = _startLocalPos.y + Mathf.Sin((Time.time + _timeOffset) * hoverFrequency) * hoverAmplitude;
        transform.localPosition = new Vector3(_startLocalPos.x, newY, _startLocalPos.z);
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
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
