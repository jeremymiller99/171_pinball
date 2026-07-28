// Updated by Claude Code (claude-opus-5) for jjmil on 2026-07-28.
// Change: unified statuses; the fire half no longer has to Fuel before lighting.

using UnityEngine;

/// <summary>
/// Standard ball: each component hit has a chance to open the box — an even split
/// between giving itself a Charge and lighting the struck component on Fire.
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
            ChargeStatusUtility.GiveCharge(this, 1);
        }
        else
        {
            FireStatusUtility.LightComponent(components[0]);
        }
    }
}
