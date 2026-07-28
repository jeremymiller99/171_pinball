using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tech bumper: banks Charge from charged balls that strike it. Once fully
/// Charged it consumes every stack and activates the nearest components
/// around it through the weak-shake scoring path. Portals are skipped so a
/// discharge cannot fire teleports.
/// </summary>
public class CapacitorComponent : Bumper
{
    [Header("Capacitor")]
    [SerializeField] private int chargeNeeded = 2;
    [SerializeField] private int componentsToActivate = 4;
    [SerializeField] private float searchRadius = 10f;

    private ComponentChargeStatus _chargeStatus;

    protected override void Awake()
    {
        base.Awake();
        _chargeStatus = ChargeStatusUtility.GetOrAddComponentStatus(this);
        _chargeStatus.ChargeChanged += DischargeIfCharged;
    }

    private void OnDestroy()
    {
        if (_chargeStatus != null)
        {
            _chargeStatus.ChargeChanged -= DischargeIfCharged;
        }
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball != null)
        {
            ChargeStatusUtility.DrainBallInto(ball, _chargeStatus);
        }
    }

    private void DischargeIfCharged()
    {
        if (_chargeStatus.Charge < chargeNeeded)
        {
            return;
        }

        int consumed = _chargeStatus.TakeAllCharge();
        List<BoardComponent> nearest = FindNearestComponents();
        ChargeDebug.Log(
            $"{name} consumes {consumed} Charge and activates {nearest.Count} components");

        foreach (BoardComponent component in nearest)
        {
            ChargeDebug.Log($"{name} activates {component.name}");
            component.ActivateAsBurnTick();
        }
    }

    private List<BoardComponent> FindNearestComponents()
    {
        Dictionary<int, BoardComponent> candidates = new Dictionary<int, BoardComponent>();

        foreach (Collider overlap in Physics.OverlapSphere(transform.position, searchRadius))
        {
            foreach (BoardComponent component in Ball.GetBoardComponentsForScoring(overlap))
            {
                if (component == null || component == this
                    || component.componentType == BoardComponentType.Portal)
                {
                    continue;
                }

                candidates[component.GetInstanceID()] = component;
            }
        }

        List<BoardComponent> nearest = new List<BoardComponent>(candidates.Values);
        nearest.Sort((a, b) =>
            Vector3.SqrMagnitude(a.transform.position - transform.position).CompareTo(
                Vector3.SqrMagnitude(b.transform.position - transform.position)));

        if (nearest.Count > componentsToActivate)
        {
            nearest.RemoveRange(componentsToActivate, nearest.Count - componentsToActivate);
        }

        return nearest;
    }
}
