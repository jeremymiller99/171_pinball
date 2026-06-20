// Updated 2026-04-09: passive mult uses base AddScore only (no ScoreManager API changes required).
using UnityEngine;

/// <summary>
/// Amplifier-class ball: while this instance is in the spawner's active list, awards additive mult
/// on a fixed interval (default +0.1 mult every 5 seconds). Uses <see cref="Ball"/>'s scoring path
/// (same as board mult hits; each tick also registers as a component hit in <see cref="ScoreManager"/>).
/// </summary>
public class Pitball : Ball
{
    [Header("Passive mult")]
    [Tooltip("Distance travelled between each mult award.")]
    [Min(0.01f)]
    [SerializeField] private float distancePerMultAward = 5f;
    [SerializeField] private float distTravelled = 0f;
    [SerializeField] private Vector3 prevPos = Vector3.zero;
    [Tooltip("Additive mult granted each tick (before ScoreManager mult modifiers).")]
    [SerializeField] private float multToAdd = 0.1f;
    [SerializeField] private BallSpawner ballSpawner;


    private void Awake()
    {
        if (ballSpawner == null)
        {
            ballSpawner = ServiceLocator.Get<BallSpawner>();
        }
    }

    private void Update()
    {
        if (!IsTrackedActiveBall())
        {
            return;
        }

        if (prevPos == Vector3.zero)
        {
            prevPos = transform.position;
            return;
        }

        distTravelled += Vector3.Distance(transform.position, prevPos);
        prevPos = transform.position;

        while (distTravelled >= distancePerMultAward)
        {
            distTravelled = 0;
            base.AddScore(multToAdd, TypeOfScore.mult, transform);
        }
    }

    private bool IsTrackedActiveBall()
    {
        if (ballSpawner == null)
        {
            ballSpawner = ServiceLocator.Get<BallSpawner>();
        }

        if (ballSpawner == null)
        {
            return false;
        }

        var active = ballSpawner.ActiveBalls;
        if (active == null)
        {
            return false;
        }

        for (int i = 0; i < active.Count; i++)
        {
            if (active[i] == gameObject)
            {
                return true;
            }
        }

        return false;
    }
}
