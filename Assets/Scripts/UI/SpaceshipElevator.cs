// Created with Claude Code (Opus 4.8) by JJ on 2026-06-04: hangar elevator that
// rises from the floor carrying the selected ship, spinning slightly as it comes up.
using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the spaceship-selection elevator. A "hangar elevator" transform slides
/// between a lowered position (hidden in the floor) and a raised position, spinning
/// as it moves. The selected ship's model prefab is spawned at a child "ship point"
/// so it rides up with the elevator.
///
/// The platform rests at the top by default. Choosing (or swapping) a ship spins it
/// down, swaps the model at the bottom, then spins it back up with the new ship.
///
/// Referenced by <see cref="Monitor2Controller"/>: it calls <see cref="ShowShip"/>
/// whenever a ship is chosen, and <see cref="Reset"/> to return to the empty top state.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpaceshipElevator : MonoBehaviour
{
    [Header("Transforms")]
    [Tooltip("The hangar elevator object that moves up and down. " +
             "Defaults to this GameObject's transform if left empty.")]
    [SerializeField] private Transform elevator;

    [Tooltip("Child point where the selected ship model is placed.")]
    [SerializeField] private Transform shipPoint;

    [Tooltip("Size multiplier applied to the docked ship model on top of its prefab " +
             "scale. 2 = twice the model's normal size.")]
    [SerializeField] private float shipScaleMultiplier = 2f;

    [Header("Travel")]
    [Tooltip("Local Y position when fully lowered (hidden in the floor).")]
    [SerializeField] private float loweredLocalY = 0f;

    [Tooltip("Local Y position when fully raised into view.")]
    [SerializeField] private float raisedLocalY = 2f;

    [Tooltip("Seconds the elevator takes to travel between lowered and raised.")]
    [SerializeField] private float travelDuration = 1.5f;

    [Header("Spin")]
    [Tooltip("Total degrees the elevator spins around its up axis as it rises. " +
             "The spin eases with the travel, so it slows and settles at the top.")]
    [SerializeField] private float spinDegrees = 360f;

    [Tooltip("Where the empty platform rests before any ship is chosen. " +
             "On by default so the first selection drops down and rises with the ship.")]
    [SerializeField] private bool startRaised = true;

    private GameObject _currentShip;
    private TrailRenderer[] _currentTrails;
    private Coroutine _routine;

    // The elevator's resting local rotation, captured at Awake. Every Move ends by
    // snapping back to this so an interrupted spin (e.g. spam-clicking ship selection)
    // can't leave the platform — and the ship riding it — facing the wrong way.
    private Quaternion _homeRotation;

    /// <summary>
    /// True while a show/swap animation is in progress (the platform is dropping,
    /// swapping the model, or rising back up). Callers can poll this to hold off
    /// actions — e.g. Start — until the ship has fully settled at the top.
    /// </summary>
    public bool IsAnimating => _routine != null;

    private void Awake()
    {
        if (elevator == null)
        {
            elevator = transform;
        }

        _homeRotation = elevator.localRotation;
        SetLocalY(startRaised ? raisedLocalY : loweredLocalY);
    }

    /// <summary>
    /// Swaps in the given ship's model: spins the platform down, replaces the model
    /// at the bottom, then spins it back up. Same motion for the first pick or a swap.
    /// </summary>
    public void ShowShip(PlayerShipDefinition ship)
    {
        ShowShip(ship != null ? ship.shipModelPrefab : null);
    }

    /// <summary>
    /// Swaps in the given model prefab with a down-then-up cycle. Passing null just
    /// clears the platform and brings it back up empty.
    /// </summary>
    public void ShowShip(GameObject shipModelPrefab)
    {
        Play(SwapSequence(shipModelPrefab));
    }

    /// <summary>Snaps the platform back to its empty, raised default (no animation).</summary>
    public void Reset()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        ClearShip();
        SetLocalY(raisedLocalY);
        elevator.localRotation = _homeRotation;
    }

    // Spin down, swap the model at the bottom, then spin back up.
    private IEnumerator SwapSequence(GameObject shipModelPrefab)
    {
        AudioManager audio = ServiceLocator.Get<AudioManager>();

        // Drop-target "down" as the platform sinks into the floor to swap the model.
        audio?.PlayDropTargetDown(elevator.position);
        yield return Move(loweredLocalY);

        ClearShip();
        if (shipModelPrefab != null && shipPoint != null)
        {
            _currentShip = Instantiate(shipModelPrefab, shipPoint);
            _currentShip.transform.localPosition = Vector3.zero;
            _currentShip.transform.localRotation = Quaternion.identity;
            _currentShip.transform.localScale *= shipScaleMultiplier;

            // Some ship models (e.g. Loric) carry engine TrailRenderers. Those draw in
            // world space, so while the platform rises and spins they streak across the
            // scene and clip through the hangar walls. Silence them on the docked model;
            // ReleaseShip re-enables them so the trails still fire during the launch fly-out.
            _currentTrails = _currentShip.GetComponentsInChildren<TrailRenderer>(includeInactive: true);
            SetTrailsEmitting(false);
        }

        // Drop-target "up" as it rises back into view carrying the chosen ship.
        audio?.PlayDropTargetUp(elevator.position);
        yield return Move(raisedLocalY);
        _routine = null;
    }

    /// <summary>
    /// Detaches the current ship from the platform and hands it to the caller (e.g.
    /// the launch sequence), so it no longer rides the elevator. Returns null if empty.
    /// </summary>
    public GameObject ReleaseShip()
    {
        GameObject ship = _currentShip;
        _currentShip = null;
        if (ship != null)
        {
            // Hand the ship to the launch sequence with its engine trails live again,
            // cleared so they start fresh from the launch point rather than streaking
            // from where it sat docked.
            SetTrailsEmitting(true);
            ship.transform.SetParent(null, worldPositionStays: true);
        }
        _currentTrails = null;
        return ship;
    }

    private void ClearShip()
    {
        if (_currentShip != null)
        {
            Destroy(_currentShip);
            _currentShip = null;
        }
        _currentTrails = null;
    }

    // Toggles emission on the docked ship's TrailRenderers (and clears any trail already
    // drawn) so the engine trails don't streak across the hangar while it spins.
    private void SetTrailsEmitting(bool emitting)
    {
        if (_currentTrails == null)
        {
            return;
        }

        foreach (TrailRenderer trail in _currentTrails)
        {
            if (trail == null)
            {
                continue;
            }

            trail.Clear();
            trail.emitting = emitting;
        }
    }

    private void Play(IEnumerator sequence)
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
        }
        _routine = StartCoroutine(sequence);
    }

    // Eased slide to targetY with a spin that decelerates and settles on arrival.
    private IEnumerator Move(float targetY)
    {
        float startY = elevator.localPosition.y;
        float duration = Mathf.Max(0.01f, travelDuration);
        float elapsed = 0f;
        float prevT = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            SetLocalY(Mathf.LerpUnclamped(startY, targetY, t));
            elevator.Rotate(0f, (t - prevT) * spinDegrees, 0f, Space.Self);
            prevT = t;
            yield return null;
        }

        SetLocalY(targetY);

        // Settle on a deterministic orientation. The incremental spin above accumulates
        // from wherever the platform happened to be when this Move started, so if a prior
        // spin was interrupted mid-flight the angle would otherwise drift; snapping to the
        // home rotation here guarantees the ship always finishes facing the right way.
        elevator.localRotation = _homeRotation;
    }

    private void SetLocalY(float y)
    {
        Vector3 p = elevator.localPosition;
        p.y = y;
        elevator.localPosition = p;
    }
}
