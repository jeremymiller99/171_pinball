// Created with Claude Code (Opus 4.8) by JJ on 2026-06-08: a single row in the
// monitor-3 progression "Items" list (item name + the score it unlocks at).
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One row in the progression Items list. Attach to the item-entry prefab and
/// wire up at least the name and amount text. <see cref="ProgressionItemList"/>
/// instantiates this prefab once per pinball/component and calls
/// <see cref="Apply"/> to fill it in.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProgressionItemEntry : MonoBehaviour
{
    [Header("Required")]
    [Tooltip("Displays the item's name.")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("Displays the score the item unlocks at (or \"Starter\").")]
    [SerializeField] private TMP_Text amountText;

    [Header("Optional")]
    [Tooltip("Item icon. Left untouched if unassigned.")]
    [SerializeField] private Image iconImage;

    [Tooltip("Shown only while the item is still locked. Optional.")]
    [SerializeField] private GameObject lockedOverlay;

    [Header("Styling")]
    [Tooltip("Name color once the item is unlocked.")]
    [SerializeField] private Color unlockedColor = Color.white;

    [Tooltip("Name color while the item is still locked.")]
    [SerializeField] private Color lockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    /// <summary>Fills this row in for one progression item.</summary>
    public void Apply(string itemName, string amountLabel, bool unlocked, Sprite icon)
    {
        if (nameText != null)
        {
            nameText.text = itemName;
            nameText.color = unlocked ? unlockedColor : lockedColor;
        }

        if (amountText != null)
        {
            amountText.text = amountLabel;
        }

        if (iconImage != null && icon != null)
        {
            iconImage.sprite = icon;
        }

        if (lockedOverlay != null)
        {
            lockedOverlay.SetActive(!unlocked);
        }
    }
}
