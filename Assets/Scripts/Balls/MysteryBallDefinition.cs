using UnityEngine;

/// <summary>
/// A shop-only ball definition that, when purchased, is replaced at runtime by
/// a random unlocked ball of <see cref="TargetRarity"/>. The icon and prefab
/// inherited from <see cref="BallDefinition"/> are the question-mark visual
/// shown on the shop shelf -- the player never holds a mystery ball in their
/// loadout.
/// </summary>
[CreateAssetMenu(menuName = "Pinball/Mystery Ball Definition", fileName = "MysteryBallDefinition_")]
public sealed class MysteryBallDefinition : BallDefinition
{
    [Header("Mystery")]
    [Tooltip("Rarity tier of the ball that will be granted on purchase.")]
    [SerializeField] private BallRarity targetRarity = BallRarity.Common;

    public BallRarity TargetRarity => targetRarity;
}
