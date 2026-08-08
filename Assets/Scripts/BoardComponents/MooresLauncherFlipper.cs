// Updated by Claude Code (claude-opus-5) for jjmil on 2026-07-28.
// Change: unified ChargeStatus and its FullyCharged event.

using UnityEngine;

/// <summary>
/// Tech flipper upgrade: attach next to PinballFlipper. Charged balls that
/// strike the flipper deposit into it; once it has banked enough Charge it
/// spends all of it and creates a Transistor ball on the board.
/// </summary>
[RequireComponent(typeof(PinballFlipper))]
public sealed class MooresLauncherFlipper : MonoBehaviour
{
    private const string transistorDefinitionPath = "BallDefinitions/Transistor";

    [Header("Moore's Launcher")]
    [SerializeField] private int chargesNeeded = 5;
    [Tooltip("Ball created when fully Charged; loaded from Resources if empty.")]
    [SerializeField] private BallDefinition transistorDefinition;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0f, 0.5f);
    [Tooltip("Floor between creations so a board full of Transistors cannot avalanche.")]
    [SerializeField] private float secondsBetweenCreations = 2f;

    private ChargeStatus _chargeStatus;
    private GameRulesManager _gameRulesManager;
    private float _lastCreationTime = float.NegativeInfinity;

    private void Awake()
    {
        _gameRulesManager = ServiceLocator.Get<GameRulesManager>();

        _chargeStatus = ChargeStatusUtility.GetOrAddConsumerStatus(
            gameObject, chargesNeeded);
        _chargeStatus.FullyCharged += CreateTransistorIfCharged;

        if (transistorDefinition == null)
        {
            transistorDefinition = Resources.Load<BallDefinition>(transistorDefinitionPath);
        }
    }

    private void OnDestroy()
    {
        if (_chargeStatus != null)
        {
            _chargeStatus.FullyCharged -= CreateTransistorIfCharged;
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

    private void CreateTransistorIfCharged()
    {
        if (Time.time - _lastCreationTime < secondsBetweenCreations)
        {
            return;
        }

        if (transistorDefinition == null || transistorDefinition.Prefab == null)
        {
            ChargeDebug.Log($"{name} is Charged but has no Transistor definition to create");
            return;
        }

        _lastCreationTime = Time.time;
        int consumed = _chargeStatus.TakeAllCharge();
        _gameRulesManager ??= ServiceLocator.Get<GameRulesManager>();
        Vector3 spawnPosition = transform.position + spawnOffset;
        GameObject newBall = Instantiate(
            transistorDefinition.Prefab, spawnPosition, Quaternion.identity);
        _gameRulesManager?.ActiveBalls?.Add(newBall);
        ChargeDebug.Log(
            $"{name} consumes {consumed} Charge and creates a {newBall.name} at {spawnPosition}");
    }
}
