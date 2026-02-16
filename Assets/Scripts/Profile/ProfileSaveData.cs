// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using System;

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
}

