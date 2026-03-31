using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a playable ship that a player can select before starting a challenge run.
/// Provides a starting hand and run-long passive modifiers.
/// </summary>
[CreateAssetMenu(menuName = "Pinball/Player Ship Definition", fileName = "New Player Ship")]
public class PlayerShipDefinition : ScriptableObject
{
    [Header("Display")]
    public string displayName = "New Ship";
    
    [TextArea(2, 4)]
    public string description = "A reliable generic spacecraft.";

    [Tooltip("Optional 3D model prefab to show in the UI or board.")]
    public GameObject shipModelPrefab;

    [Header("Starting Loadout")]
    [Tooltip("Sets the player's max inventory size (Max Balls). Default is usually 5.")]
    [Min(1)]
    public int startingMaxBalls = 5;

    [Tooltip("The initial balls the player will start with. The rest of the hand will be filled with basic balls up to startingMaxBalls.")]
    public List<BallDefinition> startingHand = new List<BallDefinition>();

    [Header("Run Modifiers")]
    [Tooltip("Multiplier applied to all points earned. 1.0 = normal, 2.0 = double.")]
    [Min(0f)]
    public float scoreMultiplier = 1f;

    [Tooltip("Multiplier applied to coins earned. 1.0 = normal, 0.5 = half.")]
    [Min(0f)]
    public float coinMultiplier = 1f;

    [Tooltip("Multiplier applied to mult earned. 1.0 = normal, 0.5 = half.")]
    [Min(0f)]
    public float multMultiplier = 1f;

    [Tooltip("Starting coins for the run.")]
    public int startingCoins = 0;
}
