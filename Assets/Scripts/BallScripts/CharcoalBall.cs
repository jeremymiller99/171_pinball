// Updated by Claude Code (claude-opus-5) for jjmil on 2026-07-28.
// Change: Fuel is gone, so this now lights what it touches on a roll. The queued
// fuel-every-launch passive had no equivalent and is dropped.

using UnityEngine;

/// <summary>
/// Catalyst: a lump of fuel that sets alight what it touches. Each component
/// contact rolls to light that component on Fire. The chance sits above Flint's so
/// Charcoal reads as the dedicated fire-spreader, without being the automatic
/// contact spread the old system had.
/// </summary>
public sealed class CharcoalBall : Ball
{
    public const string DefinitionId = "Charcoal";

    [Header("Charcoal")]
    [SerializeField, Range(0f, 1f)] private float chanceToLight = 0.5f;

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        BoardComponent[] components = GetBoardComponentsForScoring(collision.collider);
        if (components.Length == 0 || Random.value > chanceToLight)
        {
            return;
        }

        if (FireStatusUtility.LightComponent(components[0]))
        {
            FireDebug.Log($"{name} lights {components[0].name}");
        }
    }
}
