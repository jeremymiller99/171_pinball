using UnityEngine;

/// <summary>
/// Tech / Entropy bumper. While On Fire, every activation Projects a Holoball that lasts
/// 3 seconds (see <see cref="Projector"/> / <see cref="Holoball"/>) — the first item to
/// use the Project keyword's timed longevity. "Activation" means both a real ball
/// collision and a burn tick, since burning re-activates the component as if a ball hit it.
///
/// Purely reactive: the Shadow Lamp never ignites itself. A fire source (a burning ball,
/// Molotov/Charcoal, a Lighter blast, fire spreading from a neighbour, ...) has to set it
/// alight first, which is what gives it a ComponentFireStatus to read.
/// </summary>
public class ShadowLampComponent : Bumper
{
    [Header("Shadow Lamp")]
    [Tooltip("Seconds each projected Holoball lasts before it despawns.")]
    [Min(0.01f)]
    [SerializeField] private float holoballSeconds = 3f;

    [Tooltip("How far from the lamp its Holoballs are projected (world units).")]
    [Min(0f)]
    [SerializeField] private float holoballSpawnRadius = 2f;

    private ComponentFireStatus _fireStatus;

    new protected void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        if (collision.collider.GetComponent<Ball>() == null)
        {
            return;
        }

        TryProjectIfOnFire();
    }

    // Burn ticks re-activate the component as if a ball collided with it (see
    // ComponentFireStatus.ActivateTick), so they Project a Holoball too.
    public override void ActivateAsIfHit()
    {
        base.ActivateAsIfHit();
        TryProjectIfOnFire();
    }

    private void TryProjectIfOnFire()
    {
        if (IsOnFire())
        {
            Projector.Project(
                gameObject, HoloballLifetime.Seconds(holoballSeconds), radius: holoballSpawnRadius);
        }
    }

    // The fire status is often added at runtime by whatever ignited this component, so
    // resolve it lazily rather than assuming it exists at Awake.
    private bool IsOnFire()
    {
        if (_fireStatus == null)
        {
            _fireStatus = GetComponent<ComponentFireStatus>();
        }

        return _fireStatus != null && _fireStatus.IsOnFire;
    }
}
