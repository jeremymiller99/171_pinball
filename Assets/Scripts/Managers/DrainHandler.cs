// Generated with Cursor (Composer) by assistant on 2026-03-31.
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
        OnBallDrained(ball, 1f, false);
    }

    public void OnBallDrained(GameObject ball, float bankMultiplier, bool showHomeRunPopup)
    {
        ResolveServices();

        if (_drainProcessing)
        {
            DespawnBall(ball);
            return;
        }

        StartCoroutine(OnBallDrainedRoutine(ball, bankMultiplier, showHomeRunPopup));
    }

    private IEnumerator OnBallDrainedRoutine(
        GameObject ball,
        float bankMultiplier,
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

        double bankedPoints = 0d;
        if (scoreManager != null)
        {
            float m = bankMultiplier <= 0f ? 1f : bankMultiplier;
            bankedPoints = scoreManager.Points * scoreManager.Mult * m;
        }

        Vector3 drainedBallWorldPos = ball != null ? ball.transform.position : Vector3.zero;
        DespawnBall(ball);

        if (showHomeRunPopup)
        {
            rules.ShowHomeRunPopup();
        }

        float roundTotal;
        if (scoreTallyAnimator != null && scoreManager != null)
        {
            yield return scoreTallyAnimator.PlayTally(
                scoreManager, bankMultiplier, drainedBallWorldPos);
            roundTotal = scoreManager.RoundTotal;
        }
        else
        {
            BankCurrentBallIntoRoundTotal(bankMultiplier);
            roundTotal = scoreManager != null ? scoreManager.RoundTotal : 0f;
            bankedPoints = roundTotal;
        }

        rules.SyncRoundTotal(roundTotal);
        ProfileService.AddBankedPoints(bankedPoints);

        DrainBankCompleted?.Invoke();

        bool shopBallSaveAvailable = rules.ConsumeShopBallSave();

        if (shopBallSaveAvailable)
        {
            rules.RefreshBallsRemaining();

            if (rules.CheckAndCompleteRun())
            {
                _drainProcessing = false;
                yield break;
            }

            rules.OpenShop();
            _drainProcessing = false;
            yield break;
        }

        var loadout = ServiceLocator.Get<BallLoadoutController>();
        loadout?.ConsumeActiveBallFromLoadout(slotHint);
        rules.RefreshBallsRemaining();

        if (rules.BallsRemaining > 0)
        {
            GameObject nextBall = SpawnBall();
            if (nextBall != null)
            {
                _drainProcessing = false;
                yield break;
            }
        }

        rules.ShowRoundFailed();
        _drainProcessing = false;
    }

    private float BankCurrentBallIntoRoundTotal(float bankMultiplier = 1f)
    {
        if (scoreManager == null) return 0f;
        return scoreManager.BankCurrentBallScore(bankMultiplier);
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
