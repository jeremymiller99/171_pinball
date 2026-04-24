#if !DISABLESTEAMWORKS
using Steamworks;
using System.Collections.Generic;
#endif
using UnityEngine;

public class SteamLeaderboards : MonoBehaviour
{
    public static SteamLeaderboards Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject(nameof(SteamLeaderboards));
        DontDestroyOnLoad(go);
        go.AddComponent<SteamLeaderboards>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void UploadScore(string boardSceneName, int score)
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized || Instance == null) return;
        if (string.IsNullOrEmpty(boardSceneName)) return;
        Instance.UploadInternal("HighScore_" + boardSceneName, score);
#endif
    }

#if !DISABLESTEAMWORKS
    private readonly Dictionary<string, SteamLeaderboard_t> _leaderboards =
        new Dictionary<string, SteamLeaderboard_t>();

    private readonly List<(string name, int score)> _pendingUploads =
        new List<(string, int)>();

    private readonly HashSet<string> _findInProgress = new HashSet<string>();
    private readonly List<object> _activeCallResults = new List<object>();

    private void UploadInternal(string leaderboardName, int score)
    {
        if (_leaderboards.TryGetValue(leaderboardName, out var handle))
        {
            DoUpload(handle, score);
            return;
        }

        _pendingUploads.Add((leaderboardName, score));

        if (_findInProgress.Contains(leaderboardName)) return;

        _findInProgress.Add(leaderboardName);
        string captured = leaderboardName;
        var findCall = SteamUserStats.FindLeaderboard(leaderboardName);
        var cr = CallResult<LeaderboardFindResult_t>.Create();
        cr.Set(findCall, (result, ioFailure) => OnLeaderboardFound(result, ioFailure, captured));
        _activeCallResults.Add(cr);
    }

    private void OnLeaderboardFound(LeaderboardFindResult_t result, bool ioFailure, string requestedName)
    {
        _findInProgress.Remove(requestedName);

        if (ioFailure || result.m_bLeaderboardFound == 0)
        {
            Debug.LogWarning($"[SteamLeaderboards] Failed to find leaderboard '{requestedName}'.");
            _pendingUploads.RemoveAll(p => p.name == requestedName);
            return;
        }

        _leaderboards[requestedName] = result.m_hSteamLeaderboard;

        for (int i = _pendingUploads.Count - 1; i >= 0; i--)
        {
            if (_pendingUploads[i].name == requestedName)
            {
                DoUpload(result.m_hSteamLeaderboard, _pendingUploads[i].score);
                _pendingUploads.RemoveAt(i);
            }
        }
    }

    private void DoUpload(SteamLeaderboard_t board, int score)
    {
        SteamUserStats.UploadLeaderboardScore(
            board,
            ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
            score, null, 0);
    }

    /// <summary>
    /// Downloads global top entries for a board. Results are returned via the callback.
    /// </summary>
    public static void DownloadGlobalScores(string boardSceneName, int rangeStart, int rangeEnd,
        System.Action<LeaderboardEntry_t[]> onComplete)
    {
        if (!SteamManager.Initialized || Instance == null) return;
        Instance.DownloadInternal("HighScore_" + boardSceneName,
            ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, rangeStart, rangeEnd, onComplete);
    }

    /// <summary>
    /// Downloads friend entries for a board. Results are returned via the callback.
    /// </summary>
    public static void DownloadFriendScores(string boardSceneName,
        System.Action<LeaderboardEntry_t[]> onComplete)
    {
        if (!SteamManager.Initialized || Instance == null) return;
        Instance.DownloadInternal("HighScore_" + boardSceneName,
            ELeaderboardDataRequest.k_ELeaderboardDataRequestFriends, 0, 0, onComplete);
    }

    private void DownloadInternal(string leaderboardName, ELeaderboardDataRequest requestType,
        int rangeStart, int rangeEnd, System.Action<LeaderboardEntry_t[]> onComplete)
    {
        if (_leaderboards.TryGetValue(leaderboardName, out var handle))
        {
            DoDownload(handle, requestType, rangeStart, rangeEnd, onComplete);
            return;
        }

        string captured = leaderboardName;
        var findCall = SteamUserStats.FindLeaderboard(leaderboardName);
        var cr = CallResult<LeaderboardFindResult_t>.Create();
        cr.Set(findCall, (result, ioFailure) =>
        {
            if (ioFailure || result.m_bLeaderboardFound == 0)
            {
                onComplete?.Invoke(new LeaderboardEntry_t[0]);
                return;
            }

            _leaderboards[captured] = result.m_hSteamLeaderboard;
            DoDownload(result.m_hSteamLeaderboard, requestType, rangeStart, rangeEnd, onComplete);
        });
        _activeCallResults.Add(cr);
    }

    private void DoDownload(SteamLeaderboard_t board, ELeaderboardDataRequest requestType,
        int rangeStart, int rangeEnd, System.Action<LeaderboardEntry_t[]> onComplete)
    {
        var downloadCall = SteamUserStats.DownloadLeaderboardEntries(board, requestType, rangeStart, rangeEnd);
        var cr = CallResult<LeaderboardScoresDownloaded_t>.Create();
        cr.Set(downloadCall, (result, ioFailure) =>
        {
            if (ioFailure || result.m_cEntryCount == 0)
            {
                onComplete?.Invoke(new LeaderboardEntry_t[0]);
                return;
            }

            var entries = new LeaderboardEntry_t[result.m_cEntryCount];
            for (int i = 0; i < result.m_cEntryCount; i++)
            {
                SteamUserStats.GetDownloadedLeaderboardEntry(
                    result.m_hSteamLeaderboardEntries, i, out entries[i], null, 0);
            }

            onComplete?.Invoke(entries);
        });
        _activeCallResults.Add(cr);
    }
#endif
}
