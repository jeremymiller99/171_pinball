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

    [SerializeField] private int componentHitsSoFar;

    [Header("Split Scoring")]
    [Tooltip("Points multiplier applied to balls created by this ball's split.")]
    [Range(0f, 1f)]
    [SerializeField] private float splitChildPointsMultiplier = 0.5f;

    [Tooltip("True if this ball was created as a split child.")]
    [SerializeField] private bool isSplitChild;

    public override float PointsAwardMultiplier => isSplitChild ? splitChildPointsMultiplier : 1f;


    void Awake()
    {
        base.Awake();
        ballSpawner = FindFirstObjectByType<BallSpawner>();
    }

    private void OnValidate()
    {
        if (componentHitsToSplit < 1) componentHitsToSplit = 1;
        if (splitChildPointsMultiplier < 0f) splitChildPointsMultiplier = 0f;
        if (splitChildPointsMultiplier > 1f) splitChildPointsMultiplier = 1f;
    }


    private void RegisterComponentHitAndMaybeSplit()
    {
        if (!readyToSplit) return;

        componentHitsSoFar++;
        if (componentHitsSoFar < componentHitsToSplit) return;

        SplitNow();
    }

    private void SplitNow()
    {
        readyToSplit = false;

        // After splitting, BOTH balls should score at the split (reduced) rate.
        isSplitChild = true;

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
            child.componentHitsSoFar = 0;
            child.componentHitsToSplit = componentHitsToSplit;
            // Ensure child uses the same split scoring multiplier as the parent (in case prefab differs).
            child.splitChildPointsMultiplier = splitChildPointsMultiplier;
        }

        if (ballSpawner != null)
        {
            ballSpawner.ActiveBalls.Add(newBall);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        BoardComponent component = collision.collider.GetComponent<BoardComponent>();
        if (!component) return;
        HandleParticles(collision);
        RegisterComponentHitAndMaybeSplit();
        if (component.typeOfScore == TypeOfScore.coins)
        {
            AddScore(component.amountToScore, TypeOfScore.coins, transform);
        } else
        {
            AddScore(component.amountToScore * PointsAwardMultiplier, component.typeOfScore, transform);
        }
        
    }

}
