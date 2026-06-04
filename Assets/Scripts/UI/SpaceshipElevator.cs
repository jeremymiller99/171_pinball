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
    private Coroutine _routine;

    private void Awake()
    {
        if (elevator == null)
        {
            elevator = transform;
        }

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
    }

    // Spin down, swap the model at the bottom, then spin back up.
    private IEnumerator SwapSequence(GameObject shipModelPrefab)
    {
        yield return Move(loweredLocalY);

        ClearShip();
        if (shipModelPrefab != null && shipPoint != null)
        {
            _currentShip = Instantiate(shipModelPrefab, shipPoint);
            _currentShip.transform.localPosition = Vector3.zero;
            _currentShip.transform.localRotation = Quaternion.identity;
        }

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
            ship.transform.SetParent(null, worldPositionStays: true);
        }
        return ship;
    }

    private void ClearShip()
    {
        if (_currentShip != null)
        {
            Destroy(_currentShip);
            _currentShip = null;
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
    }

    private void SetLocalY(float y)
    {
        Vector3 p = elevator.localPosition;
        p.y = y;
        elevator.localPosition = p;
    }
}
