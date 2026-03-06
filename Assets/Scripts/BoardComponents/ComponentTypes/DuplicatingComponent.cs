using System.Collections.Generic;
using UnityEngine;

public class DuplicatingComponent : BoardComponent
{
    [Header("Duplication")]
    [SerializeField] private int ballHitsToDuplicate;
    [SerializeField] private List<Ball> duplicatedBalls = new List<Ball>();
    [SerializeField] private List<Ball> ballsToDestroy = new List<Ball>();
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private int componentHitsToDestroy;

    new void Awake()
    {
        base.Awake();
        ballSpawner = FindAnyObjectByType<BallSpawner>();
    }

    new void OnCollisionEnter(Collision collision)
    {
        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball)
        {
            ballHits++;
            if (ballHits % ballHitsToDuplicate == 0)
            {
                Ball newBall = Instantiate(ball);
                duplicatedBalls.Add(newBall);
                newBall.componentHits = 0;
                ballSpawner.ActiveBalls.Add(newBall.gameObject);
            }

        }
        
    }

    void Update()
    {
        foreach (Ball ball in duplicatedBalls)
        {
            if (ball.componentHits >= componentHitsToDestroy)
            {
                ballsToDestroy.Add(ball);
            }
        }

        foreach (Ball ball in ballsToDestroy)
        {
            if (ball.componentHits >= componentHitsToDestroy)
            {
                duplicatedBalls.Remove(ball);
                ballSpawner.DespawnBall(ball.gameObject);
            }
        }

        ballsToDestroy.Clear();
    }

}
