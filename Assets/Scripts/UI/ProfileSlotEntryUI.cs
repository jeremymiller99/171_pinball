// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI binder for one profile slot row (Save 1/2/3).
/// Assign TMP labels + optional buttons/indicator in the inspector.
/// </summary>
public sealed class ProfileSlotEntryUI : MonoBehaviour
{
    [Header("Slot")]
    [SerializeField] private ProfileSlotId slotId = ProfileSlotId.Slot1;

    [Header("UI (optional)")]
    [SerializeField] private TMP_Text slotLabelText;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text winsText;
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private GameObject selectedIndicator;

    [Header("Buttons (optional)")]
    [SerializeField] private Button selectButton;
    [SerializeField] private Button resetButton;

    private void Awake()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(OnSelectPressed);
            selectButton.onClick.AddListener(OnSelectPressed);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(OnResetPressed);
            resetButton.onClick.AddListener(OnResetPressed);
        }
    }

    private void OnEnable()
    {
        ProfileService.ActiveSlotChanged += HandleActiveSlotChanged;
        ProfileService.ProfileChanged += HandleProfileChanged;

        Refresh();
    }

    private void OnDisable()
    {
        ProfileService.ActiveSlotChanged -= HandleActiveSlotChanged;
        ProfileService.ProfileChanged -= HandleProfileChanged;
    }

    public void Refresh()
    {
        ProfileSaveData data = ProfileService.GetProfileCopy(slotId);
        ProfileStats stats = data != null ? data.stats : null;

        if (slotLabelText != null)
        {
            slotLabelText.text = GetDefaultSlotLabel(slotId);
        }

        if (displayNameText != null)
        {
            displayNameText.text = data != null ? (data.displayName ?? string.Empty) : string.Empty;
        }

        if (pointsText != null)
        {
            double points = stats != null ? stats.totalPointsScored : 0d;
            pointsText.text = FormatWholeNumber(points);
        }

        if (winsText != null)
        {
            int wins = stats != null ? stats.totalBoardWins : 0;
            winsText.text = wins.ToString(CultureInfo.InvariantCulture);
        }

        if (selectedIndicator != null)
        {
            selectedIndicator.SetActive(ProfileService.GetActiveSlot() == slotId);
        }
    }

    public void SetSlotId(ProfileSlotId id)
    {
        slotId = id;
        Refresh();
    }

    private void OnSelectPressed()
    {
        ProfileService.SetActiveSlot(slotId);
    }

    private void OnResetPressed()
    {
        ProfileService.ResetSlot(slotId);
    }

    private void HandleActiveSlotChanged(ProfileSlotId slot)
    {
        // Ensure the selected indicator deactivates on other entries.
        Refresh();
    }

    private void HandleProfileChanged(ProfileSlotId slot)
    {
        if (slot == slotId)
        {
            Refresh();
        }
        else if (selectedIndicator != null)
        {
            // Keep indicator correct even if you only refresh the changed slot elsewhere.
            selectedIndicator.SetActive(ProfileService.GetActiveSlot() == slotId);
        }
    }

    private static string GetDefaultSlotLabel(ProfileSlotId slot)
    {
        switch (slot)
        {
            case ProfileSlotId.Slot1:
                return "Save 1";
            case ProfileSlotId.Slot2:
                return "Save 2";
            case ProfileSlotId.Slot3:
                return "Save 3";
            default:
                return "Save";
        }
    }

    private static string FormatWholeNumber(double value)
    {
        if (value <= 0d)
        {
            return "0";
        }

        // Keep as whole number for display (points are banked floats, but stat is a running total).
        double rounded = System.Math.Floor(value);
        return rounded.ToString("0", CultureInfo.InvariantCulture);
    }
}

