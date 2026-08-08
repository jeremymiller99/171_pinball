// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
// Modified by Cursor AI for jjmil on 2026-03-24.
// Updated by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (FTUE completion + AI name).
using System;
using System.Collections.Generic;

[Serializable]
public sealed class ChallengeBestEntry
{
    public string challengeName;
    public long bestScore;
}

[Serializable]
public sealed class ProfileSaveData
{
    public int version = 1;

    public string displayName = "";

    public ProfileStats stats = new ProfileStats();

    public bool hasConsumedCleanFirstRunSkip;
    public bool hasAnsweredFirstTimePlayingPrompt;
    public bool isFirstTimePlayingAnswerYes;
    public bool hasSeenShopTutorial;
    public bool hasSeenFirstPlayTutorial;
    public bool hasSeenLevelUpTutorial;

    // False on a brand new profile, which is what routes a first-time player into the FTUE board.
    // Every profile that predates version 7 is grandfathered to true by the migration, so existing
    // players are never dropped into the tutorial.
    public bool hasCompletedFtue;

    // What the player named the assisting AI during the FTUE. Empty until they choose; readers go
    // through FtueNarrator, which substitutes the fallback name. Player-authored text — it must
    // not be sent to analytics, Steam, or any log line.
    public string aiName = "";

    public List<string> unlockedBallIds = new List<string>();

    public List<string> unlockedComponentIds =
        new List<string>();

    public List<string> unlockedShipIds =
        new List<string>();

    public List<ChallengeBestEntry> challengeBests =
        new List<ChallengeBestEntry>();
}

