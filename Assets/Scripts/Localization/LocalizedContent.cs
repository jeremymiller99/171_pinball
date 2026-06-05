using UnityEngine.Localization.Settings;

/// <summary>
/// Resolves data-driven content strings (ScriptableObject display names / descriptions)
/// from the "Content" string table by a stable key, with an English fallback to the raw
/// serialized field. Key convention: "&lt;kind&gt;.&lt;assetName&gt;.&lt;field&gt;"
/// e.g. "ball.8_Ball.desc", "component.Fire Bumper.name".
/// The asset name is the ScriptableObject's <c>name</c> (its file name), so every
/// definition type resolves uniformly whether or not it exposes a serialized id.
/// </summary>
public static class LocalizedContent
{
    private const string Table = "Content";

    /// <summary>
    /// Returns the localized string for the given content key, or <paramref name="fallback"/>
    /// (the raw serialized field) when localization is unavailable or the key is missing in
    /// the selected locale. Never returns null/empty as long as the fallback is non-empty.
    /// </summary>
    public static string Get(string kind, string assetName, string field, string fallback)
    {
        if (string.IsNullOrEmpty(assetName) || string.IsNullOrEmpty(field))
        {
            return fallback;
        }

        // LocalizationSettings is unavailable in edit mode / before init and throws if poked.
        if (!LocalizationSettings.HasSettings)
        {
            return fallback;
        }

        try
        {
            var key = $"{kind}.{assetName}.{field}";
            var entry = LocalizationSettings.StringDatabase.GetTableEntry(Table, key).Entry;
            if (entry == null)
            {
                return fallback;
            }

            var localized = entry.GetLocalizedString();
            return string.IsNullOrEmpty(localized) ? fallback : localized;
        }
        catch
        {
            // Table not loaded yet, missing collection, etc. — degrade to source text.
            return fallback;
        }
    }
}
