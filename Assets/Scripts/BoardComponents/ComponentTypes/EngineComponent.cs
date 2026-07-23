using UnityEngine;

/// <summary>
/// Striker bumper: while Charged, any Flammable stacks it collects are
/// instantly converted into score instead of burning. There is no Shock
/// system yet, so charge is seeded in the inspector and AddCharge is the
/// hook for it later.
/// </summary>
public class EngineComponent : Bumper
{
    [Header("Engine")]
    [SerializeField] private int chargeNeeded = 1;
    [Tooltip("Seed charge until Shock can grant it during play.")]
    [SerializeField] private int charge = 1;
    [SerializeField] private float scorePerFlammableStack = 5f;

    private ComponentFireStatus _fireStatus;
    private bool _converting;

    public bool IsCharged => charge >= chargeNeeded;

    new protected void Awake()
    {
        base.Awake();

        _fireStatus = GetComponent<ComponentFireStatus>();
        if (_fireStatus == null)
        {
            _fireStatus = FireStatusUtility.GetOrAddComponentStatus(this);
        }

        if (_fireStatus != null)
        {
            _fireStatus.StacksChanged += ConvertStacksToScore;
        }
    }

    private void Start()
    {
        ConvertStacksToScore();
    }

    private void OnDestroy()
    {
        if (_fireStatus != null)
        {
            _fireStatus.StacksChanged -= ConvertStacksToScore;
        }
    }

    public void AddCharge(int amount)
    {
        charge += amount;
        ConvertStacksToScore();
    }

    private void ConvertStacksToScore()
    {
        if (_converting || !IsCharged || _fireStatus == null)
        {
            return;
        }

        int stacks = _fireStatus.Stacks;
        if (stacks <= 0)
        {
            return;
        }

        // SetStacks re-raises StacksChanged; the guard stops the loop.
        _converting = true;
        _fireStatus.SetStacks(0);
        _converting = false;

        FireDebug.Log(
            $"{name} converts {stacks} stacks into {stacks * scorePerFlammableStack} score");

        if (scoreManager == null)
        {
            scoreManager = ServiceLocator.Get<ScoreManager>();
        }
        scoreManager?.AddScore(
            stacks * scorePerFlammableStack, typeOfScore, transform);
    }
}
