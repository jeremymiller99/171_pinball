using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// When the active round modifier has cyclic hide ball enabled, hides all active balls (and their trails)
/// for a set duration every cycle. Add this to the same scene as GameRulesManager and BallSpawner.
/// </summary>
public class BallHideController : MonoBehaviour
{
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private BallSpawner ballSpawner;

    private float _cycleTimer;
    private bool _phaseVisible; // true = showing, false = hidden
    private bool _justBecameVisible; // true for one frame after switching to visible (so we Clear trail once)
    private bool _modifierWasActive;

    private void Awake()
    {
        if (gameRulesManager == null)
            gameRulesManager = FindFirstObjectByType<GameRulesManager>();
        if (ballSpawner == null)
            ballSpawner = FindFirstObjectByType<BallSpawner>();
    }

    private void LateUpdate()
    {
        var modifier = gameRulesManager != null ? gameRulesManager.ActiveModifier : null;
        bool modifierActive = modifier != null && modifier.cyclicHideBallEnabled;

        if (!modifierActive)
        {
            _cycleTimer = 0f;
            _phaseVisible = true;
            _justBecameVisible = false;
            if (_modifierWasActive)
                ApplyBallsVisibility(true, clearTrails: false);
            _modifierWasActive = false;
            return;
        }

        _modifierWasActive = true;
        float visibleSec = Mathf.Max(0.1f, modifier.cyclicHideBallVisibleSeconds);
        float hiddenSec = Mathf.Max(0.1f, modifier.cyclicHideBallHiddenSeconds);
        float phaseDuration = _phaseVisible ? visibleSec : hiddenSec;

        _cycleTimer += Time.deltaTime;
        if (_cycleTimer >= phaseDuration)
        {
            _cycleTimer -= phaseDuration;
            _phaseVisible = !_phaseVisible;
            if (_phaseVisible)
                _justBecameVisible = true;
        }

        // Apply every frame so we override Ball's trail toggles (e.g. OnTriggerExit) and keep state consistent.
        ApplyBallsVisibility(_phaseVisible, clearTrails: _justBecameVisible);
        if (_justBecameVisible)
            _justBecameVisible = false;
    }

    private void ApplyBallsVisibility(bool visible, bool clearTrails)
    {
        var balls = GetActiveBalls();
        if (balls == null) return;

        foreach (var go in balls)
        {
            if (go == null) continue;
            SetBallVisibility(go, visible, clearTrails);
        }
    }

    private void SetBallVisibility(GameObject ball, bool visible, bool clearTrails)
    {
        foreach (var r in ball.GetComponentsInChildren<Renderer>(true))
        {
            if (r != null)
                r.enabled = visible;
        }
        var trail = ball.GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.enabled = visible;
            if (visible && clearTrails)
                trail.Clear();
        }
        foreach (var trailInChild in ball.GetComponentsInChildren<TrailRenderer>(true))
        {
            if (trailInChild != null)
            {
                trailInChild.enabled = visible;
                if (visible && clearTrails)
                    trailInChild.Clear();
            }
        }
    }

    private List<GameObject> GetActiveBalls()
    {
        if (ballSpawner != null)
            return ballSpawner.ActiveBalls;
        if (gameRulesManager != null)
            return gameRulesManager.ActiveBalls;
        return null;
    }
}
