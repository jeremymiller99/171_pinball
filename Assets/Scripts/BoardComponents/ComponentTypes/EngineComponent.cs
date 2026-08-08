// Updated by Claude Code (claude-opus-5) for jjmil on 2026-07-28.
// Change: the bespoke burning-hit score ramp is now the global Fire rule, so this
// collapses to "bank Charge, then light itself".

using UnityEngine;

/// <summary>
/// Entropy/Tech bumper: charged balls that strike it deposit into it. Once it holds
/// enough Charge it spends the lot and lights itself on Fire — from there the Fire
/// keyword supplies the repeated activations and the scoring ramp.
/// </summary>
public class EngineComponent : Bumper
{
    [Header("Engine")]
    [SerializeField] private int chargeNeeded = 1;

    private ChargeStatus _chargeStatus;

    protected override void Awake()
    {
        base.Awake();

        _chargeStatus = ChargeStatusUtility.GetOrAddConsumerStatus(this, chargeNeeded);
        _chargeStatus.FullyCharged += IgniteSelf;

        // Attach the status up front so the Engine can always light itself, and so
        // its burn knobs are visible on the prefab.
        FireStatusUtility.GetOrAddComponentStatus(this);
    }

    private void OnDestroy()
    {
        if (_chargeStatus != null)
        {
            _chargeStatus.FullyCharged -= IgniteSelf;
        }
    }

    private void IgniteSelf()
    {
        if (FireStatus == null)
        {
            return;
        }

        int consumed = _chargeStatus.TakeAllCharge();
        ChargeDebug.Log($"{name} spends {consumed} Charge and lights itself");
        FireStatus.Ignite();
    }
}
