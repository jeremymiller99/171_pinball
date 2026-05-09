using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class LocalLeaderboard
{
    private const int maxEntries = 100;
    private const int currentVersion = 1;
    private const string fileName = "leaderboard.json";
    private const string lastNamePrefsKey = "LocalLeaderboard_LastName";

    private static LeaderboardData cached;
    private static bool loaded;

    public static string GetLastUsedName()
    {
        return PlayerPrefs.GetString(lastNamePrefsKey, "");
    }

    public static void SetLastUsedName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return;

        PlayerPrefs.SetString(lastNamePrefsKey, playerName.Trim());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Submits a new run entry. Returns the 1-based rank of the inserted entry,
    /// or -1 if it didn't make the cap.
    /// </summary>
    public static int Submit(string playerName, long score, int levelReached,
        string boardName, bool wasWin)
    {
        EnsureLoaded();

        var entry = new LeaderboardEntry
        {
            playerName = string.IsNullOrWhiteSpace(playerName)
                ? "Anonymous"
                : playerName.Trim(),
            score = Math.Max(0L, score),
            levelReached = Math.Max(0, levelReached),
            boardName = boardName ?? "",
            dateUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            wasWin = wasWin
        };

        cached.entries.Add(entry);
        cached.entries.Sort((a, b) => b.score.CompareTo(a.score));

        int insertedIndex = cached.entries.IndexOf(entry);

        if (cached.entries.Count > maxEntries)
        {
            cached.entries.RemoveRange(maxEntries, cached.entries.Count - maxEntries);
        }

        SetLastUsedName(entry.playerName);
        Save();

        return insertedIndex >= 0 && insertedIndex < cached.entries.Count
            ? insertedIndex + 1
            : -1;
    }

    public static IReadOnlyList<LeaderboardEntry> GetTopEntries(int count)
    {
        EnsureLoaded();

        int n = Mathf.Clamp(count, 0, cached.entries.Count);
        var top = new List<LeaderboardEntry>(n);

        for (int i = 0; i < n; i++)
        {
            top.Add(cached.entries[i]);
        }

        return top;
    }

    public static int TotalEntries
    {
        get
        {
            EnsureLoaded();
            return cached.entries.Count;
        }
    }

    public static void ClearAll()
    {
        EnsureLoaded();
        cached.entries.Clear();
        Save();
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        string path = GetFilePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            cached = new LeaderboardData();
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                cached = new LeaderboardData();
                return;
            }

            LeaderboardData data = JsonUtility.FromJson<LeaderboardData>(json);
            if (data == null)
            {
                cached = new LeaderboardData();
                return;
            }

            if (data.entries == null) data.entries = new List<LeaderboardEntry>();
            if (data.version <= 0) data.version = currentVersion;

            data.entries.Sort((a, b) => b.score.CompareTo(a.score));
            cached = data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LocalLeaderboard] Failed to load: {ex.Message}");
            cached = new LeaderboardData();
        }
    }

    private static void Save()
    {
        string path = GetFilePath();
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            string json = JsonUtility.ToJson(cached);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LocalLeaderboard] Failed to save: {ex.Message}");
        }
    }

    private static string GetFilePath()
    {
        string root = Application.persistentDataPath;
        if (string.IsNullOrWhiteSpace(root)) return "";

        return Path.Combine(root, fileName);
    }
}
