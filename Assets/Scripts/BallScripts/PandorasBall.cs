using UnityEngine;

/// <summary>
/// Standard ball: each component hit has a chance to open the box — an even
/// split between Shocking itself and lighting the struck component on Fire.
/// The fire half fuels the component first so the Ignite takes even on a
/// target with no Flammable stacks.
/// </summary>
public class PandorasBall : Ball
{
    [SerializeField, Range(0f, 1f)] private float chanceToTrigger = 0.2f;

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        BoardComponent[] components = GetBoardComponentsForScoring(collision.collider);
        if (components.Length == 0 || Random.value > chanceToTrigger)
        {
            return;
        }

        if (Random.value < 0.5f)
        {
            ChargeStatusUtility.GetOrAddBallStatus(this)?.Shock();
        }
        else
        {
            LightComponentOnFire(components[0]);
        }
    }

    private void LightComponentOnFire(BoardComponent component)
    {
        if (!FireStatusUtility.CanCatchFire(component))
        {
            return;
        }

        ComponentFireStatus fireStatus = FireStatusUtility.GetOrAddComponentStatus(component);
        if (fireStatus == null)
        {
            return;
        }

        if (!fireStatus.IsFlammable && fireStatus.CanBeFueled)
        {
            fireStatus.Fuel();
        }
        fireStatus.Ignite();
    }
}
