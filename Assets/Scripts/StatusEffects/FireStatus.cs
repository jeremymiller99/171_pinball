using System;
using UnityEngine;

/// <summary>
/// Shared Flammable / Ignite / On Fire / Fuel bookkeeping. Flammable X = X stacks =
/// X seconds of burn once Ignited; Fuel adds one stack (and one second mid-burn).
/// Burning consumes roughly one stack per second and the object activates as if
/// collided with every half second.
/// </summary>
public abstract class FireStatus : MonoBehaviour
{
    private const float tickIntervalSeconds = 0.5f;

    [SerializeField] private int baseFlammableStacks = 0;
    [SerializeField] private bool canBeFueled = true;
    [SerializeField] private GameObject fireVfxPrefab;
    [SerializeField] private int stacks;
    [SerializeField] private bool isOnFire;
    [SerializeField] private float burnSecondsRemaining;

    private float _tickAccumulator;
    private GameObject _fireVfxInstance;

    public event Action Ignited;
    public event Action BurnedOut;
    public event Action StacksChanged;

    public int BaseFlammableStacks => baseFlammableStacks;
    public int Stacks => stacks;
    public bool IsFlammable => stacks > 0;
    public bool IsOnFire => isOnFire;
    public bool CanBeFueled => canBeFueled;

    protected virtual void Awake()
    {
        stacks = baseFlammableStacks;
    }

    protected virtual void Update()
    {
        if (!isOnFire || !FireStatusUtility.CanTickNow())
        {
            return;
        }

        burnSecondsRemaining -= Time.deltaTime;

        int stacksLeft = Mathf.Max(0, Mathf.CeilToInt(burnSecondsRemaining));
        if (stacksLeft != stacks)
        {
            stacks = stacksLeft;
            StacksChanged?.Invoke();
        }

        if (burnSecondsRemaining <= 0f)
        {
            EndBurn();
            return;
        }

        _tickAccumulator += Time.deltaTime;
        if (_tickAccumulator >= tickIntervalSeconds)
        {
            _tickAccumulator -= tickIntervalSeconds;
            ActivateTick();
        }
    }

    protected virtual void OnDestroy()
    {
        if (isOnFire)
        {
            ServiceLocator.Get<AudioManager>()?.StopBurningSound();
        }
    }

    public void Ignite()
    {
        if (isOnFire || !IsFlammable)
        {
            return;
        }

        isOnFire = true;
        burnSecondsRemaining = stacks;
        _tickAccumulator = 0f;
        StartFireFeedback();
        Ignited?.Invoke();
    }

    public void Fuel(int amount = 1)
    {
        if (!canBeFueled || amount <= 0)
        {
            return;
        }

        stacks += amount;
        if (isOnFire)
        {
            burnSecondsRemaining += amount;
        }
        StacksChanged?.Invoke();
    }

    /// <summary>
    /// Direct stack write used by the loadout sync when the hand is rebuilt.
    /// </summary>
    public void SetStacks(int value)
    {
        int clamped = Mathf.Max(0, value);
        if (clamped == stacks)
        {
            return;
        }

        stacks = clamped;
        if (isOnFire)
        {
            burnSecondsRemaining = stacks;
        }
        StacksChanged?.Invoke();
    }

    protected abstract void ActivateTick();

    private void EndBurn()
    {
        isOnFire = false;
        burnSecondsRemaining = 0f;
        _tickAccumulator = 0f;
        if (stacks != 0)
        {
            stacks = 0;
            StacksChanged?.Invoke();
        }
        StopFireFeedback();
        BurnedOut?.Invoke();
    }

    private void StartFireFeedback()
    {
        if (fireVfxPrefab != null && _fireVfxInstance == null)
        {
            _fireVfxInstance = Instantiate(fireVfxPrefab, transform);
        }
        ServiceLocator.Get<AudioManager>()?.StartBurningSound();
    }

    private void StopFireFeedback()
    {
        if (_fireVfxInstance != null)
        {
            Destroy(_fireVfxInstance);
            _fireVfxInstance = null;
        }
        ServiceLocator.Get<AudioManager>()?.StopBurningSound();
    }
}
