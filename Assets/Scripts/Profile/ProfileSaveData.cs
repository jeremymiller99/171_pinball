// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
// Modified by Cursor AI for jjmil on 2026-03-24.
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

    public List<string> unlockedBallIds = new List<string>();

    public List<string> unlockedComponentIds =
        new List<string>();

    public List<ChallengeBestEntry> challengeBests =
        new List<ChallengeBestEntry>();
}

