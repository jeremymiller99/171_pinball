#if !DISABLESTEAMWORKS
using Steamworks;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Steamworks-free view of a leaderboard row so UI code compiles without Steam.
/// </summary>
public struct SteamLeaderboardEntry
{
    public int rank;
    public ulong steamId;
    public string playerName;
    public int score;
    public int levelReached;
    public bool isLocalUser;
}

public class SteamLeaderboards : MonoBehaviour
{
    public static SteamLeaderboards Instance { get; private set; }

    /// <summary>Raised when a previously unknown player name has been resolved.</summary>
    public static event Action PlayerNamesUpdated;

    /// <summary>Raised when Steam confirms a score upload landed on a leaderboard.</summary>
    public static event Action ScoreUploaded;

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

    public static bool IsAvailable
    {
        get
        {
#if !DISABLESTEAMWORKS
            return SteamManager.Initialized && Instance != null;
#else
            return false;
#endif
        }
    }

    public static void UploadScore(string boardSceneName, int score, int levelReached)
    {
#if !DISABLESTEAMWORKS
        if (!IsAvailable) return;
        if (string.IsNullOrEmpty(boardSceneName)) return;
        Instance.UploadInternal("HighScore_" + boardSceneName, score, levelReached);
#endif
    }

    /// <summary>
    /// Downloads global top entries for a board. Results arrive via the callback;
    /// an empty list means no entries or Steam unavailable.
    /// </summary>
    public static void DownloadGlobalScores(string boardSceneName, int rangeStart, int rangeEnd,
        Action<List<SteamLeaderboardEntry>> onComplete)
    {
#if !DISABLESTEAMWORKS
        if (!IsAvailable)
        {
            onComplete?.Invoke(new List<SteamLeaderboardEntry>());
            return;
        }
        Instance.DownloadInternal("HighScore_" + boardSceneName,
            ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, rangeStart, rangeEnd, onComplete);
#else
        onComplete?.Invoke(new List<SteamLeaderboardEntry>());
#endif
    }

    /// <summary>
    /// Downloads friend entries for a board (always includes the local user).
    /// </summary>
    public static void DownloadFriendScores(string boardSceneName,
        Action<List<SteamLeaderboardEntry>> onComplete)
    {
#if !DISABLESTEAMWORKS
        if (!IsAvailable)
        {
            onComplete?.Invoke(new List<SteamLeaderboardEntry>());
            return;
        }
        Instance.DownloadInternal("HighScore_" + boardSceneName,
            ELeaderboardDataRequest.k_ELeaderboardDataRequestFriends, 0, 0, onComplete);
#else
        onComplete?.Invoke(new List<SteamLeaderboardEntry>());
#endif
    }

    /// <summary>
    /// Downloads entries centered on the local user, which carries their global rank.
    /// </summary>
    public static void DownloadScoresAroundUser(string boardSceneName, int range,
        Action<List<SteamLeaderboardEntry>> onComplete)
    {
#if !DISABLESTEAMWORKS
        if (!IsAvailable)
        {
            onComplete?.Invoke(new List<SteamLeaderboardEntry>());
            return;
        }
        Instance.DownloadInternal("HighScore_" + boardSceneName,
            ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser, -range, range, onComplete);
#else
        onComplete?.Invoke(new List<SteamLeaderboardEntry>());
#endif
    }

#if !DISABLESTEAMWORKS
    private readonly Dictionary<string, SteamLeaderboard_t> _leaderboards =
        new Dictionary<string, SteamLeaderboard_t>();

    private readonly List<(string name, int score, int level)> _pendingUploads =
        new List<(string, int, int)>();

    private readonly HashSet<string> _findInProgress = new HashSet<string>();
    private readonly List<object> _activeCallResults = new List<object>();
    private readonly HashSet<ulong> _nameRequests = new HashSet<ulong>();

    private Callback<PersonaStateChange_t> _personaStateChange;

    private void UploadInternal(string leaderboardName, int score, int level)
    {
        if (_leaderboards.TryGetValue(leaderboardName, out var handle))
        {
            DoUpload(handle, score, level);
            return;
        }

        _pendingUploads.Add((leaderboardName, score, level));

        if (_findInProgress.Contains(leaderboardName)) return;

        _findInProgress.Add(leaderboardName);
        string captured = leaderboardName;
        var findCall = SteamUserStats.FindOrCreateLeaderboard(leaderboardName,
            ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
            ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);
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
                DoUpload(result.m_hSteamLeaderboard, _pendingUploads[i].score, _pendingUploads[i].level);
                _pendingUploads.RemoveAt(i);
            }
        }
    }

    private void DoUpload(SteamLeaderboard_t board, int score, int level)
    {
        int[] details = { level };
        var uploadCall = SteamUserStats.UploadLeaderboardScore(
            board,
            ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
            score, details, details.Length);
        var cr = CallResult<LeaderboardScoreUploaded_t>.Create();
        cr.Set(uploadCall, (result, ioFailure) =>
        {
            if (ioFailure || result.m_bSuccess == 0) return;

            ScoreUploaded?.Invoke();
        });
        _activeCallResults.Add(cr);
    }

    private void DownloadInternal(string leaderboardName, ELeaderboardDataRequest requestType,
        int rangeStart, int rangeEnd, Action<List<SteamLeaderboardEntry>> onComplete)
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
                onComplete?.Invoke(new List<SteamLeaderboardEntry>());
                return;
            }

            _leaderboards[captured] = result.m_hSteamLeaderboard;
            DoDownload(result.m_hSteamLeaderboard, requestType, rangeStart, rangeEnd, onComplete);
        });
        _activeCallResults.Add(cr);
    }

    private void DoDownload(SteamLeaderboard_t board, ELeaderboardDataRequest requestType,
        int rangeStart, int rangeEnd, Action<List<SteamLeaderboardEntry>> onComplete)
    {
        var downloadCall = SteamUserStats.DownloadLeaderboardEntries(board, requestType, rangeStart, rangeEnd);
        var cr = CallResult<LeaderboardScoresDownloaded_t>.Create();
        cr.Set(downloadCall, (result, ioFailure) =>
        {
            var entries = new List<SteamLeaderboardEntry>();
            if (ioFailure || result.m_cEntryCount == 0)
            {
                onComplete?.Invoke(entries);
                return;
            }

            CSteamID localId = SteamUser.GetSteamID();
            int[] details = new int[1];

            for (int i = 0; i < result.m_cEntryCount; i++)
            {
                if (!SteamUserStats.GetDownloadedLeaderboardEntry(
                        result.m_hSteamLeaderboardEntries, i, out var raw, details, details.Length))
                {
                    continue;
                }

                entries.Add(new SteamLeaderboardEntry
                {
                    rank = raw.m_nGlobalRank,
                    steamId = raw.m_steamIDUser.m_SteamID,
                    playerName = ResolvePlayerName(raw.m_steamIDUser),
                    score = raw.m_nScore,
                    levelReached = raw.m_cDetails > 0 ? details[0] : 0,
                    isLocalUser = raw.m_steamIDUser == localId
                });
            }

            onComplete?.Invoke(entries);
        });
        _activeCallResults.Add(cr);
    }

    private string ResolvePlayerName(CSteamID user)
    {
        // Registered lazily: this only runs after a download, so Steam is initialized.
        if (_personaStateChange == null)
        {
            _personaStateChange = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);
        }

        // Steam only knows names of friends and recently seen players; request the
        // rest and refresh rows via PlayerNamesUpdated when the data arrives.
        if (SteamFriends.RequestUserInformation(user, true))
        {
            _nameRequests.Add(user.m_SteamID);
            return "...";
        }

        return SteamFriends.GetFriendPersonaName(user);
    }

    private void OnPersonaStateChange(PersonaStateChange_t data)
    {
        if (!_nameRequests.Remove(data.m_ulSteamID)) return;

        PlayerNamesUpdated?.Invoke();
    }
#endif

    /// <summary>Best-known display name for a user; valid once entries have been downloaded.</summary>
    public static string GetPlayerName(ulong steamId)
    {
#if !DISABLESTEAMWORKS
        if (!IsAvailable) return "";
        return SteamFriends.GetFriendPersonaName(new CSteamID(steamId));
#else
        return "";
#endif
    }
}
