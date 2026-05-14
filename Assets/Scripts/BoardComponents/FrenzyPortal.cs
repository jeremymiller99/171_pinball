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
    [Tooltip("Bonus points awarded on portal entry.")]
    [SerializeField] private float bonusPoints = 500f;
    [Tooltip("Bonus multiplier added on portal entry.")]
    [SerializeField] private float bonusMult = 0.5f;
    [Tooltip("Canvas offset for bonus popups.")]
    [SerializeField]
    private Vector2 popupOffset =
        new Vector2(0f, -100f);

    [Header("References")]
    [SerializeField] private FrenzyManager frenzyManager;
    [SerializeField] private ScoreManager scoreManager;

    private void Awake()
    {
        EnsureRefs();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        EnsureRefs();

        if (scoreManager != null)
        {
            if (bonusPoints > 0f)
            {
                scoreManager.AddScore(
                    bonusPoints,
                    TypeOfScore.points,
                    transform);
            }

            if (bonusMult > 0f)
            {
                scoreManager.AddScore(
                    bonusMult,
                    TypeOfScore.mult,
                    transform);
            }
        }

        if (frenzyManager != null)
        {
            frenzyManager.ActivateFrenzy(transform.position, popupOffset);
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
