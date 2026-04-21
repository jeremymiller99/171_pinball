// Generated with Claude Code (Opus 4.7) by JJ on 2026-04-20.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public readonly struct PixelationPreset
{
    public readonly string displayName;
    public readonly int width;
    public readonly int height;

    public PixelationPreset(string displayName, int width, int height)
    {
        this.displayName = displayName;
        this.width = width;
        this.height = height;
    }
}

[DisallowMultipleComponent]
public sealed class PixelationSettingsManager : MonoBehaviour
{
    private const string keyLevel = "PixelationLevel";
    private const int defaultLevelIndex = 3;

    private static readonly PixelationPreset[] presets = new PixelationPreset[]
    {
        new PixelationPreset("Crisp", 1280, 720),
        new PixelationPreset("Smooth", 960, 540),
        new PixelationPreset("Normal", 640, 360),
        new PixelationPreset("Retro", 400, 225),
        new PixelationPreset("Pixel Art", 320, 180),
    };

    [Header("Runtime (debug)")]
    [SerializeField] private int currentLevelIndex = defaultLevelIndex;

    public int CurrentLevelIndex => currentLevelIndex;
    public static int PresetCount => presets.Length;

    public static PixelationPreset GetPreset(int index)
    {
        if (index < 0 || index >= presets.Length)
        {
            return presets[defaultLevelIndex];
        }

        return presets[index];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
        if (ServiceLocator.Get<PixelationSettingsManager>() != null) return;

        var go = new GameObject(nameof(PixelationSettingsManager));
        DontDestroyOnLoad(go);
        go.AddComponent<PixelationSettingsManager>();
    }

    private void Awake()
    {
        var existing = ServiceLocator.Get<PixelationSettingsManager>();
        if (existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        ServiceLocator.Register<PixelationSettingsManager>(this);
        DontDestroyOnLoad(gameObject);

        int saved = PlayerPrefs.GetInt(keyLevel, defaultLevelIndex);
        currentLevelIndex = Mathf.Clamp(saved, 0, presets.Length - 1);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (ServiceLocator.Get<PixelationSettingsManager>() == this)
        {
            ServiceLocator.Unregister<PixelationSettingsManager>();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToPixelRenderTextures();
    }

    public void ApplyPixelation(int index)
    {
        currentLevelIndex = Mathf.Clamp(index, 0, presets.Length - 1);
        ApplyToPixelRenderTextures();
        PlayerPrefs.SetInt(keyLevel, currentLevelIndex);
        PlayerPrefs.Save();
    }

    private void ApplyToPixelRenderTextures()
    {
        PixelationPreset preset = presets[currentLevelIndex];

        var seen = new HashSet<RenderTexture>();
        Camera[] cams = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < cams.Length; i++)
        {
            RenderTexture rt = cams[i].targetTexture;
            if (rt == null) continue;
            if (!seen.Add(rt)) continue;
            if (rt.width == preset.width && rt.height == preset.height) continue;

            rt.Release();
            rt.width = preset.width;
            rt.height = preset.height;
            rt.Create();
        }
    }
}
