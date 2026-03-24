// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
// Modified by Cursor AI for jjmil on 2026-03-22.
using System;
using System.Collections.Generic;

[Serializable]
public sealed class ProfileSaveData
{
    public int version = 1;

    public string displayName = "";

    public ProfileStats stats = new ProfileStats();

    // Tutorial / first-time flags (per profile slot)
    public bool hasConsumedCleanFirstRunSkip;
    public bool hasAnsweredFirstTimePlayingPrompt;
    public bool isFirstTimePlayingAnswerYes;
    public bool hasSeenShopTutorial;

    // Progression: ball IDs unlocked via the battle-pass track.
    public List<string> unlockedBallIds = new List<string>();
}

