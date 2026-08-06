// Generated with Antigravity by jjmil on 2026-04-09.
// Power Surge portal: awards bonus + activates Power Surge via
// DropTargetsScoringMode when the ball enters.
// Power Surge activation SFX hook added by Claude Code (Opus 4.7) for jjmil on 2026-04-21.
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Place on the portal GameObject hidden behind the 4 bumpers.
/// When the ball enters, awards configurable bonus points and
/// multiplier, then activates Power Surge mode via the linked
/// <see cref="DropTargetsScoringMode"/>.
/// Teleportation is handled by the existing <see cref="Portal"/>
/// component on the same GameObject.
/// </summary>
public class PowerSurgePortal : MonoBehaviour
{
    [Header("Portal Rewards")]
    [Tooltip("Bonus multiplier added on portal entry.")]
    [SerializeField] private float bonusMult = 0.5f;

    [Header("References")]
    [FormerlySerializedAs("frenzyManager")]
    [SerializeField] private PowerSurgeManager powerSurgeManager;
    [SerializeField] private ScoreManager scoreManager;
    [Tooltip("Point where the Power Surge VFX spawns. Falls back to this object's position.")]
    [FormerlySerializedAs("frenzyVFXPoint")]
    [SerializeField] private Transform powerSurgeVFXPoint;

    private void Awake()
    {
        EnsureRefs();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        EnsureRefs();

        if (scoreManager != null && bonusMult > 0f)
        {
            scoreManager.AddScore(
                bonusMult,
                TypeOfScore.mult,
                transform);
        }

        if (powerSurgeManager != null)
        {
            Vector3 powerSurgeVFXPos = powerSurgeVFXPoint != null
                ? powerSurgeVFXPoint.position
                : transform.position;
            powerSurgeManager.ActivatePowerSurge(powerSurgeVFXPos);
        }
    }

    private void EnsureRefs()
    {
        if (scoreManager == null)
        {
            scoreManager =
                ServiceLocator.Get<ScoreManager>();
        }

        if (powerSurgeManager == null)
        {
            powerSurgeManager =
                ServiceLocator.Get<PowerSurgeManager>();
        }
    }
}
