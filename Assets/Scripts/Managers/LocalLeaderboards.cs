using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Machine-local high scores persisted in PlayerPrefs, one list per
/// board. Lives alongside Steam leaderboards for shared-machine
/// playtests: every run adds a row (no keep-best), and the lists can
/// be cleared from the leaderboard panel.
/// </summary>
public static class LocalLeaderboards
{
    private const string keyPrefix = "LocalLeaderboard_";
    private const string boardsKey = "LocalLeaderboard_Boards";
    private const int maxEntriesPerBoard = 200;

    [Serializable]
    public struct Entry
    {
        public long score;
        public int level;
        public long ticksUtc;
    }

    [Serializable]
    private class EntryList
    {
        public List<Entry> entries = new List<Entry>();
    }

    public static void AddScore(string boardSceneName, long score, int level)
    {
        if (string.IsNullOrEmpty(boardSceneName)) return;

        EntryList list = Load(boardSceneName);
        list.entries.Add(new Entry
        {
            score = Math.Max(0L, score),
            level = Math.Max(1, level),
            ticksUtc = DateTime.UtcNow.Ticks,
        });

        list.entries.Sort((a, b) => b.score.CompareTo(a.score));
        if (list.entries.Count > maxEntriesPerBoard)
        {
            list.entries.RemoveRange(
                maxEntriesPerBoard, list.entries.Count - maxEntriesPerBoard);
        }

        PlayerPrefs.SetString(keyPrefix + boardSceneName, JsonUtility.ToJson(list));
        RegisterBoard(boardSceneName);
        PlayerPrefs.Save();
    }

    /// <summary>Entries for a board, already sorted best-first.</summary>
    public static List<Entry> GetEntries(string boardSceneName)
    {
        if (string.IsNullOrEmpty(boardSceneName)) return new List<Entry>();

        return Load(boardSceneName).entries;
    }

    public static void ClearAll()
    {
        string boards = PlayerPrefs.GetString(boardsKey, "");
        foreach (string board in boards.Split('|'))
        {
            if (board.Length > 0) PlayerPrefs.DeleteKey(keyPrefix + board);
        }

        PlayerPrefs.DeleteKey(boardsKey);
        PlayerPrefs.Save();
    }

    private static EntryList Load(string boardSceneName)
    {
        string json = PlayerPrefs.GetString(keyPrefix + boardSceneName, "");
        if (string.IsNullOrEmpty(json)) return new EntryList();

        try
        {
            EntryList list = JsonUtility.FromJson<EntryList>(json);
            return list ?? new EntryList();
        }
        catch (Exception)
        {
            return new EntryList();
        }
    }

    // Tracked so ClearAll can find every board's key without loading
    // board definitions.
    private static void RegisterBoard(string boardSceneName)
    {
        string boards = PlayerPrefs.GetString(boardsKey, "");
        foreach (string board in boards.Split('|'))
        {
            if (board == boardSceneName) return;
        }

        PlayerPrefs.SetString(boardsKey,
            boards.Length == 0 ? boardSceneName : boards + "|" + boardSceneName);
    }
}
