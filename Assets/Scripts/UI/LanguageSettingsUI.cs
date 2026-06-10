// Created with Claude Code (Opus 4.8) by JJ on 2026-06-09: language dropdown
// driving the Unity Localization selected locale, with persistence across runs.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Binds a <see cref="TMP_Dropdown"/> to the project's localization locales so the
/// player can switch language at runtime. Selecting an option sets
/// <see cref="LocalizationSettings.SelectedLocale"/> (which re-resolves every
/// LocalizeStringEvent and the <c>LocalizedUI</c>/<c>LocalizedContent</c> helpers)
/// and persists the choice to PlayerPrefs. The saved locale is re-applied on the
/// next launch via <see cref="ApplySavedLocale"/>, since the project's startup
/// selectors otherwise default back to English each run.
/// </summary>
public class LanguageSettingsUI : MonoBehaviour
{
    // Shared with ApplySavedLocale; stores the saved locale's identifier code (e.g. "en", "es").
    private const string LocaleKey = "SelectedLocaleCode";

    [Header("UI Dropdowns")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    private bool initialized;

    // Locales in the same order they appear in the dropdown, so the dropdown index
    // maps straight back to a Locale.
    private readonly List<Locale> locales = new List<Locale>();

    // Re-apply the player's saved language as the game boots, before scene
    // LocalizeStringEvents resolve, so content loads in the right language without
    // a visible flicker from the English default.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedLocale()
    {
        if (!LocalizationSettings.HasSettings)
        {
            return;
        }

        string code = PlayerPrefs.GetString(LocaleKey, string.Empty);
        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        var init = LocalizationSettings.InitializationOperation;
        if (init.IsDone)
        {
            SelectLocaleByCode(code);
        }
        else
        {
            init.Completed += _ => SelectLocaleByCode(code);
        }
    }

    private static void SelectLocaleByCode(string code)
    {
        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
        }
    }

    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (initialized)
        {
            RefreshSelection();
        }
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;

        if (!LocalizationSettings.HasSettings)
        {
            Debug.LogWarning($"{nameof(LanguageSettingsUI)}: no LocalizationSettings in the project; language dropdown disabled.", this);
            if (languageDropdown != null)
            {
                languageDropdown.interactable = false;
            }
            return;
        }

        // AvailableLocales / SelectedLocale aren't reliable until localization has
        // finished initializing, so defer populating until then.
        var init = LocalizationSettings.InitializationOperation;
        if (init.IsDone)
        {
            PopulateLanguageDropdown();
        }
        else
        {
            init.Completed += _ => PopulateLanguageDropdown();
        }

        // Keep the dropdown in sync if the locale is changed from elsewhere.
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChangedExternally;

        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        }
    }

    private void PopulateLanguageDropdown()
    {
        if (languageDropdown == null) return;

        locales.Clear();
        IList<Locale> available = LocalizationSettings.AvailableLocales.Locales;

        var options = new List<TMP_Dropdown.OptionData>(available.Count);
        for (int i = 0; i < available.Count; i++)
        {
            Locale locale = available[i];
            locales.Add(locale);
            options.Add(new TMP_Dropdown.OptionData(GetDisplayName(locale)));
        }

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(options);

        RefreshSelection();
    }

    // Prefer the culture's native name ("English", "Español"); fall back to the
    // locale's authored name, then its identifier code.
    private static string GetDisplayName(Locale locale)
    {
        if (locale == null) return string.Empty;

        var culture = locale.Identifier.CultureInfo;
        if (culture != null && !string.IsNullOrEmpty(culture.NativeName))
        {
            return culture.NativeName;
        }

        return string.IsNullOrEmpty(locale.LocaleName)
            ? locale.Identifier.Code
            : locale.LocaleName;
    }

    private void RefreshSelection()
    {
        if (languageDropdown == null || locales.Count == 0) return;

        Locale selected = LocalizationSettings.SelectedLocale;
        int index = selected != null ? locales.IndexOf(selected) : -1;
        if (index >= 0)
        {
            languageDropdown.SetValueWithoutNotify(index);
        }
    }

    private void OnLanguageChanged(int index)
    {
        if (index < 0 || index >= locales.Count) return;

        Locale locale = locales[index];
        LocalizationSettings.SelectedLocale = locale;

        PlayerPrefs.SetString(LocaleKey, locale.Identifier.Code);
        PlayerPrefs.Save();
    }

    private void OnLocaleChangedExternally(Locale locale)
    {
        RefreshSelection();
    }

    private void OnDestroy()
    {
        if (LocalizationSettings.HasSettings)
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChangedExternally;
        }

        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
        }
    }
}
