using System;
using UnityEngine;

/// <summary>
/// Holds the active language, resolves localization keys, and notifies listeners
/// (such as <see cref="LocalizedText"/>) whenever the language changes.
/// </summary>
public sealed class LocalizationManager : MonoBehaviour
{
    public const string CatText = "Meow";

    private const string LanguagePrefKey = "settings.language";

    [SerializeField] private LocalizationTable table;
    [SerializeField] private Language defaultLanguage = Language.Vietnamese;

    public static LocalizationManager Instance { get; private set; }

    public Language CurrentLanguage { get; private set; }

    /// <summary>Raised after the active language changes so UI can refresh.</summary>
    public event Action<Language> LanguageChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentLanguage = (Language)PlayerPrefs.GetInt(LanguagePrefKey, (int)defaultLanguage);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Changes the active language and persists the choice.</summary>
    public void SetLanguage(Language language)
    {
        if (CurrentLanguage == language)
        {
            return;
        }

        CurrentLanguage = language;
        PlayerPrefs.SetInt(LanguagePrefKey, (int)language);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke(language);
    }

    /// <summary>
    /// Returns the localized string for <paramref name="key"/> in the active language,
    /// falling back to the key itself when no translation exists.
    /// </summary>
    public string Get(string key)
    {
        if (CurrentLanguage == Language.Cat && !IsCatLanguageExemptKey(key))
        {
            return CatText;
        }

        if (table != null && table.TryGet(key, CurrentLanguage, out string value))
        {
            return value;
        }

        if (CurrentLanguage == Language.Cat)
        {
            return CatText;
        }

        return key;
    }

    public string GetDialogueText(string vietnameseText, string englishText)
    {
        return CurrentLanguage switch
        {
            Language.English => !string.IsNullOrWhiteSpace(englishText) ? englishText : vietnameseText,
            Language.Cat => Catify(vietnameseText),
            _ => vietnameseText
        };
    }

    public string GetDialogueSpeaker(string vietnameseSpeaker, string englishSpeaker)
    {
        return CurrentLanguage switch
        {
            Language.English => !string.IsNullOrWhiteSpace(englishSpeaker) ? englishSpeaker : vietnameseSpeaker,
            Language.Cat => Catify(vietnameseSpeaker),
            _ => vietnameseSpeaker
        };
    }

    public static string GetLanguageDisplayName(Language language)
    {
        return language switch
        {
            Language.English => "English",
            Language.Cat => "Tiếng Mèo",
            _ => "Tiếng Việt"
        };
    }

    public static string Catify(string source)
    {
        return string.IsNullOrWhiteSpace(source) ? string.Empty : CatText;
    }

    private static bool IsCatLanguageExemptKey(string key)
    {
        return key is "language.vietnamese" or "language.english" or "language.cat";
    }
}
