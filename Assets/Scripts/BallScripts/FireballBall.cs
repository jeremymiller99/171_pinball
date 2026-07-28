// Updated by Claude Code (claude-opus-5) for jjmil on 2026-07-28.
// Change: inverted. The ball no longer burns itself or detonates on burnout; it
// lights the components it strikes, a limited number of times.

using UnityEngine;

/// <summary>
/// Entropy striker: every component it touches is lit on Fire. It carries a fixed
/// number of lightings; when the last one is spent it rolls once to reignite, which
/// refills the charges. Failing that roll leaves it a plain ball until the ball
/// itself is rebuilt — the count is set at Awake, not per launch.
/// </summary>
public sealed class FireballBall : Ball
{
    public const string DefinitionId = "Fireball";

    [Header("Fireball")]
    [Tooltip("How many components this ball can light before it is spent.")]
    [SerializeField] private int usesPerLight = 5;
    [Tooltip("Chance the ball reignites when its last use is spent.")]
    [SerializeField, Range(0f, 1f)] private float chanceToReignite = 0.1f;

    [SerializeField] private int usesRemaining;

    public int UsesRemaining => usesRemaining;

    private void Awake()
    {
        usesRemaining = usesPerLight;
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        if (usesRemaining <= 0)
        {
            return;
        }

        BoardComponent[] components = GetBoardComponentsForScoring(collision.collider);
        if (components.Length == 0)
        {
            return;
        }

        if (!FireStatusUtility.LightComponent(components[0]))
        {
            return;
        }

        usesRemaining--;
        FireDebug.Log(
            $"{name} lights {components[0].name}, {usesRemaining} lights left");

        if (usesRemaining <= 0)
        {
            RollReignite();
        }
    }

    private void RollReignite()
    {
        if (Random.value < chanceToReignite)
        {
            usesRemaining = usesPerLight;
            FireDebug.Log($"{name} reignites, back to {usesRemaining} lights");
            return;
        }

        FireDebug.Log($"{name} is spent for this launch");
    }
}
