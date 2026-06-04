// Created with Claude Code (Opus 4.8) by JJ on 2026-06-04: launch cinematic played
// when "Start" is pressed on monitor 2a — a door slides up and the ship flies out,
// after which Monitor2Controller transitions to the gameplay scene.
using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the pre-launch cinematic: a door object slides up, then the selected ship
/// flies out through it. Owns only the door and the flight motion — it does NOT load
/// the scene. <see cref="Monitor2Controller"/> awaits <see cref="Play"/> and then
/// runs the actual scene transition, keeping run config + scene loading in one place.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShipLaunchSequence : MonoBehaviour
{
    [Header("Door")]
    [Tooltip("Door object that slides up to open. Its placed position is treated as closed.")]
    [SerializeField] private Transform door;

    [Tooltip("How far (local units) the door slides up to open.")]
    [SerializeField] private float doorOpenHeight = 3f;

    [Tooltip("Seconds the door takes to open.")]
    [SerializeField] private float doorOpenDuration = 0.6f;

    [Header("Ship flight")]
    [Tooltip("Optional point the ship flies toward. If unset, the ship flies along its " +
             "own up axis by Flight Distance.")]
    [SerializeField] private Transform flightTarget;

    [Tooltip("Distance the ship travels when no Flight Target is set.")]
    [SerializeField] private float flightDistance = 25f;

    [Tooltip("Seconds the ship takes to fly out (accelerates as it goes).")]
    [SerializeField] private float flightDuration = 1f;

    [Tooltip("Launch acceleration shape. 1 = linear; higher = slower start then a " +
             "faster 'shoot out' at the end. 3-4 reads as a strong burst.")]
    [Min(1f)]
    [SerializeField] private float flightAcceleration = 3f;

    [Tooltip("Extra beat held after the ship leaves before the scene transition.")]
    [SerializeField] private float postLaunchDelay = 0.15f;

    // Door's authored Y, captured as the "closed" position.
    private float _doorClosedY;

    private void Awake()
    {
        if (door != null)
        {
            _doorClosedY = door.localPosition.y;
        }
    }

    /// <summary>
    /// Opens the door and flies the given ship out, then returns. Pass the ship
    /// instance handed off from the elevator (may be null — the door still opens).
    /// </summary>
    public IEnumerator Play(Transform ship)
    {
        if (door != null)
        {
            yield return MoveDoorY(_doorClosedY + doorOpenHeight, doorOpenDuration);
        }

        if (ship != null)
        {
            yield return FlyOut(ship);
        }

        if (postLaunchDelay > 0f)
        {
            yield return new WaitForSeconds(postLaunchDelay);
        }
    }

    private IEnumerator MoveDoorY(float targetY, float duration)
    {
        float startY = door.localPosition.y;
        float dur = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            Vector3 p = door.localPosition;
            p.y = Mathf.LerpUnclamped(startY, targetY, t);
            door.localPosition = p;
            yield return null;
        }

        Vector3 end = door.localPosition;
        end.y = targetY;
        door.localPosition = end;
    }

    private IEnumerator FlyOut(Transform ship)
    {
        Vector3 start = ship.position;
        Vector3 target = flightTarget != null
            ? flightTarget.position
            : start + ship.up * flightDistance;

        float dur = Mathf.Max(0.01f, flightDuration);
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            // Ease-in: starts slow and accelerates, so the ship "shoots out" at the
            // end. Higher flightAcceleration = slower start, faster finish.
            float eased = Mathf.Pow(t, flightAcceleration);
            ship.position = Vector3.LerpUnclamped(start, target, eased);
            yield return null;
        }

        ship.position = target;
    }
}
