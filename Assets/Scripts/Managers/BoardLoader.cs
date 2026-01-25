using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads/unloads board scenes additively and binds board references into core gameplay systems.
/// </summary>
public sealed class BoardLoader : MonoBehaviour
{
    [Header("Core refs")]
    [SerializeField] private GameRulesManager rulesManager;
    [SerializeField] private BallSpawner ballSpawner;

    [Header("Runtime (debug)")]
    [SerializeField] private string currentBoardSceneName;
    [SerializeField] private BoardRoot currentBoardRoot;

    public BoardRoot CurrentBoardRoot => currentBoardRoot;
    public string CurrentBoardSceneName => currentBoardSceneName;

    private void Awake()
    {
        if (rulesManager == null)
        {
#if UNITY_2022_2_OR_NEWER
            rulesManager = FindFirstObjectByType<GameRulesManager>();
#else
            rulesManager = FindObjectOfType<GameRulesManager>();
#endif
        }

        if (ballSpawner == null)
        {
#if UNITY_2022_2_OR_NEWER
            ballSpawner = FindFirstObjectByType<BallSpawner>();
#else
            ballSpawner = FindObjectOfType<BallSpawner>();
#endif
        }
    }

    public IEnumerator LoadBoard(BoardDefinition board)
    {
        if (board == null || string.IsNullOrWhiteSpace(board.boardSceneName))
        {
            Debug.LogError($"{nameof(BoardLoader)}: Missing board or boardSceneName.", this);
            yield break;
        }

        // Unload current board if any.
        if (!string.IsNullOrWhiteSpace(currentBoardSceneName))
        {
            Scene oldScene = SceneManager.GetSceneByName(currentBoardSceneName);
            if (oldScene.IsValid() && oldScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(oldScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }
        }

        currentBoardSceneName = board.boardSceneName;
        currentBoardRoot = null;

        AsyncOperation load = SceneManager.LoadSceneAsync(board.boardSceneName, LoadSceneMode.Additive);
        if (load == null)
        {
            Debug.LogError($"{nameof(BoardLoader)}: Failed to start loading scene '{board.boardSceneName}'. Is it in Build Settings?", this);
            yield break;
        }
        while (!load.isDone)
        {
            yield return null;
        }

        Scene loadedScene = SceneManager.GetSceneByName(board.boardSceneName);
        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
        {
            Debug.LogError($"{nameof(BoardLoader)}: Loaded scene not valid/loaded: {board.boardSceneName}", this);
            yield break;
        }

        // Find BoardRoot inside that scene.
        currentBoardRoot = FindBoardRootInScene(loadedScene);
        if (currentBoardRoot == null)
        {
            Debug.LogError($"{nameof(BoardLoader)}: No {nameof(BoardRoot)} found in scene '{board.boardSceneName}'. Add one to the board scene.", this);
            yield break;
        }

        currentBoardRoot.Initialize(board);

        // Bind spawn point into core systems.
        Transform sp = currentBoardRoot.SpawnPoint;
        if (sp == null)
        {
            Debug.LogError($"{nameof(BoardLoader)}: BoardRoot '{currentBoardRoot.name}' has no spawnPoint assigned.", currentBoardRoot);
            yield break;
        }

        if (ballSpawner != null)
        {
            ballSpawner.SetSpawnPoint(sp);
            // Hand path start; if unset, the spawner will fall back to its own transform.
            ballSpawner.SetHandPath(currentBoardRoot.HandPathStart, currentBoardRoot.HandPathWaypoints);
        }
    }

    private static BoardRoot FindBoardRootInScene(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var r = roots[i].GetComponentInChildren<BoardRoot>(includeInactive: true);
            if (r != null)
            {
                return r;
            }
        }
        return null;
    }
}

