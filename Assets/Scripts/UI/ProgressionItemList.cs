// Created with Claude Code (Opus 4.8) by JJ on 2026-06-08: populates the
// monitor-3 progression "Items" tab with every pinball and component, in the
// same order/data the old main-menu progression screen used.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the scrolling list of progression items (pinballs + board components)
/// shown on the monitor-3 "Items" tab. Reads the same data the old progression
/// screen did: starter balls, then starter components, then each
/// <see cref="ProgressionTier"/> reward in order, labelling every row with the
/// lifetime score it unlocks at (starters show <see cref="starterLabel"/>).
///
/// Rebuilds whenever this object is enabled — put it on the Items-tab content (or
/// anything that becomes active when the tab is shown) so it refreshes each time
/// the tab opens, picking up newly-earned unlocks.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProgressionItemList : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("Progression config to read. Falls back to ProgressionService.Config if left null.")]
    [SerializeField] private ProgressionConfig config;

    [Header("Layout")]
    [Tooltip("The scroll-view Content (with the VerticalLayoutGroup) that rows are parented under.")]
    [SerializeField] private RectTransform content;

    [Tooltip("Prefab instantiated once per item. Must have a ProgressionItemEntry component.")]
    [SerializeField] private ProgressionItemEntry entryPrefab;

    [Header("Labels")]
    [Tooltip("Amount text shown for starter (always-unlocked) items.")]
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
            Debug.LogWarning($"{nameof(ProgressionItemList)}: missing config, content, or entryPrefab; " +
                "nothing to build.", this);
            return;
        }

        double xp = ProgressionService.Instance != null
            ? ProgressionService.Instance.GetCurrentXp()
            : 0d;

        // Starter balls, then starter components — always unlocked.
        IReadOnlyList<BallDefinition> starterBalls = config.StarterBalls;
        for (int i = 0; i < starterBalls.Count; i++)
        {
            BallDefinition ball = starterBalls[i];
            if (ball != null)
            {
                Spawn(ball.GetSafeDisplayName(), starterLabel, true, ball.Icon);
            }
        }

        IReadOnlyList<BoardComponentDefinition> starterComps = config.StarterComponents;
        for (int i = 0; i < starterComps.Count; i++)
        {
            BoardComponentDefinition comp = starterComps[i];
            if (comp != null)
            {
                Spawn(comp.GetSafeDisplayName(), starterLabel, true, comp.Icon);
            }
        }

        // Tier rewards, in order — each tier grants a ball or a component.
        IReadOnlyList<ProgressionTier> tiers = config.Tiers;
        for (int i = 0; i < tiers.Count; i++)
        {
            ProgressionTier tier = tiers[i];
            if (tier == null || !tier.HasValidReward)
            {
                continue;
            }

            bool unlocked = xp >= tier.XpThreshold;
            string amount = FormatXp(tier.XpThreshold);

            if (tier.HasValidBallReward)
            {
                BallDefinition ball = tier.RewardBall;
                Spawn(ball.GetSafeDisplayName(), amount, unlocked, ball.Icon);
            }
            else if (tier.HasValidComponentReward)
            {
                BoardComponentDefinition comp = tier.RewardComponent;
                Spawn(comp.GetSafeDisplayName(), amount, unlocked, comp.Icon);
            }
        }
    }

    private void Spawn(string itemName, string amountLabel, bool unlocked, Sprite icon)
    {
        ProgressionItemEntry row = Instantiate(entryPrefab, content);
        row.gameObject.SetActive(true);
        row.Apply(itemName, amountLabel, unlocked, icon);
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

    // Mirrors the old progression screen's score formatting (e.g. 1.5K, 2.0M).
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
