using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mult-focused ball: board awards of type <see cref="TypeOfScore.mult"/> use the board amount
/// multiplied by how many <see cref="DefinitionId"/> entries are in the current
/// <see cref="BallLoadoutController"/> loadout (including the slot for the ball in play until drain),
/// then by <see cref="gearSynergyScale"/> (inspector). Base <see cref="Ball"/> still applies
/// <c>multMultiplier</c> and flat mult add inside <see cref="Ball.AddScore"/>.
/// </summary>
public sealed class GearBall : Ball
{
    public const string DefinitionId = "Gear";

    [Header("Gear mult")]
    [Tooltip("Board mult amount is scaled by (gear count in loadout × this). 1 = design default.")]
    [Min(0f)]
    [SerializeField] private float gearSynergyScale = 1f;

    private void OnValidate()
    {
        gearSynergyScale = Mathf.Max(0f, gearSynergyScale);
    }

    protected override void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (typeOfScore != TypeOfScore.mult)
        {
            base.AddScore(amount, typeOfScore, pos);
            return;
        }

        int gearCount = CountGearsInCurrentLoadout();
        float scaledAmount = amount * gearCount * gearSynergyScale;
        base.AddScore(scaledAmount, typeOfScore, pos);
    }

    private static int CountGearsInCurrentLoadout()
    {
        if (!ServiceLocator.TryGet<BallLoadoutController>(out BallLoadoutController loadout))
        {
            return 1;
        }

        List<BallDefinition> snapshot = loadout.GetBallLoadoutSnapshot();
        int count = 0;
        for (int i = 0; i < snapshot.Count; i++)
        {
            BallDefinition def = snapshot[i];
            if (def != null && def.Id == DefinitionId)
            {
                count++;
            }
        }

        return Mathf.Max(1, count);
    }
}
