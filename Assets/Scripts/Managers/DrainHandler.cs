// Updated with Antigravity by jjmil on 2026-04-07 (removed bankMultiplier).
// Updated with Claude Code (claude-opus-4-7) by jjmil on 2026-04-20 (removed AmpUpBall drain hook).
// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-05 (ball save window).
// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-05 (IsBallSaveArmed for board light).
// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-05 (ball saved VFX pop).
// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-05 (save window 15s -> 12s).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles ball drain events: tally animation, banking score, consuming loadout,
/// spawning the next ball, or signalling shop/fail. Extracted from GameRulesManager.
/// </summary>
[DisallowMultipleComponent]
public class DrainHandler : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ScoreTallyAnimator scoreTallyAnimator;
    [SerializeField] private BallSpawner ballSpawner;

    [Header("Ball Save")]
    [Tooltip("A ball drained this many seconds or less after it was launched is returned to " +
             "the launcher instead of the next ball in the hand.")]
    [SerializeField] private float ballSaveSeconds = 12f;

    private bool _drainProcessing;

    private readonly Dictionary<int, float> _launchTimeByBallId = new Dictionary<int, float>();

    public bool goingToShop;

    public bool IsDrainProcessing => _drainProcessing;

    public event Action DrainBankCompleted;
    public event Action<bool> PostDrainResolved;

    private void Awake()
    {
        ServiceLocator.Register<DrainHandler>(this);
        PinballLauncher.BallLaunched += OnBallLaunched;
        ResolveServices();
    }

    private void OnDisable()
    {
        ServiceLocator.Unregister<DrainHandler>();
        PinballLauncher.BallLaunched -= OnBallLaunched;
    }

    private void OnBallLaunched(GameObject launched)
    {
        if (launched == null)
        {
            return;
        }

        _launchTimeByBallId[launched.GetInstanceID()] = Time.time;
    }

    /// <summary>
    /// True while losing the ball right now would return it to the launcher: exactly one ball is
    /// in play and it is still inside its save window. Multiball is excluded because a drain with
    /// other balls still out never reaches the save at all — it just despawns.
    /// Drives the board's ball save light.
    /// </summary>
    public bool IsBallSaveArmed
    {
        get
        {
            if (ballSpawner == null)
            {
                ballSpawner = ServiceLocator.Get<BallSpawner>();
            }

            List<GameObject> activeBalls = ballSpawner != null ? ballSpawner.ActiveBalls : null;
            if (activeBalls == null || activeBalls.Count != 1)
            {
                return false;
            }

            return IsWithinBallSaveWindow(activeBalls[0]);
        }
    }

    /// <summary>
    /// True when this ball was launched from a plunger and drained inside the save window.
    /// Balls that never passed through a launcher are never savable.
    /// </summary>
    private bool IsWithinBallSaveWindow(GameObject ball)
    {
        if (ball == null)
        {
            return false;
        }

        if (!_launchTimeByBallId.TryGetValue(ball.GetInstanceID(), out float launchTime))
        {
            return false;
        }

        return Time.time - launchTime <= ballSaveSeconds;
    }

    private void ResolveServices()
    {
        if (scoreManager == null) scoreManager = ServiceLocator.Get<ScoreManager>();
        if (scoreTallyAnimator == null) scoreTallyAnimator = ServiceLocator.Get<ScoreTallyAnimator>();
        if (ballSpawner == null) ballSpawner = ServiceLocator.Get<BallSpawner>();
    }

    public void ResetState()
    {
        _drainProcessing = false;
        _launchTimeByBallId.Clear();
    }

    public void OnBallDrained(GameObject ball)
    {
        OnBallDrained(ball, false, false);
    }

    public void OnBallDrained(
        GameObject ball, bool showHomeRunPopup)
    {
        OnBallDrained(ball, showHomeRunPopup, false);
    }

    /// <summary>
    /// <paramref name="eligibleForBallSave"/> should only be true for a genuine loss of the ball
    /// (the drain zones). Balls that route through here because they consumed themselves by
    /// design — Molotov breaking, Holoball expiring — must not be handed back.
    /// </summary>
    public void OnBallDrained(
        GameObject ball, bool showHomeRunPopup, bool eligibleForBallSave)
    {
        ResolveServices();

        if (_drainProcessing)
        {
            DespawnBall(ball);
            return;
        }

        StartCoroutine(
            OnBallDrainedRoutine(
                ball, showHomeRunPopup, eligibleForBallSave));
    }

    private IEnumerator OnBallDrainedRoutine(
        GameObject ball,
        bool showHomeRunPopup,
        bool eligibleForBallSave)
    {
        if (goingToShop == true)
        {
            goingToShop = false;
        }

        List<GameObject> activeBalls = ballSpawner != null ? ballSpawner.ActiveBalls : null;

        if (activeBalls != null && activeBalls.Count > 1)
        {
            DespawnBall(ball);
            yield break;
        }

        _drainProcessing = true;

        var rules = ServiceLocator.Get<GameRulesManager>();
        if (rules == null || !rules.RunActive || rules.IsShopOpen)
        {
            DespawnBall(ball);
            _drainProcessing = false;
            yield break;
        }

        int slotHint = -1;
        if (ball != null)
        {
            var marker = ball.GetComponent<BallHandSlotMarker>();
            if (marker != null) slotHint = marker.SlotIndex;
        }

        // Read before the despawn below: it drops this ball's launch timestamp.
        bool ballSaved = eligibleForBallSave && IsWithinBallSaveWindow(ball);

        double bankedPoints = 0d;
        if (scoreManager != null)
        {
            bankedPoints = scoreManager.Points;
        }

        Vector3 drainedBallWorldPos = ball != null ? ball.transform.position : Vector3.zero;
        DespawnBall(ball);

        if (showHomeRunPopup)
        {
            rules.ShowHomeRunPopup();
        }

        double roundTotal;
        if (scoreTallyAnimator != null
            && scoreManager != null)
        {
            yield return scoreTallyAnimator.PlayTally(
                scoreManager, drainedBallWorldPos);
            roundTotal = scoreManager.RoundTotal;
        }
        else
        {
            BankCurrentBallIntoRoundTotal();
            roundTotal = scoreManager != null
                ? scoreManager.RoundTotal : 0d;
            bankedPoints = roundTotal;
        }

        // Ball lost: cancel Power Surge and decay the board mult toward 1x. Sits after the
        // bank (so the drained ball still scores at its full mult) and before the
        // shop bail-out, so a drain that ends the round decays as well.
        scoreManager?.DecayMultiplier();

        if (goingToShop == true)
        {
            goingToShop = false;
            yield break;
        }

        rules.SyncRoundTotal(roundTotal);
        ProfileService.AddBankedPoints(bankedPoints);

        DrainBankCompleted?.Invoke();

        var loadout = ServiceLocator.Get<BallLoadoutController>();
        bool ballReturned = ballSaved && TryReturnSavedBallToHand(loadout, slotHint);
        if (ballReturned)
        {
            // Popped here, not where the save is first decided: the routine can still bail to
            // the shop after the tally, and the effect must not fire for a save that never lands.
            SpawnBallSaveVfx();
        }
        else
        {
            loadout?.ConsumeActiveBallFromLoadout(slotHint);
        }

        rules.RefreshBallsRemaining();

        if (rules.BallsRemaining > 0)
        {
            GameObject nextBall = SpawnBall();
            if (nextBall != null)
            {
                _drainProcessing = false;
                // Reconcile any level-ups that ScoreChanged events tried to trigger
                // while _drainProcessing was true (e.g. Power Surge scoring right before drain).
                rules.ForceReconcileLevelUps();
                yield break;
            }
        }

        rules.ShowRoundFailed();
        _drainProcessing = false;
    }

    /// <summary>
    /// Pops the ball saved VFX at the center of the board's VFX volume — the same place the
    /// level-up banner and the Power Surge VFX appear, rather than down at the ball save lamp
    /// where it read as a speck. The volume and the VFX list live in the board scene, so a
    /// board wired with neither simply gets no effect.
    /// </summary>
    private void SpawnBallSaveVfx()
    {
        ServiceLocator.Get<LevelUpVFXTrigger>()?.SpawnBallSaveVFX();
    }

    /// <summary>
    /// Ball save: leaves the loadout (and so the ball count) untouched and puts a fresh copy of
    /// the drained ball at the front of the hand, so the spawn below serves that ball again
    /// rather than the one queued behind it. False if it could not be re-served, in which case
    /// the caller consumes the ball as normal.
    /// </summary>
    private bool TryReturnSavedBallToHand(BallLoadoutController loadout, int slotHint)
    {
        if (loadout == null || ballSpawner == null)
        {
            return false;
        }

        List<BallDefinition> loadoutSnapshot = loadout.GetBallLoadoutSnapshot();
        int slotIndex = slotHint >= 0 && slotHint < loadoutSnapshot.Count ? slotHint : 0;
        if (slotIndex >= loadoutSnapshot.Count)
        {
            return false;
        }

        BallDefinition savedDefinition = loadoutSnapshot[slotIndex];
        if (savedDefinition == null || savedDefinition.Prefab == null)
        {
            return false;
        }

        GameObject savedBall = ballSpawner.InsertHandBallAtFront(savedDefinition.Prefab);
        if (savedBall == null)
        {
            return false;
        }

        // Only BuildHandFromPrefabs syncs this, so a saved ball would silently lose its amp.
        Ball ballComponent = savedBall.GetComponent<Ball>();
        if (ballComponent != null)
        {
            ballComponent.SetAmpedUp(loadout.GetAmpedUpForSlot(slotIndex));
        }

        return true;
    }

    private double BankCurrentBallIntoRoundTotal()
    {
        if (scoreManager == null) return 0d;
        return scoreManager.BankCurrentBallScore();
    }

    private GameObject SpawnBall()
    {
        if (ballSpawner == null) return null;
        return ballSpawner.ActivateNextBall();
    }

    private void DespawnBall(GameObject ball)
    {
        if (ball == null) return;

        _launchTimeByBallId.Remove(ball.GetInstanceID());
        if (ballSpawner != null) ballSpawner.DespawnBall(ball);
    }
}
