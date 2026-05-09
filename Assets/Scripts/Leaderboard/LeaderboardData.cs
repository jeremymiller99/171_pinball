using System;
using System.Collections.Generic;

[Serializable]
public sealed class LeaderboardData
{
    public int version = 1;
    public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
}
