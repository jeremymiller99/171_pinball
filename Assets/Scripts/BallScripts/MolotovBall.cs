// Updated by Claude Code (claude-opus-5) for jjmil on 2026-07-28.
// Change: Fuel is gone, so contact now lights both sides on a roll. The queued
// fuel-every-launch passive had no equivalent and is dropped.

using UnityEngine;

/// <summary>
/// Catalyst: contact with a component rolls to set both the component and the
/// bottle itself alight, and each pour has a small chance to break the bottle and
/// retire the ball.
/// </summary>
public sealed class MolotovBall : Ball
{
    public const string DefinitionId = "UnfinishedMolotov";

    [Header("Molotov")]
    [SerializeField, Range(0f, 1f)] private float chanceToLight = 0.6f;
    [Tooltip("Chance the bottle breaks each time it pours.")]
    [SerializeField, Range(0f, 1f)] private float breakChance = 0.05f;

    private bool _broken;

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        BoardComponent[] components = GetBoardComponentsForScoring(collision.collider);
        if (components.Length == 0 || Random.value > chanceToLight)
        {
            return;
        }

        FireStatusUtility.LightComponent(components[0]);
        FireStatusUtility.LightBall(this);
        FireDebug.Log($"{name} pours over {components[0].name}, both alight");

        TryBreak();
    }

    private void TryBreak()
    {
        if (_broken || Random.value >= breakChance)
        {
            return;
        }

        _broken = true;
        FireDebug.Log($"{name} broke and retires");

        DrainHandler drainHandler = ServiceLocator.Get<DrainHandler>();
        if (drainHandler != null)
        {
            drainHandler.OnBallDrained(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
