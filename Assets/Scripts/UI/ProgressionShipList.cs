// Created with Claude Code (Opus 4.8) by JJ on 2026-06-08: populates the
// monitor-3 progression "Ships" tab with every player ship, mirroring how
// ProgressionItemList builds the "Items" tab.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the scrolling list of progression ships shown on the monitor-3
/// "Ships" tab. Reads the same <see cref="ProgressionConfig"/> the Items tab
/// does: starter ships first, then each <see cref="ProgressionTier"/> ship
/// reward in order, labelling every row with the lifetime score it unlocks at
/// (starters show <see cref="starterLabel"/>).
///
/// This is the ship-flavoured twin of <see cref="ProgressionItemList"/> and
/// reuses the same <see cref="ProgressionItemEntry"/> row prefab. Rebuilds
/// whenever this object is enabled — put it on the Ships-tab content (or
/// anything that becomes active when the tab is shown) so it refreshes each
/// time the tab opens, picking up newly-earned unlocks.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProgressionShipList : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("Progression config to read. Falls back to ProgressionService.Config if left null.")]
    [SerializeField] private ProgressionConfig config;

    [Header("Layout")]
    [Tooltip("The scroll-view Content (with the VerticalLayoutGroup) that rows are parented under.")]
    [SerializeField] private RectTransform content;

    [Tooltip("Prefab instantiated once per ship. Must have a ProgressionItemEntry component.")]
    [SerializeField] private ProgressionItemEntry entryPrefab;

    [Header("Labels")]
    [Tooltip("Amount text shown for starter (always-unlocked) ships.")]
    [SerializeField] private string starterLabel = "Starter";

    private readonly List<ProgressionItemEntry> _rows = new List<ProgressionItemEntry>();

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>Clears and rebuilds every row from the current config + unlock state.</summary>
    public void Refresh()
    {
        EnsureConfig();
        Clear();

        if (config == null || content == null || entryPrefab == null)
        {
            Debug.LogWarning($"{nameof(ProgressionShipList)}: missing config, content, or entryPrefab; " +
                "nothing to build.", this);
            return;
        }

        double xp = ProgressionService.Instance != null
            ? ProgressionService.Instance.GetCurrentXp()
            : 0d;

        // Starter ships — always unlocked.
        IReadOnlyList<PlayerShipDefinition> starterShips = config.StarterShips;
        for (int i = 0; i < starterShips.Count; i++)
        {
            PlayerShipDefinition ship = starterShips[i];
            if (ship != null)
            {
                Spawn(ship.GetSafeDisplayName(), starterLabel, true, ship.Icon);
            }
        }

        // Tier ship rewards, in order.
        IReadOnlyList<ProgressionTier> tiers = config.Tiers;
        for (int i = 0; i < tiers.Count; i++)
        {
            ProgressionTier tier = tiers[i];
            if (tier == null || !tier.HasValidShipReward)
            {
                continue;
            }

            bool unlocked = xp >= tier.XpThreshold;
            string amount = FormatXp(tier.XpThreshold);

            PlayerShipDefinition ship = tier.RewardShip;
            Spawn(ship.GetSafeDisplayName(), amount, unlocked, ship.Icon);
        }
    }

    private void Spawn(string shipName, string amountLabel, bool unlocked, Sprite icon)
    {
        ProgressionItemEntry row = Instantiate(entryPrefab, content);
        row.gameObject.SetActive(true);
        row.Apply(shipName, amountLabel, unlocked, icon);
        _rows.Add(row);
    }

    private void Clear()
    {
        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            if (_rows[i] != null)
            {
                Destroy(_rows[i].gameObject);
            }
        }
        _rows.Clear();
    }

    private void EnsureConfig()
    {
        if (config == null && ProgressionService.Instance != null)
        {
            config = ProgressionService.Instance.Config;
        }
    }

    // Mirrors ProgressionItemList's score formatting (e.g. 1.5K, 2.0M).
    private static string FormatXp(double value)
    {
        if (value >= 1_000_000d)
        {
            return $"{value / 1_000_000d:F1}M";
        }

        if (value >= 1_000d)
        {
            return $"{value / 1_000d:F1}K";
        }

        return $"{value:N0}";
    }
}
