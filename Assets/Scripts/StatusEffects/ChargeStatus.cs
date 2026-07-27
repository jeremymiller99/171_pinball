using System;
using UnityEngine;

/// <summary>
/// Shared Shock / Charge bookkeeping. Shocking an object gives it one Charge
/// per shock. An object left unshocked for a grace period starts bleeding
/// Charge until it is shocked again or runs dry. Consumers (Engine, Plasma
/// Launcher, Electric Floorboard) decide what being Charged actually does.
/// </summary>
public abstract class ChargeStatus : MonoBehaviour
{
    [SerializeField] private float decayGraceSeconds = 2f;
    [SerializeField] private float decayPerSecond = 2f;
    [SerializeField] private int charge;

    private float _secondsSinceShock;
    private float _decayAccumulator;

    public event Action ChargeChanged;

    public int Charge => charge;
    public bool HasCharge => charge > 0;

    protected virtual void Update()
    {
        if (charge <= 0 || !FireStatusUtility.CanTickNow())
        {
            return;
        }

        _secondsSinceShock += Time.deltaTime;
        if (_secondsSinceShock < decayGraceSeconds)
        {
            return;
        }

        _decayAccumulator += decayPerSecond * Time.deltaTime;
        while (_decayAccumulator >= 1f && charge > 0)
        {
            _decayAccumulator -= 1f;
            charge--;
            ChargeDebug.Log($"{name} charge decays to {charge}");
            ChargeChanged?.Invoke();
        }
    }

    public void Shock(int times = 1)
    {
        if (times <= 0)
        {
            return;
        }

        charge += times;
        ResetDecay();
        ChargeDebug.Log($"{name} shocked x{times}, now {charge} Charge");
        ChargeChanged?.Invoke();
    }

    /// <summary>
    /// Empties this object and returns how much Charge it held. Used both for
    /// ball-to-component transfer on contact and for abilities that consume
    /// all stacks when they trigger.
    /// </summary>
    public int TakeAllCharge()
    {
        int taken = charge;
        if (taken == 0)
        {
            return 0;
        }

        charge = 0;
        ResetDecay();
        ChargeChanged?.Invoke();
        return taken;
    }

    private void ResetDecay()
    {
        _secondsSinceShock = 0f;
        _decayAccumulator = 0f;
    }
}
