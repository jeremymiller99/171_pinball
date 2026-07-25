using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catalyst bumper: a hit Ignites it, and a second ball hit while it burns
/// destroys it, Fueling everything nearby twice and Igniting it. If it burns
/// out untouched it refills its innate fuel and can be lit again.
/// </summary>
public class LighterComponent : Bumper
{
    [Header("Lighter")]
    [SerializeField] private float blastRadius = 6f;
    [SerializeField] private int blastFuelAmount = 2;

    private ComponentFireStatus _fireStatus;
    private bool _exploded;

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
            _fireStatus.BurnedOut += RefillFuel;
        }
    }

    private void OnDestroy()
    {
        if (_fireStatus != null)
        {
            _fireStatus.BurnedOut -= RefillFuel;
        }
    }

    new protected void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        if (collision.collider.GetComponent<Ball>() != null)
        {
            HandleBallHit();
        }
    }

    private void HandleBallHit()
    {
        if (_exploded || _fireStatus == null)
        {
            return;
        }

        if (_fireStatus.IsOnFire)
        {
            FireDebug.Log($"{name} hit while burning, exploding");
            Explode();
        }
        else if (_fireStatus.IsFlammable)
        {
            FireDebug.Log($"{name} lit by hit, explodes if hit again while burning");
            _fireStatus.Ignite();
        }
    }

    // An untouched burn empties the stacks; top the reservoir back up so
    // the lighter can be lit again instead of going dead.
    private void RefillFuel()
    {
        if (_exploded || _fireStatus == null)
        {
            return;
        }

        FireDebug.Log($"{name} burn ended untouched, refilling its fuel");
        _fireStatus.SetStacks(_fireStatus.BaseFlammableStacks);
    }

    private void Explode()
    {
        _exploded = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius);
        var affected = new HashSet<FireStatus>();

        foreach (Collider hit in hits)
        {
            foreach (BoardComponent component in Ball.GetBoardComponentsForScoring(hit))
            {
                if (component == this)
                {
                    continue;
                }

                ComponentFireStatus status =
                    FireStatusUtility.GetOrAddComponentStatus(component);
                if (status != null)
                {
                    affected.Add(status);
                }
            }

            Ball ball = hit.GetComponentInParent<Ball>();
            if (ball != null)
            {
                BallFireStatus status = FireStatusUtility.GetOrAddBallStatus(ball);
                if (status != null)
                {
                    affected.Add(status);
                }
            }
        }

        FireDebug.Log(
            $"{name} explodes, fueling and igniting {affected.Count} nearby objects");

        // Fuel before Igniting so even bare objects catch from the blast.
        foreach (FireStatus status in affected)
        {
            status.Fuel(blastFuelAmount);
            status.Ignite();
        }

        Destroy(gameObject);
    }
}
