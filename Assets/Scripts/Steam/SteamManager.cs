#if !DISABLESTEAMWORKS
using Steamworks;
#endif
using UnityEngine;

public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }
    public static bool Initialized { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject(nameof(SteamManager));
        DontDestroyOnLoad(go);
        go.AddComponent<SteamManager>();
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

#if !DISABLESTEAMWORKS
        if (SteamAPI.Init())
        {
            Initialized = true;
        }
        else
        {
            Debug.LogWarning("[SteamManager] SteamAPI.Init() failed. Steam features disabled.");
        }
#endif
    }

    private void Update()
    {
#if !DISABLESTEAMWORKS
        if (Initialized)
            SteamAPI.RunCallbacks();
#endif
    }

    private void OnApplicationQuit()
    {
#if !DISABLESTEAMWORKS
        if (Initialized)
            SteamAPI.Shutdown();
#endif
    }
}
