// Updated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-17.
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

    [Header("Split Scoring")]
    [Tooltip("Points multiplier applied to balls created by this ball's split.")]
    [Range(0f, 1f)]
    [SerializeField] private float splitChildMultiplier = 0.5f;

    [Tooltip("True if this ball was created as a split child.")]
    [SerializeField] private bool isSplitChild;


    new void Awake()
    {
        base.Awake();
        ballSpawner = FindFirstObjectByType<BallSpawner>();
    }

    new void Start()
    {
        base.Start();
        if (isSplitChild)
        {
            pointMultiplier = splitChildMultiplier;
            multMultiplier = splitChildMultiplier;
        }
    }

    private void OnValidate()
    {
        if (componentHitsToSplit < 1) componentHitsToSplit = 1;
        if (splitChildMultiplier < 0f) splitChildMultiplier = 0f;
        if (splitChildMultiplier > 1f) splitChildMultiplier = 1f;
    }

    private void SplitNow()
    {
        readyToSplit = false;

        // After splitting, BOTH balls should score at the split (reduced) rate.
        isSplitChild = true;
        pointMultiplier = splitChildMultiplier;
        multMultiplier = splitChildMultiplier;  

        if (ballSpawner == null)
        {
            ballSpawner = FindFirstObjectByType<BallSpawner>();
        }

        GameObject newBall = Instantiate(prefab, transform.position, transform.rotation);
        MultiBall child = newBall.GetComponent<MultiBall>();
        if (child != null)
        {
            child.readyToSplit = false;
            child.isSplitChild = true;
            child.componentHits = 0;
            child.componentHitsToSplit = componentHitsToSplit;
            // Ensure child uses the same split scoring multiplier as the parent (in case prefab differs).
            child.splitChildMultiplier = splitChildMultiplier;
        }

        if (ballSpawner != null)
        {
            ballSpawner.ActiveBalls.Add(newBall);
        }
    }

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits >= componentHitsToSplit && !isSplitChild)
        {
            SplitNow();
        }
        
        base.AddScore(amount, typeOfScore, pos);
    }

}
