// Generated with Cursor (Composer) by assistant on 2026-03-31 (shared run completion + win screen).
using System;

/// <summary>
/// Centralizes profile/progression updates and win presentation when a run ends in success.
/// </summary>
public static class RunCompletionHelper
{
    /// <summary>
    /// Records run completion, checks unlocks, then shows the win screen.
    /// Hooks preserve ordering when callers need work after the run is recorded or before the win UI.
    /// </summary>
    public static void RecordProgressAndShowWinScreen(
        int levelReached,
        long points,
        Action afterRecordBeforeUnlocks = null,
        Action beforeShowWin = null)
    {
        ProfileService.RecordRunCompleted();
        afterRecordBeforeUnlocks?.Invoke();
        ProgressionService.Instance?.CheckAndGrantUnlocks();

        SteamAchievements.UnlockFirstWin();
        var board = GameSession.Instance?.GetCurrentBoard();
        if (board != null)
        {
            SteamAchievements.UnlockBoardWin(board.boardSceneName);
            SteamLeaderboards.UploadScore(board.boardSceneName,
                (int)Math.Min(points, int.MaxValue));
        }

        beforeShowWin?.Invoke();
        WinScreenController.Show(levelReached, points);
    }
}
