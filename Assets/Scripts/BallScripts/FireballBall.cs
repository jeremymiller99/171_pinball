using System.Collections;
using UnityEngine;

/// <summary>
/// Striker: launches already On Fire (Flammable 5, cannot be Fueled). While
/// burning it re-activates the last component hit every tick; when the burn
/// ends it detonates like a bomb and retires through the drain flow.
/// </summary>
[RequireComponent(typeof(BallFireStatus))]
public sealed class FireballBall : Ball
{
    public const string DefinitionId = "Fireball";

    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float explosionActiveTime = 1f;
    [SerializeField] private float explosionShakeDuration = 0.3f;
    [SerializeField] private float explosionShakeMagnitude = 0.25f;

    private BallFireStatus _fireStatus;
    private bool _ignited;
    private bool _detonating;

    private void Awake()
    {
        _fireStatus = GetComponent<BallFireStatus>();
        _fireStatus.BurnedOut += OnBurnedOut;
        PinballLauncher.BallLaunched += OnBallLaunched;
    }

    private void OnDestroy()
    {
        if (_fireStatus != null)
        {
            _fireStatus.BurnedOut -= OnBurnedOut;
        }
        PinballLauncher.BallLaunched -= OnBallLaunched;
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);

        // Split or duplicated fireballs never pass through the launcher, so
        // fall back to igniting on their first component hit. Resting
        // contact with lane or wall geometry must not start the fuse.
        if (GetBoardComponentsForScoring(collision.collider).Length > 0)
        {
            IgniteOnce();
        }
    }

    private void OnBallLaunched(GameObject launched)
    {
        if (launched == gameObject)
        {
            IgniteOnce();
        }
    }

    private void IgniteOnce()
    {
        if (_ignited)
        {
            return;
        }

        _ignited = true;
        _fireStatus.Ignite();
    }

    private void OnBurnedOut()
    {
        if (_detonating)
        {
            return;
        }

        _detonating = true;
        StartCoroutine(Detonate());
    }

    private IEnumerator Detonate()
    {
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(
                explosionPrefab, transform.position, Quaternion.identity);
            explosion.SetActive(true);

            // The bomb's trigger needs physics steps to collect overlapping
            // components before it can score them.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Bomb bomb = explosion.GetComponent<Bomb>();
            if (bomb != null)
            {
                bomb.Explode();
            }
            Destroy(explosion, explosionActiveTime);
        }

        CameraShake camShake = ServiceLocator.Get<CameraShake>();
        if (camShake != null && camShake.isActiveAndEnabled)
        {
            camShake.Shake(explosionShakeDuration, explosionShakeMagnitude);
        }

        DrainHandler drainHandler = ServiceLocator.Get<DrainHandler>();
        if (drainHandler != null)
        {
            drainHandler.OnBallDrained(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
