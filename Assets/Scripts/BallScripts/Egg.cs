using UnityEngine;

public class EggBall : Ball
{
    [SerializeField] private float nextBallPointMultiplier;
    [SerializeField] private float nextBallMultMultiplier;
    [SerializeField] private int nextBallCoinMultiplier;
    [SerializeField] private BallSpawner ballSpawner;
    

    new protected void Awake()
    {
        base.Awake();
        ballSpawner = FindFirstObjectByType<BallSpawner>();
    }

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        ballSpawner.DespawnBall(gameObject);
    }

    void OnDestroy()
    {
        if (ballSpawner.HandCount <= 0) return;
        ballSpawner.ActivateNextBall();
        Ball nextBall = ballSpawner.ActiveBalls[0].GetComponent<Ball>();
        nextBall.pointMultiplier = nextBallPointMultiplier;
        nextBall.multMultiplier = nextBallMultMultiplier;
        nextBall.coinMultiplier = nextBallCoinMultiplier;
    }
}
