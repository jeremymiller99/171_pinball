using UnityEngine;

/// <summary>
/// Tech flipper upgrade: attach next to PinballFlipper. Charged balls that
/// strike the flipper discharge into it; once it has banked enough Charge it
/// consumes all of it and fires a plasma ball up the board that activates the
/// components it passes through (see PlasmaBall).
/// </summary>
[RequireComponent(typeof(PinballFlipper))]
public sealed class PlasmaLauncherFlipper : MonoBehaviour
{
    [Header("Plasma Launcher")]
    [SerializeField] private int chargesNeeded = 5;
    [Tooltip("Optional prefab with a PlasmaBall; a glowing sphere is built if empty.")]
    [SerializeField] private GameObject plasmaBallPrefab;
    [SerializeField] private Vector3 launchDirection = Vector3.forward;
    [SerializeField] private float launchSpeed = 12f;
    [SerializeField] private float spawnClearance = 0.5f;
    [SerializeField] private float plasmaBallScale = 0.5f;
    [SerializeField] private Color plasmaColor = new Color(0.3f, 0.9f, 1f);

    private ComponentChargeStatus _chargeStatus;

    private void Awake()
    {
        _chargeStatus = GetComponent<ComponentChargeStatus>();
        if (_chargeStatus == null)
        {
            _chargeStatus = gameObject.AddComponent<ComponentChargeStatus>();
        }
        _chargeStatus.ChargeChanged += FireIfCharged;
    }

    private void OnDestroy()
    {
        if (_chargeStatus != null)
        {
            _chargeStatus.ChargeChanged -= FireIfCharged;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball == null)
        {
            return;
        }

        int moved = ChargeStatusUtility.DrainBallInto(ball, _chargeStatus);
        if (moved > 0)
        {
            ChargeDebug.Log(
                $"{name} banks {moved} Charge ({_chargeStatus.Charge}/{chargesNeeded})");
        }
    }

    private void FireIfCharged()
    {
        if (_chargeStatus.Charge < chargesNeeded)
        {
            return;
        }

        int consumed = _chargeStatus.TakeAllCharge();
        ChargeDebug.Log($"{name} consumes {consumed} Charge and fires a plasma ball");
        SpawnPlasmaBall();
    }

    private void SpawnPlasmaBall()
    {
        Vector3 direction = launchDirection.normalized;
        Vector3 spawnPosition = transform.position + direction * spawnClearance;

        GameObject projectile = plasmaBallPrefab != null
            ? Instantiate(plasmaBallPrefab, spawnPosition, Quaternion.identity)
            : BuildDefaultProjectile(spawnPosition);

        PlasmaBall plasma = projectile.GetComponent<PlasmaBall>();
        if (plasma == null)
        {
            plasma = projectile.AddComponent<PlasmaBall>();
        }
        plasma.Launch(direction * launchSpeed);
    }

    private GameObject BuildDefaultProjectile(Vector3 spawnPosition)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = $"{name} PlasmaBall";
        projectile.transform.position = spawnPosition;
        projectile.transform.localScale = Vector3.one * plasmaBallScale;
        projectile.GetComponent<Collider>().isTrigger = true;
        projectile.GetComponent<Renderer>().material.color = plasmaColor;

        Rigidbody rb = projectile.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        return projectile;
    }
}
