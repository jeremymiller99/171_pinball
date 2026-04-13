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
    [Tooltip("Seconds between each passive mult award.")]
    [Min(0.01f)]
    [SerializeField] private float secondsPerMultAward = 5f;
    [Tooltip("Additive mult granted each tick (before ScoreManager mult modifiers).")]
    [SerializeField] private float multAwardPerTick = 0.1f;
    [SerializeField] private BallSpawner ballSpawner;

    private float _secondsAccumulated;


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

        _secondsAccumulated += Time.deltaTime;

        while (_secondsAccumulated >= secondsPerMultAward)
        {
            _secondsAccumulated -= secondsPerMultAward;
            AwardPassiveMult();
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

    private void AwardPassiveMult()
    {
        float scaled = multAwardPerTick * multMultiplier;
        base.AddScore(scaled, TypeOfScore.mult, transform);
    }
}
