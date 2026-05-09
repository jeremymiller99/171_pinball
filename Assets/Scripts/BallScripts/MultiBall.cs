// Updated with Cursor (claude-4.6-opus) by jjmil on 2026-03-24.
using System;
using UnityEngine;

public class MultiBall : Ball, ISplitter
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private BallSpawner ballSpawner;
    public bool readyToSplit = true;

    [Header("Split Trigger")]
    [Tooltip("Split after this many board component hits.")]
    [Min(1)]
    [SerializeField] private int componentHitsToSplit = 5;
    public int BallsOnSplit { get; set; } = 1;

    [SerializeField] private bool hasSplit;
    

    protected override int HitIntervalForPopup => hasSplit ? 0 : componentHitsToSplit;


    void Awake()
    {
        ballSpawner = ServiceLocator.Get<BallSpawner>();
    }

    private void OnValidate()
    {
        if (componentHitsToSplit < 1) componentHitsToSplit = 1;
    }

    public void SplitNow()
    {
        readyToSplit = false;
        hasSplit = true;

        if (ballSpawner == null)
        {
            ballSpawner = ServiceLocator.Get<BallSpawner>();
        }
        for (int i = 0; i < BallsOnSplit; i++)
        {
            GameObject newBall = Instantiate(prefab, transform.position, transform.rotation);
            EnsureOwnMaterials(newBall);

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
    }

    override protected void AddScore(float amount, TypeOfScore typeOfScore, Transform pos)
    {
        if (componentHits >= componentHitsToSplit && !hasSplit)
        {
            SplitNow();
            componentHits = 0;
        }
        
        base.AddScore(amount, typeOfScore, pos);
    }

}
