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

    public StartType ActiveStartType => startType;
    public int Seed => seed;
    public IReadOnlyList<BoardDefinition> Boards => activeRunPlan?.boards;
    public int CurrentBoardIndex => currentBoardIndex;

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
        if (next < 0 || next >= activeRunPlan.boards.Count) return null;
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

    public void ResetSession()
    {
        startType = StartType.None;
        seed = 0;
        activeRunPlan = new RunPlan();
        currentBoardIndex = 0;
    }
}

