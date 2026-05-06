#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public static class SteamAchievements
{
    public static void Unlock(string achievementId)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized) return;
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

    public static void CheckMultMilestone(float effectiveMult)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized) return;
        if (effectiveMult >= 10f) Unlock("ACH_MULT_10");
        if (effectiveMult >= 50f) Unlock("ACH_MULT_50");
        if (effectiveMult >= 100f) Unlock("ACH_MULT_100");
#endif
    }

    public static void CheckScoreMilestones(double totalPoints)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized) return;
        if (totalPoints >= 100_000d) Unlock("ACH_SCORE_100K");
        if (totalPoints >= 1_000_000d) Unlock("ACH_SCORE_1M");
        if (totalPoints >= 10_000_000d) Unlock("ACH_SCORE_10M");
#endif
    }

    public static void CheckRunMilestones(int totalWins)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized) return;
        if (totalWins >= 5) Unlock("ACH_RUNS_5");
        if (totalWins >= 25) Unlock("ACH_RUNS_25");
        if (totalWins >= 100) Unlock("ACH_RUNS_100");
#endif
    }

    public static void CheckDevilRounds(int devilRoundsCompleted)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized) return;
        if (devilRoundsCompleted >= 5) Unlock("ACH_DEVIL_5");
#endif
    }
}
