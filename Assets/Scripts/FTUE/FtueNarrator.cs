// Created by Claude Code (claude-opus-5) for jjmil on 2026-08-08 (FTUE narrator identity).
using System.Text;

/// <summary>
/// Owns who the assisting AI is. The player names it during the FTUE and that name is rendered in
/// every line afterwards, so this is the one place that decides what a valid name is and what
/// happens when there is not one.
///
/// It lives in the profile rather than in <see cref="FtueState"/> because the name outlives the
/// tutorial — the main menu will eventually be this character, long after the FTUE has been
/// cleared and its state reset.
///
/// The stored name is player-authored text. It must never reach analytics, Steam, or a log line.
/// </summary>
public static class FtueNarrator
{
    /// <summary>
    /// Longest name accepted. It appears in every dialogue box, so a long one wrecks the panel
    /// layout. The input field enforces this as the player types; <see cref="Sanitize"/> enforces
    /// it again, because the field is a convenience and this is the actual rule.
    /// </summary>
    public const int nameCharacterLimit = 16;

    /// <summary>
    /// Used when the player skips the naming beat or submits nothing. "Al" reads as a name and is
    /// near-indistinguishable from "AI" in most sans-serif faces, which is the joke.
    ///
    /// It is intentional and must not be "corrected" to "AI" — see the localization note on the
    /// naming beat's string key.
    /// </summary>
    public const string fallbackName = "Al";

    /// <summary>
    /// The name to show. Always returns something renderable, so callers never have to null-check
    /// or fall back themselves.
    /// </summary>
    public static string DisplayName => Sanitize(ProfileService.GetAiName());

    /// <summary>True once the player has actually chosen a name of their own.</summary>
    public static bool HasPlayerChosenName => !string.IsNullOrEmpty(Clean(ProfileService.GetAiName()));

    /// <summary>
    /// Stores the player's choice, sanitized. Passing null, blank, or something that sanitizes
    /// away entirely stores nothing, which leaves <see cref="DisplayName"/> on the fallback.
    /// </summary>
    public static void SetName(string rawName)
    {
        ProfileService.SetAiName(Clean(rawName));
    }

    /// <summary>
    /// Cleaned name, or <see cref="fallbackName"/> if nothing usable survives.
    /// </summary>
    public static string Sanitize(string rawName)
    {
        string cleaned = Clean(rawName);
        return string.IsNullOrEmpty(cleaned) ? fallbackName : cleaned;
    }

    /// <summary>
    /// Strips what must not be stored and clamps the length. Returns empty when nothing usable is
    /// left — the caller decides whether empty means "fall back" or "store nothing".
    /// </summary>
    private static string Clean(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

        var builder = new StringBuilder(rawName.Length);

        for (int i = 0; i < rawName.Length; i++)
        {
            char c = rawName[i];

            // Control characters would render as tofu or break the layout outright.
            if (char.IsControl(c)) continue;

            // TMP parses rich text by default, and this string is written straight into a label.
            // A name of "<size=500%>" would blow the panel apart, and "<sprite=0>" would draw
            // something the player never typed. Dropping the brackets makes the tag inert while
            // still letting the rest of the name through.
            if (c == '<' || c == '>') continue;

            builder.Append(c);
        }

        string cleaned = builder.ToString().Trim();

        if (cleaned.Length > nameCharacterLimit)
        {
            cleaned = cleaned.Substring(0, nameCharacterLimit).TrimEnd();
        }

        return cleaned;
    }
}
