// Generated with Cursor AI, by Claude, 2026-03-22.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DisplaySettingsManager : MonoBehaviour
{
    private const string keyWidth = "DisplayWidth";
    private const string keyHeight = "DisplayHeight";
    private const string keyFullscreen = "FullscreenMode";

    [Header("Runtime (debug)")]
    [SerializeField] private int currentResolutionIndex;
    [SerializeField] private FullScreenMode currentFullscreenMode;

    private Resolution[] availableResolutions;

    public Resolution[] AvailableResolutions => availableResolutions;
    public int CurrentResolutionIndex => currentResolutionIndex;
    public FullScreenMode CurrentFullscreenMode => currentFullscreenMode;

    public event Action DisplayChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
        if (ServiceLocator.Get<DisplaySettingsManager>() != null) return;

        var go = new GameObject(nameof(DisplaySettingsManager));
        DontDestroyOnLoad(go);
        go.AddComponent<DisplaySettingsManager>();
    }

    private void Awake()
    {
        var existing = ServiceLocator.Get<DisplaySettingsManager>();
        if (existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        ServiceLocator.Register<DisplaySettingsManager>(this);
        DontDestroyOnLoad(gameObject);

        BuildResolutionList();
        LoadAndApply();
    }

    private void OnDestroy()
    {
        if (ServiceLocator.Get<DisplaySettingsManager>() == this)
        {
            ServiceLocator.Unregister<DisplaySettingsManager>();
        }
    }

    private void BuildResolutionList()
    {
        Resolution[] raw = Screen.resolutions;

        var unique = new Dictionary<long, Resolution>();
        for (int i = 0; i < raw.Length; i++)
        {
            Resolution r = raw[i];
            long key = (long)r.width << 32 | (uint)r.height;

            if (!unique.ContainsKey(key))
            {
                unique[key] = r;
            }
            else
            {
                Resolution existing = unique[key];
                double existingRate = existing.refreshRateRatio.value;
                double newRate = r.refreshRateRatio.value;
                if (newRate > existingRate)
                {
                    unique[key] = r;
                }
            }
        }

        var list = new List<Resolution>(unique.Values);
        list.Sort((a, b) =>
        {
            int area = (b.width * b.height).CompareTo(a.width * a.height);
            if (area != 0) return area;
            return b.width.CompareTo(a.width);
        });

        availableResolutions = list.ToArray();
    }

    private void LoadAndApply()
    {
        int savedWidth = PlayerPrefs.GetInt(keyWidth, 0);
        int savedHeight = PlayerPrefs.GetInt(keyHeight, 0);
        int savedMode = PlayerPrefs.GetInt(keyFullscreen, -1);

        if (savedMode >= 0)
        {
            currentFullscreenMode = (FullScreenMode)savedMode;
        }
        else
        {
            currentFullscreenMode = Screen.fullScreenMode;
        }

        currentResolutionIndex = FindResolutionIndex(savedWidth, savedHeight);
        if (currentResolutionIndex < 0)
        {
            currentResolutionIndex = FindResolutionIndex(
                Screen.currentResolution.width,
                Screen.currentResolution.height
            );
        }

        if (currentResolutionIndex < 0 && availableResolutions.Length > 0)
        {
            currentResolutionIndex = 0;
        }

        if (savedWidth > 0 && savedHeight > 0)
        {
            ApplyCurrentSettings();
        }
    }

    private int FindResolutionIndex(int width, int height)
    {
        if (availableResolutions == null) return -1;

        for (int i = 0; i < availableResolutions.Length; i++)
        {
            if (availableResolutions[i].width == width
                && availableResolutions[i].height == height)
            {
                return i;
            }
        }

        return -1;
    }

    public void ApplyResolution(int index)
    {
        if (availableResolutions == null
            || index < 0
            || index >= availableResolutions.Length)
        {
            return;
        }

        currentResolutionIndex = index;
        ApplyCurrentSettings();
        Save();
        DisplayChanged?.Invoke();
    }

    public void ApplyFullscreenMode(FullScreenMode mode)
    {
        currentFullscreenMode = mode;
        ApplyCurrentSettings();
        Save();
        DisplayChanged?.Invoke();
    }

    private void ApplyCurrentSettings()
    {
        if (availableResolutions == null
            || currentResolutionIndex < 0
            || currentResolutionIndex >= availableResolutions.Length)
        {
            return;
        }

        Resolution res = availableResolutions[currentResolutionIndex];
        Screen.SetResolution(
            res.width,
            res.height,
            currentFullscreenMode,
            res.refreshRateRatio
        );
    }

    private void Save()
    {
        if (availableResolutions == null
            || currentResolutionIndex < 0
            || currentResolutionIndex >= availableResolutions.Length)
        {
            return;
        }

        Resolution res = availableResolutions[currentResolutionIndex];
        PlayerPrefs.SetInt(keyWidth, res.width);
        PlayerPrefs.SetInt(keyHeight, res.height);
        PlayerPrefs.SetInt(keyFullscreen, (int)currentFullscreenMode);
        PlayerPrefs.Save();
    }
}
