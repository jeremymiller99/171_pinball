using UnityEngine;

public class ExtraSplitArtifact : MonoBehaviour
{

    [SerializeField] private BallSpawner ballSpawner;

    private void Awake()
    {
        ballSpawner = ServiceLocator.Get<BallSpawner>();
        ballSpawner.ActivateBall += AddSplitBall;
    }

    private void AddSplitBall(GameObject ball)
    {
        MultiBall multi = ball.GetComponent<MultiBall>();
        if (multi)
        {
            multi.OnSplit += ReactivateSplitter;
        }
    }

    private void ReactivateSplitter(MultiBall ball)
    {
        ball.SplitNow();
    }
}
