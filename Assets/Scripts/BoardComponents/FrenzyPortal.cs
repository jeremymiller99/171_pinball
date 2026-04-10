// Generated with Antigravity by jjmil on 2026-04-09.
// Frenzy portal: awards bonus + activates frenzy via
// DropTargetsScoringMode when the ball enters.
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

    [Header("References")]
    [Tooltip("The DropTargetsScoringMode that manages frenzy.")]
    [SerializeField]
    private DropTargetsScoringMode scoringMode;

    private ScoreManager _scoreManager;

    private void Awake()
    {
        EnsureRefs();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        EnsureRefs();

        if (_scoreManager != null)
        {
            if (bonusPoints > 0f)
            {
                _scoreManager.AddScore(
                    bonusPoints,
                    TypeOfScore.points,
                    transform);
            }

            if (bonusMult > 0f)
            {
                _scoreManager.AddScore(
                    bonusMult,
                    TypeOfScore.mult,
                    transform);
            }
        }

        if (scoringMode != null)
        {
            scoringMode.ActivateFrenzy();
        }
    }

    private void EnsureRefs()
    {
        if (_scoreManager == null)
        {
            _scoreManager =
                ServiceLocator.Get<ScoreManager>();
        }
    }
}
