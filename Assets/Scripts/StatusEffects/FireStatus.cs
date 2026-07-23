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
    [Tooltip("Overrides the flame prefab from FireVfxLibrary for this object.")]
    [SerializeField] private GameObject fireVfxPrefab;
    [Tooltip("Overrides the smoke prefab from FireVfxLibrary for this object.")]
    [SerializeField] private GameObject fueledVfxPrefab;
    [SerializeField] private int stacks;
    [SerializeField] private bool isOnFire;
    [SerializeField] private float burnSecondsRemaining;

    private float _tickAccumulator;
    private GameObject _fireVfxInstance;
    private GameObject _fueledVfxInstance;

    public event Action Ignited;
    public event Action BurnedOut;
    public event Action StacksChanged;

    public int BaseFlammableStacks => baseFlammableStacks;
    public int Stacks => stacks;
    public bool IsFlammable => stacks > 0;
    public bool IsOnFire => isOnFire;
    public bool CanBeFueled => canBeFueled;

    /// <summary>
    /// True while the object carries Fuel beyond its innate Flammable rating,
    /// which is what the smoke VFX advertises.
    /// </summary>
    public bool IsFueled => stacks > baseFlammableStacks;

    protected virtual void Awake()
    {
        stacks = baseFlammableStacks;
        StacksChanged += RefreshFueledVfx;
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
        StacksChanged -= RefreshFueledVfx;
        DestroyFueledVfx();
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
        FireDebug.Log($"{name} ignited, burning for {stacks}s");
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
        FireDebug.Log(isOnFire
            ? $"{name} fueled +{amount}, burn extended to {burnSecondsRemaining:0.#}s"
            : $"{name} fueled +{amount}, now Flammable {stacks}");
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
        FireDebug.Log($"{name} burned out");
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
        if (_fireVfxInstance == null)
        {
            _fireVfxInstance = fireVfxPrefab != null
                ? Instantiate(fireVfxPrefab, transform)
                : FireVfxLibrary.Instance?.InstantiateOnFireVfx(transform);
        }
        RefreshFueledVfx();
        ServiceLocator.Get<AudioManager>()?.StartBurningSound();
    }

    private void StopFireFeedback()
    {
        if (_fireVfxInstance != null)
        {
            Destroy(_fireVfxInstance);
            _fireVfxInstance = null;
        }
        RefreshFueledVfx();
        ServiceLocator.Get<AudioManager>()?.StopBurningSound();
    }

    /// <summary>
    /// Smoke shows only while Fueled and unlit: once the object catches, the
    /// fire VFX takes over.
    /// </summary>
    private void RefreshFueledVfx()
    {
        if (!IsFueled || isOnFire)
        {
            DestroyFueledVfx();
            return;
        }

        if (_fueledVfxInstance != null)
        {
            return;
        }

        _fueledVfxInstance = fueledVfxPrefab != null
            ? Instantiate(fueledVfxPrefab, transform)
            : FireVfxLibrary.Instance?.InstantiateFueledVfx(transform);
    }

    private void DestroyFueledVfx()
    {
        if (_fueledVfxInstance != null)
        {
            Destroy(_fueledVfxInstance);
            _fueledVfxInstance = null;
        }
    }
}
