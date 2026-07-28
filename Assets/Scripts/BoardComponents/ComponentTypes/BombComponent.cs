// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class BombComponent : Bumper, IStatusBadgeSource
{
    private const int fuseBadgeSortOrder = 20;

    [Header("Explosion")]
    [SerializeField] private int ballHitsToExplode;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private GameObject explosion;
    [SerializeField] private float explosionActiveTime;

    protected override void Awake()
    {
        base.Awake();
        explosion = Instantiate(explosionPrefab, transform, false);
        explosion.SetActive(false);

        StatusBadgeDisplay.EnsureOn(gameObject);
    }

    /// <summary>
    /// The hit count toward the next explosion, shown as a fuse alongside the
    /// Cannon's. Hidden when no threshold is configured, which also keeps the badge
    /// clear of the modulo-by-zero that <see cref="TryExplode"/> would hit.
    /// </summary>
    public bool TryGetStatusBadge(out StatusBadgeInfo info)
    {
        if (ballHitsToExplode <= 0)
        {
            info = default;
            return false;
        }

        StatusBadgeLibrary library = StatusBadgeLibrary.Instance;
        info = new StatusBadgeInfo(
            library != null ? library.FuseIcon : null,
            $"{ballHits % ballHitsToExplode}/{ballHitsToExplode}",
            library != null ? library.FuseTint : Color.white,
            fuseBadgeSortOrder);
        return true;
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        base.OnCollisionEnter(collision);
        if (collision.collider.GetComponent<Ball>())
        {
            TryExplode();
        }
    }

    public override void ActivateAsIfHit()
    {
        base.ActivateAsIfHit();
        TryExplode();
    }

    private void TryExplode()
    {
        SpawnBoardHitCountPopup(ballHits, ballHitsToExplode);
        if (ballHits % ballHitsToExplode == 0)
        {
            ballHits = 0;
            explosion.SetActive(true);
            explosion.GetComponent<Bomb>().Explode();
            StartCoroutine("DespawnExplosion");
        }
    }

    private IEnumerator DespawnExplosion()
    {
        yield return new WaitForSeconds(explosionActiveTime);
        explosion.SetActive(false);
    }
}