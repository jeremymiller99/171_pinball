// Generated with Antigravity by jjmil on 2026-04-09.
// Drop‑target frenzy: bumper split‑open, portal reveal, multiplier doubling.
// Frenzy-gate SFX hook added by Claude Code (Opus 4.7) for jjmil on 2026-04-21.
// Updated by Claude (Opus 4.8), for jjmil, on 2026-06-04 (defer portal teardown while the entrance
// portal is holding a ball in its teleport delay, so the held ball isn't stranded).
// Updated by Claude (Opus 4.8), for jjmil, on 2026-06-05 (deactivate frenzy directly when the gate
// closes, so a portal-started frenzy ends on target-return even while the countdown is paused).
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scoring mode tied to 3 drop targets. When all 3 are down, 4 bumpers
/// split open on the X‑axis to reveal a frenzy portal behind them.
/// Entering the portal doubles the current multiplier. Frenzy ends when
/// any drop target resets back up, at which point bumpers close and the
/// multiplier bonus is removed.
/// </summary>
public class DropTargetsScoringMode : MonoBehaviour
{

    /// <summary>Fired when all 3 drop targets become down.</summary>
    public event Action OnAllTargetsDown;

    /// <summary>Fired when any target returns up.</summary>
    public event Action OnAnyTargetReturned;

    [Header("Drop Targets")]
    [SerializeField] private DropTarget[] dropTargets = new DropTarget[3];

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

    [Header("Frenzy Bumper Animation")]
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

    [Header("Frenzy Portals")]
    [Tooltip("The frenzy portal entrance. Hidden until bumpers open.")]
    [SerializeField] private GameObject frenzyPortalEntrance;
    [Tooltip("The frenzy portal exit. Hidden until bumpers open.")]
    [SerializeField] private GameObject frenzyPortalExit;

    [Header("References")]
    [SerializeField] private FrenzyManager frenzyManager;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Frenzy HUD Color")]
    [Tooltip("Color applied to the multiplier HUD meter during frenzy. Should match your frenzy lights.")]
    [SerializeField] private Color frenzyHudColor = new Color(0f, 0.85f, 1f, 1f);

    public Color FrenzyHudColor => frenzyHudColor;

    private bool _allDownBonusAwardedThisCycle;
    private bool _wasAllDown;
    private Coroutine _deferredCheckRoutine;

    // Cached Portal on the entrance so we can tell when a ball is mid-teleport
    // (held inside the delay) and defer tearing the portals down until it exits.
    private Portal _frenzyEntrancePortalComponent;
    private Coroutine _pendingPortalDeactivateRoutine;

    // Bumper animation state
    private Vector3[] _leftClosedPos;
    private Vector3[] _rightClosedPos;
    private Coroutine _bumperAnimRoutine;

    private void Awake()
    {
        EnsureRefs();
        CacheBumperClosedPositions();

        if (frenzyPortalEntrance != null)
        {
            _frenzyEntrancePortalComponent =
                frenzyPortalEntrance.GetComponent<Portal>();
        }

        SetFrenzyPortalsActive(false);
    }

    private void OnEnable()
    {
        if (dropTargets == null) return;

        foreach (DropTarget dt in dropTargets)
        {
            if (dt != null)
            {
                dt.OnFullyDown += OnTargetFullyDown;
                dt.OnReturnedUp += OnTargetReturnedUp;
            }
        }

        _allDownBonusAwardedThisCycle = false;

        RefreshDropTargetBulbVisuals();
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

        if (dropTargets != null)
        {
            foreach (DropTarget dt in dropTargets)
            {
                if (dt != null)
                {
                    dt.OnFullyDown -= OnTargetFullyDown;
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

    private void OnTargetReturnedUp()
    {
        if (_deferredCheckRoutine != null)
        {
            StopCoroutine(_deferredCheckRoutine);
            _deferredCheckRoutine = null;
        }

        if (_wasAllDown)
        {
            CloseFrenzyGate();
        }

        _allDownBonusAwardedThisCycle = false;
    }

    private IEnumerator DeferredCheckAllDown()
    {
        yield return null;

        _deferredCheckRoutine = null;

        bool allDown = AllTargetsDown();

        if (allDown)
        {
            if (!_wasAllDown)
            {
                _wasAllDown = true;
                AnimateBumpers(true);
                SetFrenzyPortalsActive(true);
                ServiceLocator.Get<AudioManager>()?.PlayFrenzyGate(
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
                CloseFrenzyGate();
            }

            _allDownBonusAwardedThisCycle = false;
        }

        RefreshDropTargetBulbVisuals();
    }

    // Closes the frenzy gate after all-down ends: retracts the bumpers, hides
    // the portals (deferred if a ball is mid-teleport), ends the frenzy
    // multiplier, and plays the gate SFX. DeactivateFrenzy is called directly
    // (not via the FrenzyManager countdown) so a portal-started frenzy ends the
    // instant a target returns up — even while the countdown is paused because a
    // ball is held inside the portal's teleport delay.
    private void CloseFrenzyGate()
    {
        _wasAllDown = false;
        AnimateBumpers(false);
        SetFrenzyPortalsActive(false);

        EnsureRefs();
        frenzyManager?.DeactivateFrenzy();

        ServiceLocator.Get<AudioManager>()?.PlayFrenzyGate(
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

        foreach (DropTarget dt in dropTargets)
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

    private void SetFrenzyPortalsActive(bool active)
    {
        if (active)
        {
            // Re-opening: cancel any deferred teardown that hadn't fired yet.
            if (_pendingPortalDeactivateRoutine != null)
            {
                StopCoroutine(_pendingPortalDeactivateRoutine);
                _pendingPortalDeactivateRoutine = null;
            }

            ApplyFrenzyPortalsActive(true);
            return;
        }

        // Deactivating: if a ball is currently held inside the entrance portal's
        // teleport delay, removing the portals now would strand it (the delay
        // coroutine dies with the entrance and the exit it needs disappears).
        // Defer teardown until the ball has exited.
        if (_frenzyEntrancePortalComponent != null
            && _frenzyEntrancePortalComponent.IsHoldingBall)
        {
            if (_pendingPortalDeactivateRoutine == null)
            {
                _pendingPortalDeactivateRoutine =
                    StartCoroutine(DeactivatePortalsAfterHold());
            }
            return;
        }

        ApplyFrenzyPortalsActive(false);
    }

    private IEnumerator DeactivatePortalsAfterHold()
    {
        while (_frenzyEntrancePortalComponent != null
               && _frenzyEntrancePortalComponent.IsHoldingBall)
        {
            yield return null;
        }

        _pendingPortalDeactivateRoutine = null;

        // Only tear down if frenzy is still meant to be closed — the targets may
        // have gone back down while we waited, re-opening the portals.
        if (!_wasAllDown)
        {
            ApplyFrenzyPortalsActive(false);
        }
    }

    private void ApplyFrenzyPortalsActive(bool active)
    {
        if (frenzyPortalEntrance != null)
            frenzyPortalEntrance.SetActive(active);

        if (frenzyPortalExit != null)
            frenzyPortalExit.SetActive(active);
    }

    // ── Reference resolution ──────────────────────────────────

    private void EnsureRefs()
    {
        if (frenzyManager == null)        
        {
            frenzyManager = ServiceLocator.Get<FrenzyManager>();
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
