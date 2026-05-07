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
        ISplitter splittingInterface = ball.GetComponent<ISplitter>();
        if (splittingInterface != null)
        {
            splittingInterface.BallsOnSplit++;
        }
    }
}
