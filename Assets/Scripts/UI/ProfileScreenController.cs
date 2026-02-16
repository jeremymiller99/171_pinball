// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using UnityEngine;

/// <summary>
/// Binds the Profile screen UI to ProfileService.
/// Add this to your Profile scene root and assign slot entry components in the inspector.
/// </summary>
public sealed class ProfileScreenController : MonoBehaviour
{
    [Header("Slot entries (assign 3)")]
    [SerializeField] private ProfileSlotEntryUI[] slotEntries;

    private void OnEnable()
    {
        ProfileService.ActiveSlotChanged += HandleActiveSlotChanged;
        ProfileService.ProfileChanged += HandleProfileChanged;

        RefreshAll();
    }

    private void OnDisable()
    {
        ProfileService.ActiveSlotChanged -= HandleActiveSlotChanged;
        ProfileService.ProfileChanged -= HandleProfileChanged;
    }

    public void RefreshAll()
    {
        if (slotEntries == null || slotEntries.Length == 0)
        {
            return;
        }

        for (int i = 0; i < slotEntries.Length; i++)
        {
            if (slotEntries[i] != null)
            {
                slotEntries[i].Refresh();
            }
        }
    }

    // Optional: wire these directly in the inspector to UI Buttons if you prefer.
    public void SelectSlot1()
    {
        ProfileService.SetActiveSlot(ProfileSlotId.Slot1);
    }

    public void SelectSlot2()
    {
        ProfileService.SetActiveSlot(ProfileSlotId.Slot2);
    }

    public void SelectSlot3()
    {
        ProfileService.SetActiveSlot(ProfileSlotId.Slot3);
    }

    private void HandleActiveSlotChanged(ProfileSlotId slot)
    {
        RefreshAll();
    }

    private void HandleProfileChanged(ProfileSlotId slot)
    {
        RefreshAll();
    }
}

