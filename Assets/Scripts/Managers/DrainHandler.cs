// Updated with Antigravity by jjmil on 2026-04-07 (removed bankMultiplier).
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

    private bool _drainProcessing;

    public bool IsDrainProcessing => _drainProcessing;

    public event Action DrainBankCompleted;
    public event Action<bool> PostDrainResolved;

    private void Awake()
    {
        ServiceLocator.Register<DrainHandler>(this);
        ResolveServices();
    }

    private void OnDisable()
    {
        ServiceLocator.Unregister<DrainHandler>();
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
    }

    public void OnBallDrained(GameObject ball)
    {
        OnBallDrained(ball, false);
    }

    public void OnBallDrained(
        GameObject ball, bool showHomeRunPopup)
    {
        ResolveServices();

        if (_drainProcessing)
        {
            DespawnBall(ball);
            return;
        }

        StartCoroutine(
            OnBallDrainedRoutine(
                ball, showHomeRunPopup));
    }

    private IEnumerator OnBallDrainedRoutine(
        GameObject ball,
        bool showHomeRunPopup)
    {
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

        AmpUpBall ampUpBall =
            ball != null ? ball.GetComponent<AmpUpBall>() : null;
        float ampUpFlatMultDelta =
            ampUpBall != null ? ampUpBall.FlatMultBonusForBallBehind : 0f;

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

        rules.SyncRoundTotal(roundTotal);
        ProfileService.AddBankedPoints(bankedPoints);

        DrainBankCompleted?.Invoke();

        var loadout = ServiceLocator.Get<BallLoadoutController>();
        if (ampUpBall != null)
        {
            loadout?.TryApplyAmpUpBonusBehindDrainedSlot(
                slotHint,
                ampUpFlatMultDelta);
        }

        loadout?.ConsumeActiveBallFromLoadout(slotHint);
        rules.RefreshBallsRemaining();

        if (rules.BallsRemaining > 0)
        {
            GameObject nextBall = SpawnBall();
            if (nextBall != null)
            {
                _drainProcessing = false;
                // Reconcile any level-ups that ScoreChanged events tried to trigger
                // while _drainProcessing was true (e.g. frenzy scoring right before drain).
                rules.ForceReconcileLevelUps();
                yield break;
            }
        }

        rules.ShowRoundFailed();
        _drainProcessing = false;
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
        if (ball != null && ballSpawner != null) ballSpawner.DespawnBall(ball);
    }
}
