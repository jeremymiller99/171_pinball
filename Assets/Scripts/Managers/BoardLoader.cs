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
        ServiceLocator.Register<BoardLoader>(this);

        if (rulesManager == null)
        {
            rulesManager = ServiceLocator.Get<GameRulesManager>();
        }

        if (ballSpawner == null)
        {
            ballSpawner = ServiceLocator.Get<BallSpawner>();
        }
    }

    private void OnDisable()
    {
        ServiceLocator.Unregister<BoardLoader>();
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

        // Disable duplicate Main Camera and EventSystem in the board scene.
        // GameplayCore provides the main view; additive boards must not have competing cameras/input.
        DisableBoardSceneDuplicates(loadedScene);

        Transform sp = currentBoardRoot.SpawnPoint;
        if (sp == null)
        {
            Debug.LogError($"{nameof(BoardLoader)}: BoardRoot '{currentBoardRoot.name}' has no SpawnPoint assigned.", currentBoardRoot);
            yield break;
        }

        if (ballSpawner != null)
        {
            ballSpawner.SetSpawnPoint(sp);
            var slots = currentBoardRoot.HandSlots;
            if (slots != null)
            {
                var list = new System.Collections.Generic.List<BallHandSlot>(slots.Count);
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null) list.Add(slots[i]);
                }
                ballSpawner.SetHandSlots(list);
            }
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

    /// <summary>
    /// Disables Main Camera and EventSystem in the board scene so GameplayCore's instances are used.
    /// Prevents second-shop-entry bugs from duplicate cameras/input.
    /// </summary>
    private static void DisableBoardSceneDuplicates(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var cameras = roots[i].GetComponentsInChildren<Camera>(includeInactive: true);
            for (int c = 0; c < cameras.Length; c++)
            {
                if (cameras[c] != null && cameras[c].CompareTag("MainCamera"))
                {
                    cameras[c].gameObject.SetActive(false);
                }
            }

            var eventSystems = roots[i].GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(includeInactive: true);
            for (int e = 0; e < eventSystems.Length; e++)
            {
                if (eventSystems[e] != null)
                {
                    eventSystems[e].gameObject.SetActive(false);
                }
            }
        }
    }
}

