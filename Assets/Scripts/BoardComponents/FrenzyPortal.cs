// Generated with Antigravity by jjmil on 2026-04-09.
// Frenzy portal: awards bonus + activates frenzy via
// DropTargetsScoringMode when the ball enters.
// Frenzy-activation SFX hook added by Claude Code (Opus 4.7) for jjmil on 2026-04-21.
using UnityEngine;

/// <summary>
/// Place on the portal GameObject hidden behind the 4 bumpers.
/// When the ball enters, awards configurable bonus points and
/// multiplier, then activates frenzy mode via the linked
/// <see cref="DropTargetsScoringMode"/>.
/// Teleportation is handled by the existing <see cref="Portal"/>
/// component on the same GameObject.
/// </summary>
public class FrenzyPortal : MonoBehaviour
{
    [Header("Portal Rewards")]
    [Tooltip("Bonus multiplier added on portal entry.")]
    [SerializeField] private float bonusMult = 0.5f;

    [Header("References")]
    [SerializeField] private FrenzyManager frenzyManager;
    [SerializeField] private ScoreManager scoreManager;
    [Tooltip("Point where the frenzy VFX spawns. Falls back to this object's position.")]
    [SerializeField] private Transform frenzyVFXPoint;

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

        if (frenzyManager != null)
        {
            Vector3 frenzyVFXPos = frenzyVFXPoint != null
                ? frenzyVFXPoint.position
                : transform.position;
            frenzyManager.ActivateFrenzy(frenzyVFXPos);
        }
    }

    private void EnsureRefs()
    {
        if (scoreManager == null)
        {
            scoreManager =
                ServiceLocator.Get<ScoreManager>();
        }

        if (frenzyManager == null)
        {
            frenzyManager =
                ServiceLocator.Get<FrenzyManager>();
        }
    }
}
