// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using System.Collections.Generic;
using UnityEngine;

public class DuplicatingComponent : BoardComponent
{
    [Header("Duplication")]
    [SerializeField] private int ballHitsToDuplicate;
    [SerializeField] private List<Ball> duplicatedBalls = new List<Ball>();
    [SerializeField] private List<Ball> ballsToDestroy = new List<Ball>();
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private int componentHitsToDestroy;

    new void Awake()
    {
        base.Awake();
        gameRulesManager = FindAnyObjectByType<GameRulesManager>();
    }

    new void OnCollisionEnter(Collision collision)
    {
        Ball ball = collision.collider.GetComponent<Ball>();
        if (ball)
        {
            ballHits++;
            SpawnBoardHitCountPopup(ballHits, ballHitsToDuplicate);
            if (ballHits % ballHitsToDuplicate == 0)
            {
                ballHits = 0;
                Ball newBall = Instantiate(ball);
                Ball.EnsureOwnMaterials(newBall.gameObject);
                duplicatedBalls.Add(newBall);
                newBall.componentHits = 0;
                gameRulesManager.ActiveBalls.Add(newBall.gameObject);
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
                gameRulesManager.OnBallDrained(ball.gameObject);
            }
        }

        ballsToDestroy.Clear();
    }

}
