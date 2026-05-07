using System;

[Serializable]
public sealed class LeaderboardEntry
{
    public string playerName;
    public long score;
    public int levelReached;
    public string boardName;
    public string dateUtc;
    public bool wasWin;
}
