using UnityEngine.Localization.Settings;

/// <summary>
/// Resolves runtime-set UI strings from the "Gameplay" string table (the same table the
/// static LocalizeStringEvent components bind to). Use for text a script writes to
/// <c>.text</c>, which LSE cannot handle. Templates keep their placeholders ("{0}") in the
/// table as plain strings and are filled via <see cref="Format"/> using C# String.Format —
/// so entries do NOT need the Smart String flag enabled.
/// </summary>
public static class LocalizedUI
{
    private const string Table = "Gameplay";

    /// <summary>Localized string for <paramref name="key"/>, or <paramref name="fallback"/>.</summary>
    public static string Get(string key, string fallback)
    {
        if (!LocalizationSettings.HasSettings)
        {
            return fallback;
        }

        try
        {
            var entry = LocalizationSettings.StringDatabase.GetTableEntry(Table, key).Entry;
            if (entry == null)
            {
                return fallback;
            }

            var s = entry.GetLocalizedString();
            return string.IsNullOrEmpty(s) ? fallback : s;
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Localized template for <paramref name="key"/> (or <paramref name="fallbackTemplate"/>),
    /// formatted with <paramref name="args"/> via String.Format. Both the localized value and
    /// the fallback are expected to contain matching "{0}"/"{1}" placeholders.
    /// </summary>
    public static string Format(string key, string fallbackTemplate, params object[] args)
    {
        var template = Get(key, fallbackTemplate);
        if (args == null || args.Length == 0)
        {
            return template;
        }

        try
        {
            return string.Format(template, args);
        }
        catch
        {
            // Localized template had bad/mismatched placeholders — fall back to source.
            try { return string.Format(fallbackTemplate, args); }
            catch { return fallbackTemplate; }
        }
    }
}
