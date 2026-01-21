using System.Collections.Generic;
using UnityEngine;

public class GameRulesManager : MonoBehaviour
{
    [Header("Scoring")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ScoreTallyAnimator scoreTallyAnimator;
    [SerializeField] private List<float> goalByRound = new List<float> { 500f, 800f, 1200f, 1700f, 2300f, 3000f, 4000f };
    [SerializeField] private float pointsPerCoin = 100f;

    [Header("Balls / Rounds")]
    [SerializeField] private int startingMaxBalls = 3;
    [SerializeField] private bool autoStartOnPlay = true;

    [Header("Ball Spawning (optional)")]
    [Tooltip("Preferred: assigns BallSpawner which pre-spawns a hand of balls and lerps the next ball to spawnPoint.")]
    [SerializeField] private BallSpawner ballSpawner;
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool enforceSingleActiveBall = true;

    [Header("Ball Loadout (hand)")]
    [Tooltip("Optional: starting ball prefabs for the player's hand/loadout. If empty, falls back to repeating Ball Prefab.")]
    [SerializeField] private List<GameObject> startingBallLoadout = new List<GameObject>();

    [Header("UI (optional)")]
    [SerializeField] private GameObject shopCanvasRoot;
    [SerializeField] private GameObject roundFailedUIRoot;

    [Header("Debug (read-only at runtime)")]
    [SerializeField] private int roundIndex;
    [SerializeField] private int maxBalls;
    [SerializeField] private int ballsRemaining;
    [SerializeField] private int coins;
    [SerializeField] private float roundTotal;

    private bool runActive;
    private bool shopOpen;
    private GameObject activeBall;
    private bool _drainProcessing;

    // Which prefabs will be used for the next round's hand (size == maxBalls).
    private readonly List<GameObject> _ballLoadout = new List<GameObject>();
    private int _nextBallSpawnIndex;

    public int RoundIndex => roundIndex;
    public int MaxBalls => maxBalls;
    public int BallsRemaining => ballsRemaining;
    public int Coins => coins;
    public float RoundTotal => roundTotal;
    public float CurrentGoal => GetGoalForRound(roundIndex);
    public int BallLoadoutCount => _ballLoadout.Count;
    public GameObject ActiveBall => ballSpawner != null ? ballSpawner.ActiveBall : activeBall;

    /// <summary>
    /// Returns a snapshot copy of the current ball loadout (one prefab per hand slot).
    /// Safe to enumerate without risking external mutation.
    /// </summary>
    public List<GameObject> GetBallLoadoutSnapshot()
    {
        EnsureLoadoutWithinCapacity();
        return new List<GameObject>(_ballLoadout);
    }

    private void Awake()
    {
        if (goalByRound == null || goalByRound.Count == 0)
        {
            goalByRound = new List<float> { 500f, 800f, 1200f, 1700f, 2300f, 3000f, 4000f };
        }
    }

    private void Start()
    {
        if (autoStartOnPlay)
        {
            StartRun();
        }
    }

    public void StartRun()
    {
        runActive = true;
        roundIndex = 0;
        coins = 0;
        maxBalls = Mathf.Max(1, startingMaxBalls);
        InitializeLoadoutForNewRun();
        StartRound();
    }

    public void StartRound()
    {
        shopOpen = false;
        SetShopOpen(false);
        SetRoundFailedOpen(false);

        roundTotal = 0f;
        EnsureLoadoutWithinCapacity();
        ballsRemaining = _ballLoadout.Count;
        _nextBallSpawnIndex = 0;

        if (scoreManager != null)
        {
            // Keep the ScoreManager's UI and state aligned with the rules state for the new round.
            scoreManager.ResetForNewRound();
            scoreManager.SetRoundIndex(roundIndex);
            scoreManager.SetGoal(CurrentGoal);
            scoreManager.SetBallsRemaining(ballsRemaining);
            scoreManager.SetCoins(coins);
        }

        if (ballSpawner != null)
        {
            ballSpawner.ClearAll();
            ballSpawner.BuildHandFromPrefabs(_ballLoadout);
        }

        if (ballsRemaining > 0)
        {
            SpawnBall();
        }
    }

    public void OnBallDrained(GameObject ball)
    {
        if (_drainProcessing)
        {
            DespawnBall(ball);
            return;
        }

        StartCoroutine(OnBallDrainedRoutine(ball));
    }

    private System.Collections.IEnumerator OnBallDrainedRoutine(GameObject ball)
    {
        _drainProcessing = true;

        if (!runActive || shopOpen)
        {
            DespawnBall(ball);
            _drainProcessing = false;
            yield break;
        }

        DespawnBall(ball);

        // Play animated tally if configured; otherwise instant-bank.
        if (scoreTallyAnimator != null && scoreManager != null)
        {
            yield return scoreTallyAnimator.PlayTally(scoreManager);
            roundTotal = scoreManager.roundTotal;
        }
        else
        {
            BankCurrentBallIntoRoundTotal();
        }

        ballsRemaining = Mathf.Max(0, ballsRemaining - 1);
        if (scoreManager != null)
        {
            scoreManager.SetBallsRemaining(ballsRemaining);
        }

        if (roundTotal >= CurrentGoal)
        {
            AwardCoinsFromRoundTotal();
            OpenShop();
            _drainProcessing = false;
            yield break;
        }

        if (ballsRemaining > 0)
        {
            SpawnBall();
            _drainProcessing = false;
            yield break;
        }

        ShowRoundFailed();
        _drainProcessing = false;
    }

    /// <summary>
    /// Called by the Shop UI when the player is done shopping.
    /// </summary>
    public void OnShopClosed()
    {
        if (!runActive)
        {
            return;
        }

        SetShopOpen(false);
        shopOpen = false;

        roundIndex = Mathf.Max(0, roundIndex + 1);
        StartRound();
    }

    /// <summary>
    /// Allows a shop purchase to increase balls for future rounds.
    /// </summary>
    public void AddMaxBalls(int delta)
    {
        maxBalls = Mathf.Max(1, maxBalls + delta);
        ballsRemaining = Mathf.Max(0, Mathf.Min(ballsRemaining, _ballLoadout.Count));
        EnsureLoadoutWithinCapacity();
        if (scoreManager != null)
        {
            scoreManager.SetBallsRemaining(ballsRemaining);
        }
    }

    /// <summary>
    /// Adds a new ball prefab into the loadout if there is an open slot (i.e. loadoutCount &lt; MaxBalls).
    /// Returns true if added.
    /// </summary>
    public bool AddBallToLoadout(GameObject prefab)
    {
        if (prefab == null) return false;
        EnsureLoadoutWithinCapacity();
        if (_ballLoadout.Count >= maxBalls)
        {
            return false;
        }
        _ballLoadout.Add(prefab);
        return true;
    }

    /// <summary>
    /// Replaces a ball in the loadout at <paramref name="slotIndex"/> with <paramref name="newPrefab"/>.
    /// Returns true if successful.
    /// </summary>
    public bool ReplaceBallInLoadout(int slotIndex, GameObject newPrefab)
    {
        if (newPrefab == null) return false;
        EnsureLoadoutWithinCapacity();
        if (slotIndex < 0 || slotIndex >= _ballLoadout.Count) return false;
        _ballLoadout[slotIndex] = newPrefab;
        return true;
    }

    /// <summary>
    /// Allows a shop purchase to spend coins.
    /// </summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (coins < amount)
        {
            return false;
        }

        coins -= amount;
        if (scoreManager != null)
        {
            scoreManager.SetCoins(coins);
        }
        return true;
    }

    public void RetryRound()
    {
        if (!runActive)
        {
            return;
        }

        StartRound();
    }

    private float GetGoalForRound(int index)
    {
        if (goalByRound == null || goalByRound.Count == 0)
        {
            return 0f;
        }

        if (index < 0)
        {
            index = 0;
        }

        if (index >= goalByRound.Count)
        {
            return goalByRound[goalByRound.Count - 1];
        }

        return goalByRound[index];
    }

    private float BankCurrentBallIntoRoundTotal()
    {
        if (scoreManager == null)
        {
            return 0f;
        }

        // Prefer ScoreManager API so its internal roundTotal + TMP labels stay in sync.
        float banked = scoreManager.BankCurrentBallScore();
        roundTotal = scoreManager.roundTotal;
        return banked;
    }

    private void ResetCurrentBallScoreVisuals()
    {
        if (scoreManager == null)
        {
            return;
        }

        // Legacy fallback: prefer BankCurrentBallScore/ResetForNewRound, but keep this safe no-op reset.
        scoreManager.AddPoints(-scoreManager.points);
        scoreManager.AddMult(1f - scoreManager.mult);
    }

    private void AwardCoinsFromRoundTotal()
    {
        if (pointsPerCoin <= 0f)
        {
            return;
        }

        int award = Mathf.FloorToInt(roundTotal / pointsPerCoin);
        if (award > 0)
        {
            coins += award;
            if (scoreManager != null)
            {
                scoreManager.SetCoins(coins);
            }
        }
    }

    private void OpenShop()
    {
        shopOpen = true;
        ClearAllBalls();
        SetShopOpen(true);
    }

    private void ShowRoundFailed()
    {
        ClearAllBalls();
        SetRoundFailedOpen(true);
    }

    private void SetShopOpen(bool open)
    {
        if (shopCanvasRoot != null)
        {
            shopCanvasRoot.SetActive(open);
        }
    }

    private void SetRoundFailedOpen(bool open)
    {
        if (roundFailedUIRoot != null)
        {
            roundFailedUIRoot.SetActive(open);
        }
    }

    private GameObject SpawnBall()
    {
        if (ballSpawner != null)
        {
            activeBall = ballSpawner.ActivateNextBall();
            return activeBall;
        }

        if (spawnPoint == null)
        {
            return null;
        }

        // Prefer loadout-based prefabs if configured.
        GameObject prefabToSpawn = ballPrefab;
        if (_ballLoadout.Count > 0)
        {
            int idx = Mathf.Clamp(_nextBallSpawnIndex, 0, _ballLoadout.Count - 1);
            prefabToSpawn = _ballLoadout[idx] != null ? _ballLoadout[idx] : prefabToSpawn;
        }
        _nextBallSpawnIndex++;

        if (prefabToSpawn == null)
        {
            return null;
        }

        if (enforceSingleActiveBall && activeBall != null)
        {
            Destroy(activeBall);
            activeBall = null;
        }

        activeBall = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
        return activeBall;
    }

    private void DespawnBall(GameObject ball)
    {
        if (ball == null)
        {
            return;
        }

        if (ballSpawner != null)
        {
            ballSpawner.DespawnBall(ball);
            if (ball == activeBall)
            {
                activeBall = null;
            }
            return;
        }

        if (ball == activeBall)
        {
            activeBall = null;
        }

        Destroy(ball);
    }

    private void ClearAllBalls()
    {
        if (ballSpawner != null)
        {
            ballSpawner.ClearAll();
        }

        if (activeBall != null)
        {
            Destroy(activeBall);
            activeBall = null;
        }
    }

    private void EnsureLoadoutWithinCapacity()
    {
        int cap = Mathf.Max(1, maxBalls);

        // Remove nulls (treat them as "no ball in inventory").
        for (int i = _ballLoadout.Count - 1; i >= 0; i--)
        {
            if (_ballLoadout[i] == null)
            {
                _ballLoadout.RemoveAt(i);
            }
        }

        // Trim to capacity.
        while (_ballLoadout.Count > cap)
        {
            _ballLoadout.RemoveAt(_ballLoadout.Count - 1);
        }
    }

    private void InitializeLoadoutForNewRun()
    {
        _ballLoadout.Clear();

        int cap = Mathf.Max(1, maxBalls);

        if (startingBallLoadout != null && startingBallLoadout.Count > 0)
        {
            for (int i = 0; i < startingBallLoadout.Count && _ballLoadout.Count < cap; i++)
            {
                if (startingBallLoadout[i] != null)
                {
                    _ballLoadout.Add(startingBallLoadout[i]);
                }
            }
        }

        // If the starting list doesn't fill the hand, fill the remainder with the fallback prefab (if any),
        // so you start with a full hand by default.
        while (_ballLoadout.Count < cap && ballPrefab != null)
        {
            _ballLoadout.Add(ballPrefab);
        }

        EnsureLoadoutWithinCapacity();
    }
}

