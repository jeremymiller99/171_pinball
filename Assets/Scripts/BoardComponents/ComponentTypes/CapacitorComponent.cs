// Updated by Claude Code (claude-opus-5) for jjmil on 2026-07-28.
// Change: unified ChargeStatus; nearest-component search moved to the registry.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tech bumper: banks Charge from charged balls that strike it. Once it holds
/// enough it spends the lot and activates the nearest components around it.
/// </summary>
public class CapacitorComponent : Bumper
{
    [Header("Capacitor")]
    [SerializeField] private int chargeNeeded = 2;
    [SerializeField] private int componentsToActivate = 4;
    [SerializeField] private float searchRadius = 10f;

    private ChargeStatus _chargeStatus;

    protected override void Awake()
    {
        base.Awake();

        _chargeStatus = ChargeStatusUtility.GetOrAddConsumerStatus(this, chargeNeeded);
        _chargeStatus.FullyCharged += Discharge;
    }

    private void OnDestroy()
    {
        if (_chargeStatus != null)
        {
            _chargeStatus.FullyCharged -= Discharge;
        }
    }

    private void Discharge()
    {
        int consumed = _chargeStatus.TakeAllCharge();
        List<BoardComponent> nearest = BoardComponentRegistry.GetNearest(
            transform.position, componentsToActivate, searchRadius, this);

        ChargeDebug.Log(
            $"{name} spends {consumed} Charge and activates {nearest.Count} components");

        foreach (BoardComponent component in nearest)
        {
            component.ActivateAsBurnTick();
        }
    }
}
