// Updated with Cursor (claude-4.6-opus) by assistant on 2026-02-25.
using UnityEngine;

public class MultiBall : Ball
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private BallSpawner ballSpawner;
    public bool readyToSplit = true;

    [Header("Split Trigger")]
    [Tooltip("Split after this many board component hits.")]
    [Min(1)]
    [SerializeField] private int componentHitsToSplit = 5;

    [SerializeField] private bool hasSplit;


    new void Awake()
    {
        base.Awake();
        ballSpawner = FindFirstObjectByType<BallSpawner>();
    }

    private void OnValidate()
    {
        if (componentHitsToSplit < 1) componentHitsToSplit = 1;
    }

    private void SplitNow()
    {
        readyToSplit = false;
        hasSplit = true;

        if (ballSpawner == null)
        {
            ballSpawner = FindFirstObjectByType<BallSpawner>();
        }

        GameObject newBall = Instantiate(prefab, transform.position, transform.rotation);
        MultiBall child = newBall.GetComponent<MultiBall>();
        if (child != null)
        {
            child.readyToSplit = false;
            child.hasSplit = true;
            child.componentHits = 0;
            child.componentHitsToSplit = componentHitsToSplit;
        }

        if (ballSpawner != null)
        {
            ballSpawner.ActiveBalls.Add(newBall);
        }
    }

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits >= componentHitsToSplit && !hasSplit)
        {
            SplitNow();
        }
        
        base.AddScore(amount, typeOfScore, pos);
    }

}
