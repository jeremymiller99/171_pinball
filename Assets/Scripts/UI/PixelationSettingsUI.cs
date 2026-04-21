// Generated with Claude Code (Opus 4.7) by JJ on 2026-04-20.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PixelationSettingsUI : MonoBehaviour
{
    [Header("UI Dropdowns")]
    [SerializeField] private TMP_Dropdown pixelationDropdown;

    private bool initialized;

    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (initialized)
        {
            RefreshSelection();
        }
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;

        PopulatePixelationDropdown();
        RefreshSelection();

        if (pixelationDropdown != null)
        {
            pixelationDropdown.onValueChanged.AddListener(
                OnPixelationChanged
            );
        }
    }

    private void PopulatePixelationDropdown()
    {
        if (pixelationDropdown == null) return;

        int count = PixelationSettingsManager.PresetCount;
        var options = new List<TMP_Dropdown.OptionData>(count);

        for (int i = 0; i < count; i++)
        {
            PixelationPreset p = PixelationSettingsManager.GetPreset(i);
            string label = $"{p.displayName} ({p.width} x {p.height})";
            options.Add(new TMP_Dropdown.OptionData(label));
        }

        pixelationDropdown.ClearOptions();
        pixelationDropdown.AddOptions(options);
    }

    private void RefreshSelection()
    {
        PixelationSettingsManager manager = ServiceLocator.Get<PixelationSettingsManager>();
        if (manager == null) return;

        if (pixelationDropdown != null)
        {
            pixelationDropdown.SetValueWithoutNotify(manager.CurrentLevelIndex);
        }
    }

    private void OnPixelationChanged(int index)
    {
        PixelationSettingsManager manager = ServiceLocator.Get<PixelationSettingsManager>();
        if (manager == null) return;

        manager.ApplyPixelation(index);
    }

    private void OnDestroy()
    {
        if (pixelationDropdown != null)
        {
            pixelationDropdown.onValueChanged.RemoveListener(
                OnPixelationChanged
            );
        }
    }
}
