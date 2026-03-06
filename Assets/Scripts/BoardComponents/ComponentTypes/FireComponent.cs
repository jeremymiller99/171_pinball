using System.Collections;
using UnityEngine;

public class FireComponent : BoardComponent
{
    [Header("Fire")]
    [SerializeField] private int ballHitsToFire;
    [SerializeField] private float timeForHits;
    [SerializeField] private float timeOnFire;
    [SerializeField] private float fireMultiplier;
    [SerializeField] private bool onFire = false;
    [SerializeField] private int preFireBallHitsInLastSeconds;
    [SerializeField] private int onFireBallHitsInLastSeconds;
    [SerializeField] private ParticleSystem particles;

    new protected void Awake()
    {
        base.Awake();
        particles = GetComponent<ParticleSystem>();
        particles.Pause();
    }

    new protected void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<Ball>())
        {
            ballHits++;
            if (!onFire)
            {
                preFireBallHitsInLastSeconds++;
                if (preFireBallHitsInLastSeconds >= ballHitsToFire)
                {
                    particles.Play();
                    onFire = true;
                    amountToScore *= fireMultiplier;
                    onFireBallHitsInLastSeconds++;
                    StartCoroutine("RemoveFireBallHit");
                }

                StartCoroutine("RemoveCheckBallHit");
            } else {
                onFireBallHitsInLastSeconds++;
                StartCoroutine("RemoveFireBallHit");
            }
        }

    }

    void Update()
    {
        if (!onFire || onFireBallHitsInLastSeconds > 0) return;
        onFire = false;
        particles.Pause();
        particles.Clear();
        amountToScore /= fireMultiplier;
    }

    private IEnumerator RemoveCheckBallHit()
    {
        yield return new WaitForSeconds(timeForHits);
        preFireBallHitsInLastSeconds--;
    }

    private IEnumerator RemoveFireBallHit()
    {
        yield return new WaitForSeconds(timeOnFire);
        onFireBallHitsInLastSeconds--;
    }

}
