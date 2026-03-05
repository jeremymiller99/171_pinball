using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class BombComponent : BoardComponent
{
    [Header("Explosion")]
    [SerializeField] private int ballHitsToExplode;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private GameObject explosion;
    [SerializeField] private float explosionActiveTime;

    new void Awake()
    {
        base.Awake();
        explosion = Instantiate(explosionPrefab, transform, false);
        explosion.SetActive(false);
    }

    new void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<Ball>())
        {
            ballHits++;
            if (ballHits %  ballHitsToExplode == 0)
            {
                explosion.SetActive(true);
                explosion.GetComponent<Bomb>().Explode();
                StartCoroutine("DespawnExplosion");
            }

        }
        
    }

    private IEnumerator DespawnExplosion()
    {
        yield return new WaitForSeconds(explosionActiveTime);
        explosion.SetActive(false);
    }
}