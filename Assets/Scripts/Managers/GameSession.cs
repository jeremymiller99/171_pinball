using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent session data that survives scene loads.
/// Stores the player's start selection (quick run vs challenge) and the active multi-board run plan.
/// </summary>
public sealed class GameSession : MonoBehaviour
{
    public enum StartType
    {
        None = 0,
        QuickRun = 1,
        Challenge = 2
    }

    [Serializable]
    public sealed class RunPlan
    {
        public List<BoardDefinition> boards = new List<BoardDefinition>();
    }

    public static GameSession Instance { get; private set; }

    [Header("Runtime (debug)")]
    [SerializeField] private StartType startType = StartType.None;
    [SerializeField] private int seed;

    [SerializeField] private RunPlan activeRunPlan = new RunPlan();
    [SerializeField] private int currentBoardIndex;

    [Header("Round Modifiers (debug)")]
    [SerializeField] private List<RoundData> generatedRounds = new List<RoundData>();
    [SerializeField] private ChallengeModeDefinition activeChallenge;
    [SerializeField] private PlayerShipDefinition activeShip;

    public StartType ActiveStartType => startType;
    public int Seed => seed;
    public IReadOnlyList<BoardDefinition> Boards => activeRunPlan?.boards;
    public int CurrentBoardIndex => currentBoardIndex;

    /// <summary>
    /// The generated round data for the current run.
    /// </summary>
    public IReadOnlyList<RoundData> GeneratedRounds => generatedRounds;

    /// <summary>
    /// The active challenge mode definition, if any.
    /// </summary>
    public ChallengeModeDefinition ActiveChallenge => activeChallenge;

    /// <summary>
    /// The active player ship definition chosen at start, if any.
    /// </summary>
    public PlayerShipDefinition ActiveShip => activeShip;

    /// <summary>
    /// Ensures a session exists even if you didn't create a Bootstrap scene yet.
    /// Safe to call from any scene.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureSessionExists()
    {
        if (Instance != null) return;

        var go = new GameObject(nameof(GameSession));
        DontDestroyOnLoad(go);
        go.AddComponent<GameSession>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ConfigureQuickRun(IList<BoardDefinition> boardsForRun, int runSeed)
    {
        startType = StartType.QuickRun;
        seed = runSeed;
        activeRunPlan = new RunPlan();
        if (boardsForRun != null)
        {
            activeRunPlan.boards.AddRange(boardsForRun);
        }
        currentBoardIndex = 0;
    }

    public void ConfigureChallenge(IList<BoardDefinition> boardsForChallenge, int runSeed)
    {
        startType = StartType.Challenge;
        seed = runSeed;
        activeRunPlan = new RunPlan();
        if (boardsForChallenge != null)
        {
            activeRunPlan.boards.AddRange(boardsForChallenge);
        }
        currentBoardIndex = 0;
        activeChallenge = null;
        generatedRounds.Clear();
    }

    public void ConfigureChallenge(ChallengeModeDefinition challenge, PlayerShipDefinition ship, int runSeed)
    {
        startType = StartType.Challenge;
        seed = runSeed;
        activeRunPlan = new RunPlan();
        activeChallenge = challenge;
        activeShip = ship;
        if (challenge != null && challenge.boards != null)
        {
            activeRunPlan.boards.AddRange(challenge.boards);
        }
        currentBoardIndex = 0;
        generatedRounds.Clear();
    }

    public void ConfigureChallenge(BoardDefinition singleBoardChallenge, int runSeed)
    {
        if (singleBoardChallenge == null)
        {
            ConfigureChallenge((IList<BoardDefinition>)null, runSeed);
            return;
        }

        ConfigureChallenge(new[] { singleBoardChallenge }, runSeed);
    }

    public BoardDefinition GetCurrentBoard()
    {
        if (activeRunPlan?.boards == null) return null;
        if (currentBoardIndex < 0 || currentBoardIndex >= activeRunPlan.boards.Count) return null;
        return activeRunPlan.boards[currentBoardIndex];
    }

    public BoardDefinition GetNextBoard()
    {
        if (activeRunPlan?.boards == null) return null;
        int next = currentBoardIndex + 1;

        if (next < 0
            || next >= activeRunPlan.boards.Count)
        {
            return null;
        }

        return activeRunPlan.boards[next];
    }

    public bool AdvanceToNextBoard()
    {
        if (activeRunPlan?.boards == null) return false;
        int next = currentBoardIndex + 1;
        if (next < 0 || next >= activeRunPlan.boards.Count) return false;
        currentBoardIndex = next;
        return true;
    }

    /// <summary>
    /// Generates round data for the run using the active challenge's settings.
    /// Call this after ConfigureChallenge and before starting the run.
    /// </summary>
    /// <param name="totalRounds">The total number of rounds to generate.</param>
    public void GenerateRounds(int totalRounds)
    {
        generatedRounds.Clear();

        if (totalRounds <= 0)
        {
            return;
        }

        var rng = new System.Random(seed);

        // Every 5 levels (index 4, 9, 14...) is a Devil round.
        for (int i = 0; i < totalRounds; i++)
        {
            RoundType type = RoundType.Normal;
            RoundModifierDefinition modifier = null;

            // deterministic devil every 5 levels
            if (i > 0 && (i + 1) % 5 == 0)
            {
                type = RoundType.Devil;
                if (activeChallenge != null && activeChallenge.devilPool != null)
                {
                    modifier = activeChallenge.devilPool.GetRandomModifier(rng);
                }
            }

            generatedRounds.Add(new RoundData(i, type, modifier));
        }
    }

    /// <summary>
    /// Gets the RoundData for a specific round index.
    /// Returns null if the index is out of range or rounds haven't been generated.
    /// </summary>
    public RoundData GetRoundData(int roundIndex)
    {
        if (generatedRounds == null || roundIndex < 0 || roundIndex >= generatedRounds.Count)
        {
            return null;
        }
        return generatedRounds[roundIndex];
    }

    /// <summary>
    /// Returns true if rounds have been generated for this session.
    /// </summary>
    public bool HasGeneratedRounds => generatedRounds != null && generatedRounds.Count > 0;

    public void ResetSession()
    {
        startType = StartType.None;
        seed = 0;
        activeRunPlan = new RunPlan();
        currentBoardIndex = 0;
        activeChallenge = null;
        activeShip = null;
        generatedRounds.Clear();
    }
}
