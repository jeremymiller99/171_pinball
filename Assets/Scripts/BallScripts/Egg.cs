using UnityEngine;

public class EggBall : Ball
{
    [SerializeField] private float nextBallPointMultiplier;
    [SerializeField] private float nextBallMultMultiplier;
    [SerializeField] private int nextBallCoinMultiplier;
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private bool applyOnUseOnly = true;

    private bool wasUsed;
    

    new protected void Awake()
    {
        base.Awake();
        ballSpawner = FindFirstObjectByType<BallSpawner>();
    }

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        wasUsed = true;
        ballSpawner.DespawnBall(gameObject);
    }

    void OnDestroy()
    {
        if (applyOnUseOnly && !wasUsed) return;
        if (ballSpawner.HandCount <= 0) return;

        GameObject nextBallObject = ballSpawner.ActivateNextBall();
        if (nextBallObject == null) return;

        Ball nextBall = nextBallObject.GetComponent<Ball>();
        if (nextBall == null) return;

        float pointFactor = nextBallPointMultiplier <= 0f ? 1f : nextBallPointMultiplier;
        float multFactor = nextBallMultMultiplier <= 0f ? 1f : nextBallMultMultiplier;
        int coinFactor = nextBallCoinMultiplier <= 0 ? 1 : nextBallCoinMultiplier;

        nextBall.pointMultiplier *= pointFactor;
        nextBall.multMultiplier *= multFactor;
        nextBall.coinMultiplier *= coinFactor;
    }
}
