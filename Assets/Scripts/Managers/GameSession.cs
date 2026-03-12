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

    [Header("Board Component Upgrades (runtime)")]
    [SerializeField] private List<BoardComponentUpgrade> boardComponentUpgrades = new List<BoardComponentUpgrade>();

    private readonly Dictionary<string, BoardComponentUpgrade> _boardComponentUpgradeByKey =
        new Dictionary<string, BoardComponentUpgrade>();
    private bool _boardComponentUpgradeLookupBuilt;

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
        ClearBoardComponentUpgrades();
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
        ClearBoardComponentUpgrades();
    }

    public void ConfigureChallenge(ChallengeModeDefinition challenge, int runSeed)
    {
        startType = StartType.Challenge;
        seed = runSeed;
        activeRunPlan = new RunPlan();
        activeChallenge = challenge;
        if (challenge != null && challenge.boards != null)
        {
            activeRunPlan.boards.AddRange(challenge.boards);
        }
        currentBoardIndex = 0;
        generatedRounds.Clear();
        ClearBoardComponentUpgrades();
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

        // If no challenge or no modifier pools, all rounds are normal
        if (activeChallenge == null || !activeChallenge.HasModifierPools)
        {
            for (int i = 0; i < totalRounds; i++)
            {
                generatedRounds.Add(new RoundData(i, RoundType.Normal, null));
            }
            return;
        }

        // Initialize all rounds as normal first
        var roundTypes = new RoundType[totalRounds];
        for (int i = 0; i < totalRounds; i++)
        {
            roundTypes[i] = RoundType.Normal;
        }

        if (activeChallenge.distributionMode == RoundDistributionMode.Guaranteed)
        {
            // Guaranteed mode: place a fixed number of angel and devil rounds
            AssignGuaranteedRounds(roundTypes, rng);
        }
        else
        {
            // Probability mode: each round has a chance to be angel or devil
            AssignProbabilityRounds(roundTypes, rng);
        }

        // The first level is always a normal round.
        roundTypes[0] = RoundType.Normal;

        // Create RoundData with assigned modifiers
        for (int i = 0; i < totalRounds; i++)
        {
            RoundModifierDefinition modifier = null;

            if (roundTypes[i] == RoundType.Angel && activeChallenge.angelPool != null)
            {
                modifier = activeChallenge.angelPool.GetRandomModifier(rng);
            }
            else if (roundTypes[i] == RoundType.Devil && activeChallenge.devilPool != null)
            {
                modifier = activeChallenge.devilPool.GetRandomModifier(rng);
            }

            generatedRounds.Add(new RoundData(i, roundTypes[i], modifier));
        }
    }

    private void AssignGuaranteedRounds(RoundType[] roundTypes, System.Random rng)
    {
        int totalRounds = roundTypes.Length;
        int availableRounds = Mathf.Max(0, totalRounds - 1);
        int angelsToPlace = Mathf.Min(activeChallenge.guaranteedAngels, availableRounds);
        int devilsToPlace = Mathf.Min(activeChallenge.guaranteedDevils, availableRounds - angelsToPlace);

        // Create list of available indices
        var availableIndices = new List<int>();
        for (int i = 1; i < totalRounds; i++)
        {
            availableIndices.Add(i);
        }

        // Shuffle the indices
        for (int i = availableIndices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int temp = availableIndices[i];
            availableIndices[i] = availableIndices[j];
            availableIndices[j] = temp;
        }

        // Assign angels first
        int indexPos = 0;
        for (int i = 0; i < angelsToPlace && indexPos < availableIndices.Count; i++, indexPos++)
        {
            roundTypes[availableIndices[indexPos]] = RoundType.Angel;
        }

        // Then assign devils
        for (int i = 0; i < devilsToPlace && indexPos < availableIndices.Count; i++, indexPos++)
        {
            roundTypes[availableIndices[indexPos]] = RoundType.Devil;
        }
    }

    private void AssignProbabilityRounds(RoundType[] roundTypes, System.Random rng)
    {
        float angelChance = Mathf.Clamp01(activeChallenge.angelChance);
        float devilChance = Mathf.Clamp01(activeChallenge.devilChance);

        // The first level is always a normal round.
        for (int i = 1; i < roundTypes.Length; i++)
        {
            float roll = (float)rng.NextDouble();

            if (roll < angelChance)
            {
                roundTypes[i] = RoundType.Angel;
            }
            else if (roll < angelChance + devilChance)
            {
                roundTypes[i] = RoundType.Devil;
            }
            // else stays Normal
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
        generatedRounds.Clear();
        ClearBoardComponentUpgrades();
    }

    [Serializable]
    private sealed class BoardComponentUpgrade
    {
        public string boardSceneName;
        public string componentPath;
        public TypeOfScore typeOfScore;
        public float amountToScore;
    }

    public bool HasBoardComponentUpgrade(GameObject target, TypeOfScore typeOfScore)
    {
        if (target == null)
        {
            return false;
        }

        EnsureBoardComponentUpgradeLookup();
        string key = BuildBoardComponentUpgradeKey(target, typeOfScore);
        if (!string.IsNullOrWhiteSpace(key) && _boardComponentUpgradeByKey.ContainsKey(key))
        {
            return true;
        }

        string legacyKey = BuildLegacyBoardComponentUpgradeKey(target, typeOfScore);
        return !string.IsNullOrWhiteSpace(legacyKey) && _boardComponentUpgradeByKey.ContainsKey(legacyKey);
    }

    public void RegisterBoardComponentUpgrade(GameObject target, TypeOfScore typeOfScore, float amountToScore)
    {
        if (target == null)
        {
            return;
        }

        EnsureBoardComponentUpgradeLookup();
        string key = BuildBoardComponentUpgradeKey(target, typeOfScore);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (_boardComponentUpgradeByKey.TryGetValue(key, out BoardComponentUpgrade existing) && existing != null)
        {
            existing.amountToScore = amountToScore;
            return;
        }

        string legacyKey = BuildLegacyBoardComponentUpgradeKey(target, typeOfScore);
        if (!string.IsNullOrWhiteSpace(legacyKey)
            && _boardComponentUpgradeByKey.TryGetValue(legacyKey, out BoardComponentUpgrade legacyExisting)
            && legacyExisting != null)
        {
            legacyExisting.amountToScore = amountToScore;
            legacyExisting.componentPath = GetHierarchyPathWithSiblingIndices(target.transform);
            _boardComponentUpgradeByKey.Remove(legacyKey);
            _boardComponentUpgradeByKey[key] = legacyExisting;
            return;
        }

        var entry = new BoardComponentUpgrade
        {
            boardSceneName = target.scene.name,
            componentPath = GetHierarchyPathWithSiblingIndices(target.transform),
            typeOfScore = typeOfScore,
            amountToScore = amountToScore
        };

        boardComponentUpgrades.Add(entry);
        _boardComponentUpgradeByKey[key] = entry;
    }

    private void ClearBoardComponentUpgrades()
    {
        boardComponentUpgrades.Clear();
        _boardComponentUpgradeByKey.Clear();
        _boardComponentUpgradeLookupBuilt = true;
    }

    private void EnsureBoardComponentUpgradeLookup()
    {
        if (_boardComponentUpgradeLookupBuilt)
        {
            return;
        }

        _boardComponentUpgradeLookupBuilt = true;
        _boardComponentUpgradeByKey.Clear();
        if (boardComponentUpgrades == null || boardComponentUpgrades.Count == 0)
        {
            return;
        }

        for (int i = 0; i < boardComponentUpgrades.Count; i++)
        {
            BoardComponentUpgrade entry = boardComponentUpgrades[i];
            if (entry == null)
            {
                continue;
            }

            string key = BuildBoardComponentUpgradeKey(entry.boardSceneName, entry.componentPath, entry.typeOfScore);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            _boardComponentUpgradeByKey[key] = entry;
        }
    }

    private static string BuildBoardComponentUpgradeKey(GameObject target, TypeOfScore typeOfScore)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string sceneName = target.scene.name;
        string path = GetHierarchyPathWithSiblingIndices(target.transform);
        return BuildBoardComponentUpgradeKey(sceneName, path, typeOfScore);
    }

    private static string BuildBoardComponentUpgradeKey(string sceneName, string componentPath, TypeOfScore typeOfScore)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(componentPath))
        {
            return string.Empty;
        }

        return $"{sceneName}|{componentPath}|{(int)typeOfScore}";
    }

    private static string BuildLegacyBoardComponentUpgradeKey(GameObject target, TypeOfScore typeOfScore)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string sceneName = target.scene.name;
        string path = GetHierarchyPathNamesOnly(target.transform);
        return BuildBoardComponentUpgradeKey(sceneName, path, typeOfScore);
    }

    private static string GetHierarchyPathWithSiblingIndices(Transform t)
    {
        if (t == null)
        {
            return string.Empty;
        }

        var names = new Stack<string>();
        Transform current = t;
        while (current != null)
        {
            names.Push($"{current.name}[{current.GetSiblingIndex()}]");
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static string GetHierarchyPathNamesOnly(Transform t)
    {
        if (t == null)
        {
            return string.Empty;
        }

        var names = new Stack<string>();
        Transform current = t;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }
}

