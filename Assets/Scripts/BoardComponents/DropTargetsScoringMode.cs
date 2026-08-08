// Generated with Antigravity by jjmil on 2026-04-09.
// Drop‑target Power Surge: bumper split‑open, portal reveal, multiplier doubling.
// Power Surge gate SFX hook added by Claude Code (Opus 4.7) for jjmil on 2026-04-21.
// Updated by Claude (Opus 4.8), for jjmil, on 2026-06-04 (defer portal teardown while the entrance
// portal is holding a ball in its teleport delay, so the held ball isn't stranded).
// Updated by Claude (Opus 4.8), for jjmil, on 2026-06-05 (deactivate Power Surge directly when the gate
// closes, so a portal-started Power Surge ends on target-return even while the countdown is paused).
// Updated by Claude (Opus 5), for jjmil, on 2026-08-06 (restart the targets' down countdown whenever the
// Power Surge timer is filled, and raise all three in the same frame when the gate closes).
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Scoring mode tied to 3 drop targets. When all 3 are down, 4 bumpers
/// split open on the X‑axis to reveal a Power Surge portal behind them.
/// Entering the portal doubles the current multiplier and restarts all 3 targets'
/// down countdowns together, so they hold the gate open for the surge and then rise as
/// one. The bumpers close whenever a target comes back up; that also ends the Power
/// Surge and removes the multiplier bonus, but only for a surge this portal granted —
/// one handed out elsewhere on the board (the alien ship, the duplicator bumper) runs
/// its own course and leaves these targets alone.
/// </summary>
public class DropTargetsScoringMode : MonoBehaviour
{

    /// <summary>Fired when all 3 drop targets become down.</summary>
    public event Action OnAllTargetsDown;

    /// <summary>Fired when any target returns up.</summary>
    public event Action OnAnyTargetReturned;

    [Header("Drop Targets")]
    [SerializeField] private Dropper[] dropTargets = new Dropper[3];

    [Header("Board lights")]
    [Tooltip(
        "The three drop-target bulb BoardLights. Refreshes visuals " +
        "when all-down state changes.")]
    [SerializeField] private BoardLight[] dropTargetBulbLights =
        new BoardLight[3];

    [Header("Bonus (when all down)")]
    [Tooltip("Points awarded when all 3 drop targets are down.")]
    [SerializeField] private float allDownBonusPoints = 500f;
    [Tooltip(
        "Transform used for floating text spawn position " +
        "(e.g. center of targets).")]
    [SerializeField] private Transform bonusSpawnPosition;
    [Tooltip("Canvas offset for bonus popups.")]
    [SerializeField] private Vector2 popupOffset =
        new Vector2(0f, -100f);

    [Header("Power Surge Bumper Animation")]
    [Tooltip("Two bumpers on the left side that slide open.")]
    [SerializeField] private Transform[] leftBumpers =
        new Transform[2];
    [Tooltip("Two bumpers on the right side that slide open.")]
    [SerializeField] private Transform[] rightBumpers =
        new Transform[2];
    [Tooltip("X‑axis offset for left bumpers when open (negative).")]
    [SerializeField] private float leftOpenOffsetX = -1f;
    [Tooltip("X‑axis offset for right bumpers when open (positive).")]
    [SerializeField] private float rightOpenOffsetX = 1f;
    [Tooltip("Duration of the bumper open/close animation.")]
    [SerializeField] private float bumperAnimDuration = 0.5f;

    [Header("Power Surge Portals")]
    [Tooltip("The Power Surge portal entrance. Hidden until bumpers open.")]
    [FormerlySerializedAs("frenzyPortalEntrance")]
    [SerializeField] private GameObject powerSurgePortalEntrance;
    [Tooltip("The Power Surge portal exit. Hidden until bumpers open.")]
    [FormerlySerializedAs("frenzyPortalExit")]
    [SerializeField] private GameObject powerSurgePortalExit;

    [Header("References")]
    [FormerlySerializedAs("frenzyManager")]
    [SerializeField] private PowerSurgeManager powerSurgeManager;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Power Surge HUD Color")]
    [Tooltip("Color applied to the multiplier HUD meter during Power Surge. Should match your Power Surge lights.")]
    [FormerlySerializedAs("frenzyHudColor")]
    [SerializeField] private Color powerSurgeHudColor = new Color(0f, 0.85f, 1f, 1f);

    public Color PowerSurgeHudColor => powerSurgeHudColor;

    private bool _allDownBonusAwardedThisCycle;
    private bool _wasAllDown;
    private Coroutine _deferredCheckRoutine;

    // Guards RaiseAllTargetsTogether against re-entering itself: the targets it raises
    // fire onStartUp, which is the very handler that calls it.
    private bool _syncingRise;

    private bool _powerSurgeSubscribed;

    // Cached Portal on the entrance so we can tell when a ball is mid-teleport
    // (held inside the delay) and defer tearing the portals down until it exits.
    private Portal _powerSurgeEntrancePortalComponent;
    private Coroutine _pendingPortalDeactivateRoutine;

    // Bumper animation state
    private Vector3[] _leftClosedPos;
    private Vector3[] _rightClosedPos;
    private Coroutine _bumperAnimRoutine;

    private void Awake()
    {
        // Subscribe here, not in OnEnable: this manager may be disabled while the
        // shop is open, which is exactly when a bumper gets replaced. An
        // OnEnable/OnDisable subscription would be inactive at that moment and miss
        // the swap, leaving a dead Transform in the Power Surge gate slots.
        BoardComponent.Replaced += OnComponentReplaced;

        EnsureRefs();
        CacheBumperClosedPositions();

        if (powerSurgePortalEntrance != null)
        {
            _powerSurgeEntrancePortalComponent =
                powerSurgePortalEntrance.GetComponent<Portal>();
        }

        SetPowerSurgePortalsActive(false);
    }

    private void OnEnable()
    {
        SubscribePowerSurge();

        if (dropTargets == null) return;

        foreach (Dropper dt in dropTargets)
        {
            if (dt != null)
            {
                dt.OnFullyDown += OnTargetFullyDown;
                dt.onStartUp += OnTargetStartedRising;
                dt.OnReturnedUp += OnTargetReturnedUp;
            }
        }

        _allDownBonusAwardedThisCycle = false;

        RefreshDropTargetBulbVisuals();
    }

    private void OnDestroy()
    {
        BoardComponent.Replaced -= OnComponentReplaced;
    }

    private void OnDisable()
    {
        if (_deferredCheckRoutine != null)
        {
            StopCoroutine(_deferredCheckRoutine);
            _deferredCheckRoutine = null;
        }

        if (_bumperAnimRoutine != null)
        {
            StopCoroutine(_bumperAnimRoutine);
            _bumperAnimRoutine = null;
        }

        if (_pendingPortalDeactivateRoutine != null)
        {
            StopCoroutine(_pendingPortalDeactivateRoutine);
            _pendingPortalDeactivateRoutine = null;
        }

        if (_powerSurgeSubscribed && powerSurgeManager != null)
        {
            powerSurgeManager.OnPowerSurgeTimerRefreshed -= OnPowerSurgeTimerRefreshed;
            powerSurgeManager.OnPowerSurgeDeactivated -= OnPowerSurgeDeactivated;
        }
        _powerSurgeSubscribed = false;

        if (dropTargets != null)
        {
            foreach (Dropper dt in dropTargets)
            {
                if (dt != null)
                {
                    dt.OnFullyDown -= OnTargetFullyDown;
                    dt.onStartUp -= OnTargetStartedRising;
                    dt.OnReturnedUp -= OnTargetReturnedUp;
                }
            }
        }
    }

    private void OnTargetFullyDown()
    {
        if (_deferredCheckRoutine != null)
        {
            StopCoroutine(_deferredCheckRoutine);
        }

        _deferredCheckRoutine =
            StartCoroutine(DeferredCheckAllDown());
    }

    // Until the ball reaches the gate portal the targets run their countdowns
    // individually, exactly as before. Every fill of the Power Surge countdown from that
    // portal — the activating entry and any re-entry that extends it — restarts all three
    // in the same frame, so they hold the gate open for the whole surge and then expire
    // together. A surge granted elsewhere on the board (the alien ship, the duplicator
    // bumper) leaves the targets alone.
    private void OnPowerSurgeTimerRefreshed(PowerSurgeSource source)
    {
        if (source != PowerSurgeSource.DropTargetPortal) return;
        if (dropTargets == null) return;

        foreach (Dropper dt in dropTargets)
        {
            if (dt != null)
            {
                dt.RestartResetCountdown();
            }
        }
    }

    // Our own Power Surge ran out: the targets go up as a bank rather than one at a time.
    // A surge the ship or the duplicator started isn't ours to end targets over.
    private void OnPowerSurgeDeactivated()
    {
        if (!OwnsPowerSurge) return;

        RaiseAllTargetsTogether();
    }

    // A target's own countdown can still run out mid-surge — the two are the same length
    // and the surge clock pauses for portal holds while the target clocks don't. When it
    // does, the other two join it in the same frame rather than trailing it by a rise, so
    // the surge always ends with the bank moving as one. Outside our own surge the targets
    // keep rising independently, as before.
    private void OnTargetStartedRising()
    {
        if (_wasAllDown && OwnsPowerSurge && powerSurgeManager.isPowerSurgeActive)
        {
            RaiseAllTargetsTogether();
        }
    }

    // Whether the surge in play — or the one that just ended, which is the state the
    // deactivation handler sees — is the one this gate's portal granted. Pair it with
    // isPowerSurgeActive anywhere the surge must still be running: the source lingers after
    // the surge ends so deactivation can be attributed.
    private bool OwnsPowerSurge =>
        powerSurgeManager != null
        && powerSurgeManager.ActiveSource == PowerSurgeSource.DropTargetPortal;

    private void RaiseAllTargetsTogether()
    {
        if (_syncingRise || dropTargets == null) return;

        _syncingRise = true;
        foreach (Dropper dt in dropTargets)
        {
            if (dt != null)
            {
                dt.ForceReturnUp();
            }
        }
        _syncingRise = false;
    }

    private void OnTargetReturnedUp()
    {
        if (_deferredCheckRoutine != null)
        {
            StopCoroutine(_deferredCheckRoutine);
            _deferredCheckRoutine = null;
        }

        if (_wasAllDown)
        {
            ClosePowerSurgeGate();
        }

        _allDownBonusAwardedThisCycle = false;
    }

    // The manager lives in another scene, so ServiceLocator may not have it yet when this
    // component enables. The gate cannot open — and so no surge can be granted here —
    // without a target going down first, which makes DeferredCheckAllDown a reliable
    // second chance to hook up.
    private void SubscribePowerSurge()
    {
        if (_powerSurgeSubscribed) return;

        EnsureRefs();
        if (powerSurgeManager == null) return;

        powerSurgeManager.OnPowerSurgeTimerRefreshed += OnPowerSurgeTimerRefreshed;
        powerSurgeManager.OnPowerSurgeDeactivated += OnPowerSurgeDeactivated;
        _powerSurgeSubscribed = true;
    }

    private IEnumerator DeferredCheckAllDown()
    {
        yield return null;

        _deferredCheckRoutine = null;

        SubscribePowerSurge();

        bool allDown = AllTargetsDown();

        if (allDown)
        {
            if (!_wasAllDown)
            {
                _wasAllDown = true;
                AnimateBumpers(true);
                SetPowerSurgePortalsActive(true);
                ServiceLocator.Get<AudioManager>()?.PlayPowerSurgeGate(
                    bonusSpawnPosition != null
                        ? bonusSpawnPosition.position
                        : transform.position);
                OnAllTargetsDown?.Invoke();
            }

            if (!_allDownBonusAwardedThisCycle)
            {
                AwardAllDownBonus();
            }
        }
        else
        {
            if (_wasAllDown)
            {
                ClosePowerSurgeGate();
            }

            _allDownBonusAwardedThisCycle = false;
        }

        RefreshDropTargetBulbVisuals();
    }

    // Closes the Power Surge gate after all-down ends: retracts the bumpers, hides
    // the portals (deferred if a ball is mid-teleport), ends a Power Surge this gate
    // granted, and plays the gate SFX. DeactivatePowerSurge is called directly
    // (not via the PowerSurgeManager countdown) so a portal-started Power Surge ends the
    // instant a target returns up — even while the countdown is paused because a
    // ball is held inside the portal's teleport delay. A surge from another source is
    // left running: the targets don't own it, so closing their gate can't cancel it.
    private void ClosePowerSurgeGate()
    {
        _wasAllDown = false;
        AnimateBumpers(false);
        SetPowerSurgePortalsActive(false);

        EnsureRefs();
        // Only end a surge this gate granted. A surge the alien ship or the duplicator
        // bumper handed out runs on its own clock and isn't the targets' to cancel.
        if (OwnsPowerSurge)
        {
            powerSurgeManager.DeactivatePowerSurge();
        }

        ServiceLocator.Get<AudioManager>()?.PlayPowerSurgeGate(
            bonusSpawnPosition != null
                ? bonusSpawnPosition.position
                : transform.position);
        OnAnyTargetReturned?.Invoke();
    }

    private void RefreshDropTargetBulbVisuals()
    {
        if (dropTargetBulbLights == null)
        {
            return;
        }

        for (int i = 0; i < dropTargetBulbLights.Length; i++)
        {
            if (dropTargetBulbLights[i] != null)
            {
                dropTargetBulbLights[i].ReapplyVisuals();
            }
        }
    }

    private bool AllTargetsDown()
    {
        if (dropTargets == null || dropTargets.Length == 0)
            return false;

        foreach (Dropper dt in dropTargets)
        {
            if (dt == null || !dt.IsDown) return false;
        }

        return true;
    }

    /// <summary>True when all 3 drop targets are down.</summary>
    public bool AllTargetsDownNow => AllTargetsDown();

    // ── Bonus award ───────────────────────────────────────────

    private void AwardAllDownBonus()
    {
        if (allDownBonusPoints <= 0f) return;

        EnsureRefs();
        if (scoreManager == null) return;

        Transform pos = bonusSpawnPosition != null
            ? bonusSpawnPosition
            : transform;

        scoreManager.AddScore(
            allDownBonusPoints,
            TypeOfScore.points,
            pos,
            popupOffset);

        _allDownBonusAwardedThisCycle = true;
    }

    // ── Bumper animation ──────────────────────────────────────

    // When a Power Surge gate bumper is swapped out in the shop, the old instance is
    // destroyed but this manager still holds a Transform slot to it. Re-point the
    // slot to the replacement and refresh its cached closed position, so the
    // open/close animation drives the new bumper instead of a dead reference.
    private void OnComponentReplaced(
        BoardComponent oldComponent, BoardComponent newComponent)
    {
        if (oldComponent == null || newComponent == null) return;

        Transform oldTransform = oldComponent.transform;
        Transform newTransform = newComponent.transform;

        for (int i = 0; i < leftBumpers.Length; i++)
        {
            if (leftBumpers[i] == oldTransform)
            {
                leftBumpers[i] = newTransform;
                if (_leftClosedPos != null && i < _leftClosedPos.Length)
                    _leftClosedPos[i] = newTransform.localPosition;
            }
        }

        for (int i = 0; i < rightBumpers.Length; i++)
        {
            if (rightBumpers[i] == oldTransform)
            {
                rightBumpers[i] = newTransform;
                if (_rightClosedPos != null && i < _rightClosedPos.Length)
                    _rightClosedPos[i] = newTransform.localPosition;
            }
        }
    }

    private void CacheBumperClosedPositions()
    {
        _leftClosedPos = new Vector3[leftBumpers.Length];
        for (int i = 0; i < leftBumpers.Length; i++)
        {
            if (leftBumpers[i] != null)
                _leftClosedPos[i] = leftBumpers[i].localPosition;
        }

        _rightClosedPos = new Vector3[rightBumpers.Length];
        for (int i = 0; i < rightBumpers.Length; i++)
        {
            if (rightBumpers[i] != null)
                _rightClosedPos[i] =
                    rightBumpers[i].localPosition;
        }
    }

    private void AnimateBumpers(bool open)
    {
        if (_bumperAnimRoutine != null)
        {
            StopCoroutine(_bumperAnimRoutine);
        }

        _bumperAnimRoutine =
            StartCoroutine(BumperAnimRoutine(open));
    }

    private IEnumerator BumperAnimRoutine(bool open)
    {
        float duration = Mathf.Max(0.01f, bumperAnimDuration);
        float elapsed = 0f;

        // Capture current positions as animation start.
        Vector3[] leftStart = new Vector3[leftBumpers.Length];
        Vector3[] leftTarget = new Vector3[leftBumpers.Length];
        for (int i = 0; i < leftBumpers.Length; i++)
        {
            if (leftBumpers[i] == null) continue;
            leftStart[i] = leftBumpers[i].localPosition;
            leftTarget[i] = open
                ? _leftClosedPos[i]
                    + new Vector3(leftOpenOffsetX, 0f, 0f)
                : _leftClosedPos[i];
        }

        Vector3[] rightStart = new Vector3[rightBumpers.Length];
        Vector3[] rightTarget =
            new Vector3[rightBumpers.Length];
        for (int i = 0; i < rightBumpers.Length; i++)
        {
            if (rightBumpers[i] == null) continue;
            rightStart[i] = rightBumpers[i].localPosition;
            rightTarget[i] = open
                ? _rightClosedPos[i]
                    + new Vector3(rightOpenOffsetX, 0f, 0f)
                : _rightClosedPos[i];
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease-in-out for smooth animation.
            float eased = t * t * (3f - 2f * t);

            for (int i = 0; i < leftBumpers.Length; i++)
            {
                if (leftBumpers[i] != null)
                {
                    leftBumpers[i].localPosition =
                        Vector3.Lerp(
                            leftStart[i],
                            leftTarget[i],
                            eased);
                }
            }

            for (int i = 0; i < rightBumpers.Length; i++)
            {
                if (rightBumpers[i] != null)
                {
                    rightBumpers[i].localPosition =
                        Vector3.Lerp(
                            rightStart[i],
                            rightTarget[i],
                            eased);
                }
            }

            yield return null;
        }

        // Snap to final positions.
        for (int i = 0; i < leftBumpers.Length; i++)
        {
            if (leftBumpers[i] != null)
                leftBumpers[i].localPosition = leftTarget[i];
        }

        for (int i = 0; i < rightBumpers.Length; i++)
        {
            if (rightBumpers[i] != null)
                rightBumpers[i].localPosition = rightTarget[i];
        }

        _bumperAnimRoutine = null;
    }

    // ── Portal visibility ─────────────────────────────────────

    private void SetPowerSurgePortalsActive(bool active)
    {
        if (active)
        {
            // Re-opening: cancel any deferred teardown that hadn't fired yet.
            if (_pendingPortalDeactivateRoutine != null)
            {
                StopCoroutine(_pendingPortalDeactivateRoutine);
                _pendingPortalDeactivateRoutine = null;
            }

            ApplyPowerSurgePortalsActive(true);
            return;
        }

        // Deactivating: if a ball is currently held inside the entrance portal's
        // teleport delay, removing the portals now would strand it (the delay
        // coroutine dies with the entrance and the exit it needs disappears).
        // Defer teardown until the ball has exited.
        if (_powerSurgeEntrancePortalComponent != null
            && _powerSurgeEntrancePortalComponent.IsHoldingBall)
        {
            if (_pendingPortalDeactivateRoutine == null)
            {
                _pendingPortalDeactivateRoutine =
                    StartCoroutine(DeactivatePortalsAfterHold());
            }
            return;
        }

        ApplyPowerSurgePortalsActive(false);
    }

    private IEnumerator DeactivatePortalsAfterHold()
    {
        while (_powerSurgeEntrancePortalComponent != null
               && _powerSurgeEntrancePortalComponent.IsHoldingBall)
        {
            yield return null;
        }

        _pendingPortalDeactivateRoutine = null;

        // Only tear down if Power Surge is still meant to be closed — the targets may
        // have gone back down while we waited, re-opening the portals.
        if (!_wasAllDown)
        {
            ApplyPowerSurgePortalsActive(false);
        }
    }

    private void ApplyPowerSurgePortalsActive(bool active)
    {
        if (powerSurgePortalEntrance != null)
            powerSurgePortalEntrance.SetActive(active);

        if (powerSurgePortalExit != null)
            powerSurgePortalExit.SetActive(active);
    }

    // ── Reference resolution ──────────────────────────────────

    private void EnsureRefs()
    {
        if (powerSurgeManager == null)        
        {
            powerSurgeManager = ServiceLocator.Get<PowerSurgeManager>();
        }

        if (scoreManager == null)
        {
            scoreManager = ServiceLocator.Get<ScoreManager>();
        }

        if (bonusSpawnPosition == null)
        {
            bonusSpawnPosition = transform;
        }
    }
}
