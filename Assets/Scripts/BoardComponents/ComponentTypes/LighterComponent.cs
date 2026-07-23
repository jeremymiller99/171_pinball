using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catalyst bumper: hits Ignite it, and any activation while On Fire (a second
/// hit or its own burn tick) destroys it, Fueling everything nearby twice and
/// Igniting it. The burn tick makes the lighter a short fuse once lit.
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
    }

    new protected void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        if (collision.collider.GetComponent<Ball>() != null)
        {
            HandleActivation();
        }
    }

    public override void ActivateAsIfHit()
    {
        base.ActivateAsIfHit();
        HandleActivation();
    }

    private void HandleActivation()
    {
        if (_exploded || _fireStatus == null)
        {
            return;
        }

        if (_fireStatus.IsOnFire)
        {
            Explode();
        }
        else
        {
            _fireStatus.Ignite();
        }
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

        // Fuel before Igniting so even bare objects catch from the blast.
        foreach (FireStatus status in affected)
        {
            status.Fuel(blastFuelAmount);
            status.Ignite();
        }

        Destroy(gameObject);
    }
}
