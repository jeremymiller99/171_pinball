using UnityEngine;

/// <summary>
/// Tech flipper upgrade: attach next to PinballFlipper. Charged balls that
/// strike the flipper discharge into it; once it has banked enough Charge it
/// consumes all of it and creates a Transistor ball on the board.
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

    private ComponentChargeStatus _chargeStatus;
    private GameRulesManager _gameRulesManager;
    private float _lastCreationTime = float.NegativeInfinity;

    private void Awake()
    {
        _gameRulesManager = ServiceLocator.Get<GameRulesManager>();

        _chargeStatus = GetComponent<ComponentChargeStatus>();
        if (_chargeStatus == null)
        {
            _chargeStatus = gameObject.AddComponent<ComponentChargeStatus>();
        }
        _chargeStatus.ChargeChanged += CreateTransistorIfCharged;

        if (transistorDefinition == null)
        {
            transistorDefinition = Resources.Load<BallDefinition>(transistorDefinitionPath);
        }
    }

    private void OnDestroy()
    {
        if (_chargeStatus != null)
        {
            _chargeStatus.ChargeChanged -= CreateTransistorIfCharged;
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
        if (_chargeStatus.Charge < chargesNeeded
            || Time.time - _lastCreationTime < secondsBetweenCreations)
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
        Vector3 spawnPosition = transform.position + spawnOffset;
        GameObject newBall = Instantiate(
            transistorDefinition.Prefab, spawnPosition, Quaternion.identity);
        _gameRulesManager?.ActiveBalls?.Add(newBall);
        ChargeDebug.Log(
            $"{name} consumes {consumed} Charge and creates a {newBall.name} at {spawnPosition}");
    }
}
