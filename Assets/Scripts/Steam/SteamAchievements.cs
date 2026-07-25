#if !DISABLESTEAMWORKS
using Steamworks;
#endif
using System.Collections.Generic;
using UnityEngine;

public static class SteamAchievements
{
    // Unlocks already sent this session; Steam ignores repeats, this just
    // avoids re-sending (and re-logging) on every score/mult change.
    private static readonly HashSet<string> sent = new HashSet<string>();

    public static void Unlock(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId)) return;
        if (!sent.Add(achievementId)) return;

        Debug.Log($"[SteamAchievements] Unlock: {achievementId}");

#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized)
        {
            Debug.Log($"[SteamAchievements] Steam not initialized -- {achievementId} not sent to Steam.");
            return;
        }
        SteamUserStats.SetAchievement(achievementId);
        SteamUserStats.StoreStats();
#endif
    }

    public static void UnlockFirstWin()
    {
        Unlock("ACH_FIRST_WIN");
    }

    public static void UnlockBoardWin(string boardSceneName)
    {
        if (string.IsNullOrEmpty(boardSceneName)) return;
        Unlock("ACH_WIN_" + boardSceneName.ToUpperInvariant());
    }

    public static void UnlockFirstFrenzy()
    {
        Unlock("ACH_FIRST_FRENZY");
    }

    public static void UnlockFirstShopVisit()
    {
        Unlock("ACH_FIRST_SHOP");
    }

    public static void UnlockFirstBallPurchase()
    {
        Unlock("ACH_BUY_BALL");
    }

    public static void UnlockFirstComponentPurchase()
    {
        Unlock("ACH_BUY_COMPONENT");
    }

    public static void CheckLevelMilestone(int levelIndex, string boardSceneName)
    {
        if (string.IsNullOrEmpty(boardSceneName)) return;
        if (levelIndex + 1 >= 5) Unlock("ACH_LEVEL5_" + boardSceneName.ToUpperInvariant());
    }

    public static void CheckMultMilestone(float effectiveMult)
    {
        if (effectiveMult >= 10f) Unlock("ACH_MULT_10");
        if (effectiveMult >= 50f) Unlock("ACH_MULT_50");
        if (effectiveMult >= 100f) Unlock("ACH_MULT_100");
    }

    public static void CheckScoreMilestones(double totalPoints)
    {
        if (totalPoints >= 100_000d) Unlock("ACH_SCORE_100K");
        if (totalPoints >= 1_000_000d) Unlock("ACH_SCORE_1M");
        if (totalPoints >= 10_000_000d) Unlock("ACH_SCORE_10M");
    }

    public static void CheckRunMilestones(int totalWins)
    {
        if (totalWins >= 5) Unlock("ACH_RUNS_5");
        if (totalWins >= 25) Unlock("ACH_RUNS_25");
        if (totalWins >= 100) Unlock("ACH_RUNS_100");
    }

    public static void CheckDevilRounds(int devilRoundsCompleted)
    {
        if (devilRoundsCompleted >= 5) Unlock("ACH_DEVIL_5");
    }
}
