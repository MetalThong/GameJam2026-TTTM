using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the in-game settings panel: music/SFX volume sliders, a language selector
/// (Vietnamese / English / Cat), a close button, and a "quit to main menu" button.
/// Volumes persist via PlayerPrefs and are applied to <see cref="AudioManager"/>.
/// </summary>
public sealed class SettingsPanelController : MonoBehaviour
{
    private const string MusicVolumePrefKey = "settings.musicVolume";
    private const string SfxVolumePrefKey = "settings.sfxVolume";
    private const float DefaultVolume = 0.8f;

    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("Volume")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Language")]
    [Tooltip("Buttons in this exact order: Vietnamese, English, Cat.")]
    [SerializeField] private Button vietnameseButton;
    [SerializeField] private Button englishButton;
    [SerializeField] private Button catButton;

    [Header("Quit")]
    [SerializeField] private Button quitToMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void OnEnable()
    {
        if (openButton != null)
        {
            openButton.onClick.AddListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (vietnameseButton != null)
        {
            vietnameseButton.onClick.AddListener(SelectVietnamese);
        }

        if (englishButton != null)
        {
            englishButton.onClick.AddListener(SelectEnglish);
        }

        if (catButton != null)
        {
            catButton.onClick.AddListener(SelectCat);
        }

        if (quitToMenuButton != null)
        {
            quitToMenuButton.onClick.AddListener(QuitToMainMenu);
        }

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }
    }

    private void Start()
    {
        InitializeVolume();
        RefreshLanguageButtons();
        Close();
    }

    private void OnDisable()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        }

        if (vietnameseButton != null)
        {
            vietnameseButton.onClick.RemoveListener(SelectVietnamese);
        }

        if (englishButton != null)
        {
            englishButton.onClick.RemoveListener(SelectEnglish);
        }

        if (catButton != null)
        {
            catButton.onClick.RemoveListener(SelectCat);
        }

        if (quitToMenuButton != null)
        {
            quitToMenuButton.onClick.RemoveListener(QuitToMainMenu);
        }

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
        }
    }

    public void Open()
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    public void Close()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void InitializeVolume()
    {
        float musicVolume = PlayerPrefs.GetFloat(MusicVolumePrefKey, DefaultVolume);
        float sfxVolume = PlayerPrefs.GetFloat(SfxVolumePrefKey, DefaultVolume);

        // SetValueWithoutNotify avoids firing the change handler (which would re-save)
        // before the AudioManager has been asked to apply the persisted value.
        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(musicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(sfxVolume);
        }

        ApplyMusicVolume(musicVolume);
        ApplySfxVolume(sfxVolume);
    }

    private void OnMusicVolumeChanged(float value)
    {
        ApplyMusicVolume(value);
        PlayerPrefs.SetFloat(MusicVolumePrefKey, value);
        PlayerPrefs.Save();
    }

    private void OnSfxVolumeChanged(float value)
    {
        ApplySfxVolume(value);
        PlayerPrefs.SetFloat(SfxVolumePrefKey, value);
        PlayerPrefs.Save();
    }

    private void SelectVietnamese() => SelectLanguage(Language.Vietnamese);

    private void SelectEnglish() => SelectLanguage(Language.English);

    private void SelectCat() => SelectLanguage(Language.Cat);

    private void SelectLanguage(Language language)
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.SetLanguage(language);
        }
    }

    private void OnLanguageChanged(Language language)
    {
        RefreshLanguageButtons();
    }

    /// <summary>
    /// Greys out the button of the active language so the current choice is visible.
    /// </summary>
    private void RefreshLanguageButtons()
    {
        Language current = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.CurrentLanguage
            : Language.Vietnamese;

        SetButtonSelected(vietnameseButton, current == Language.Vietnamese);
        SetButtonSelected(englishButton, current == Language.English);
        SetButtonSelected(catButton, current == Language.Cat);
    }

    private static void SetButtonSelected(Button button, bool selected)
    {
        if (button != null)
        {
            // The active language is non-interactable so it reads as "pressed/selected".
            button.interactable = !selected;
        }
    }

    private void QuitToMainMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.QuitToMenu();
        }

        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private static void ApplyMusicVolume(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
    }

    private static void ApplySfxVolume(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSfxVolume(value);
        }
    }
}
