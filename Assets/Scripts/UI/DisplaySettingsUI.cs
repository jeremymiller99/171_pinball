// Generated with Cursor AI, by Claude, 2026-03-22.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DisplaySettingsUI : MonoBehaviour
{
    [Header("UI Dropdowns")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown fullscreenDropdown;

    private bool initialized;

    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (initialized)
        {
            RefreshSelections();
        }
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;

        PopulateFullscreenDropdown();
        PopulateResolutionDropdown();
        RefreshSelections();

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.AddListener(
                OnResolutionChanged
            );
        }

        if (fullscreenDropdown != null)
        {
            fullscreenDropdown.onValueChanged.AddListener(
                OnFullscreenChanged
            );
        }
    }

    private void PopulateResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        DisplaySettingsManager manager = ServiceLocator.Get<DisplaySettingsManager>();
        if (manager == null || manager.AvailableResolutions == null) return;

        Resolution[] resolutions = manager.AvailableResolutions;

        var options = new List<TMP_Dropdown.OptionData>(
            resolutions.Length
        );

        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution r = resolutions[i];
            string label = $"{r.width} x {r.height}";
            options.Add(new TMP_Dropdown.OptionData(label));
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
    }

    private void PopulateFullscreenDropdown()
    {
        if (fullscreenDropdown == null) return;

        var options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("Fullscreen"),
            new TMP_Dropdown.OptionData("Borderless Window"),
            new TMP_Dropdown.OptionData("Windowed")
        };

        fullscreenDropdown.ClearOptions();
        fullscreenDropdown.AddOptions(options);
    }

    private void RefreshSelections()
    {
        DisplaySettingsManager manager = ServiceLocator.Get<DisplaySettingsManager>();
        if (manager == null) return;

        if (resolutionDropdown != null
            && manager.CurrentResolutionIndex >= 0)
        {
            resolutionDropdown.SetValueWithoutNotify(
                manager.CurrentResolutionIndex
            );
        }

        if (fullscreenDropdown != null)
        {
            int modeIndex = FullscreenModeToIndex(
                manager.CurrentFullscreenMode
            );
            fullscreenDropdown.SetValueWithoutNotify(modeIndex);
        }
    }

    private void OnResolutionChanged(int index)
    {
        DisplaySettingsManager manager = ServiceLocator.Get<DisplaySettingsManager>();
        if (manager == null) return;

        manager.ApplyResolution(index);
    }

    private void OnFullscreenChanged(int index)
    {
        DisplaySettingsManager manager = ServiceLocator.Get<DisplaySettingsManager>();
        if (manager == null) return;

        FullScreenMode mode = IndexToFullscreenMode(index);
        manager.ApplyFullscreenMode(mode);
    }

    private static int FullscreenModeToIndex(FullScreenMode mode)
    {
        switch (mode)
        {
            case FullScreenMode.ExclusiveFullScreen:
                return 0;
            case FullScreenMode.FullScreenWindow:
                return 1;
            case FullScreenMode.Windowed:
                return 2;
            default:
                return 0;
        }
    }

    private static FullScreenMode IndexToFullscreenMode(int index)
    {
        switch (index)
        {
            case 0: return FullScreenMode.ExclusiveFullScreen;
            case 1: return FullScreenMode.FullScreenWindow;
            case 2: return FullScreenMode.Windowed;
            default: return FullScreenMode.ExclusiveFullScreen;
        }
    }

    private void OnDestroy()
    {
        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.RemoveListener(
                OnResolutionChanged
            );
        }

        if (fullscreenDropdown != null)
        {
            fullscreenDropdown.onValueChanged.RemoveListener(
                OnFullscreenChanged
            );
        }
    }
}
