using UnityEngine;

/// <summary>
/// Fire status for balls. Owns all contact spread (every fire contact
/// involves a ball), optional fuel-on-contact (Charcoal), the half second
/// re-activation of the last component hit, and write-back of stacks to the
/// loadout slot so Fuel persists between launches.
/// </summary>
[RequireComponent(typeof(Ball))]
public sealed class BallFireStatus : FireStatus
{
    [SerializeField] private bool fuelOtherOnContact = false;

    private Ball _ball;

    protected override void Awake()
    {
        base.Awake();
        _ball = GetComponent<Ball>();
        StacksChanged += WriteStacksBackToLoadout;
    }

    void OnCollisionEnter(Collision collision)
    {
        HandleContact(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    protected override void ActivateTick()
    {
        GameObject lastHit = _ball != null ? _ball.LastObjectHit : null;
        if (lastHit == null)
        {
            return;
        }

        BoardComponent[] components = lastHit.GetComponents<BoardComponent>();
        if (components.Length == 0)
        {
            components = lastHit.GetComponentsInParent<BoardComponent>();
        }

        foreach (BoardComponent component in components)
        {
            component.ActivateAsIfHit();
        }
    }

    private void HandleContact(Collider other)
    {
        BoardComponent[] components = Ball.GetBoardComponentsForScoring(other);
        BallFireStatus otherBall = other.GetComponentInParent<BallFireStatus>();

        // Fuel first so hitting an already-burning object extends its burn.
        if (fuelOtherOnContact)
        {
            foreach (BoardComponent component in components)
            {
                FireStatusUtility.GetOrAddComponentStatus(component)?.Fuel();
            }
            if (otherBall != null)
            {
                otherBall.Fuel();
            }
        }

        if (IsOnFire)
        {
            foreach (BoardComponent component in components)
            {
                ComponentFireStatus status =
                    component.GetComponent<ComponentFireStatus>();
                if (status != null && status.IsFlammable)
                {
                    status.Ignite();
                }
            }
            if (otherBall != null && otherBall.IsFlammable)
            {
                otherBall.Ignite();
            }
        }
        else if (IsFlammable && TouchedFire(components, otherBall))
        {
            Ignite();
        }
    }

    private static bool TouchedFire(
        BoardComponent[] components, BallFireStatus otherBall)
    {
        if (otherBall != null && otherBall.IsOnFire)
        {
            return true;
        }

        foreach (BoardComponent component in components)
        {
            ComponentFireStatus status =
                component.GetComponent<ComponentFireStatus>();
            if (status != null && status.IsOnFire)
            {
                return true;
            }
        }
        return false;
    }

    private void WriteStacksBackToLoadout()
    {
        BallHandSlotMarker marker = GetComponent<BallHandSlotMarker>();
        if (marker == null || marker.SlotIndex < 0)
        {
            return;
        }

        BallLoadoutController loadout = ServiceLocator.Get<BallLoadoutController>();
        if (loadout != null)
        {
            loadout.SetExtraFlammableStacksForSlot(
                marker.SlotIndex, Stacks - BaseFlammableStacks);
        }
    }
}
