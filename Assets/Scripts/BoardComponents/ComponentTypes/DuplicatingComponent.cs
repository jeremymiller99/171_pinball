// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using System.Collections.Generic;
using UnityEngine;

public class DuplicatingComponent : BoardComponent
{
    [Header("Duplication")]
    [SerializeField] private int ballHitsToDuplicate;
    [SerializeField] private List<Ball> duplicatedBalls = new List<Ball>();
    [SerializeField] private List<Ball> ballsToDestroy = new List<Ball>();
    [SerializeField] private int componentHitsToDestroy;

    private DrainHandler _drainHandler;
    private GameRulesManager _rulesManager;

    new void Awake()
    {
        base.Awake();
        _drainHandler = ServiceLocator.Get<DrainHandler>();
        _rulesManager = ServiceLocator.Get<GameRulesManager>();
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
                newBall.ResetComponentHits();
                _rulesManager?.ActiveBalls?.Add(newBall.gameObject);
            }
        }
    }

    void Update()
    {
        if (_drainHandler == null)
            _drainHandler = ServiceLocator.Get<DrainHandler>();

        foreach (Ball ball in duplicatedBalls)
        {
            if (ball.ComponentHits >= componentHitsToDestroy)
            {
                ballsToDestroy.Add(ball);
            }
        }

        foreach (Ball ball in ballsToDestroy)
        {
            if (ball.ComponentHits >= componentHitsToDestroy)
            {
                duplicatedBalls.Remove(ball);
                _drainHandler?.OnBallDrained(ball.gameObject);
            }
        }

        ballsToDestroy.Clear();
    }

}
