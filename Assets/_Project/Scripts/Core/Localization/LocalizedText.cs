using TMPro;
using UnityEngine;

/// <summary>
/// Attach to any TMP_Text to keep it in sync with the active language.
/// The label is resolved from <see cref="LocalizationManager"/> using <see cref="key"/>.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedText : MonoBehaviour
{
    [SerializeField] private string key;

    private TMP_Text _text;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
        }
    }

    /// <summary>Sets the localization key at runtime and refreshes the label.</summary>
    public void SetKey(string localizationKey)
    {
        key = localizationKey;
        Refresh();
    }

    private void OnLanguageChanged(Language language)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_text == null || LocalizationManager.Instance == null)
        {
            return;
        }

        _text.text = LocalizationManager.Instance.Get(key);
    }
}
