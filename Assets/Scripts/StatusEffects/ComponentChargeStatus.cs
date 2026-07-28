using UnityEngine;

/// <summary>
/// Charge status for board components. Added at runtime the first time a
/// component receives Charge. Components never touch each other, so all
/// transfer happens through a ball draining into the component on contact.
/// </summary>
public sealed class ComponentChargeStatus : ChargeStatus
{
}
