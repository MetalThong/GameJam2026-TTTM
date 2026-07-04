using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuSettingsPanel : MonoBehaviour
{
    private const string MusicVolumePrefKey = "settings.musicVolume";
    private const string SfxVolumePrefKey = "settings.sfxVolume";
    private const string DeterminationFontResourcePath = "Text/SVN-Determination Sans SDF";
    private const float DefaultVolume = 0.8f;

    private static readonly Color LanguageNormalColor = new(0.16f, 0.18f, 0.2f, 1f);
    private static readonly Color LanguageSelectedColor = new(0.3f, 0.72f, 0.44f, 1f);
    private static readonly Color LanguageNormalTextColor = Color.white;
    private static readonly Color LanguageSelectedTextColor = new(0.04f, 0.08f, 0.06f, 1f);
    private static readonly Color LanguageSelectedOutlineColor = new(0.85f, 1f, 0.72f, 1f);

    [SerializeField] private GameObject panel;

    private Slider _musicSlider;
    private Slider _sfxSlider;
    private Button _vietnameseButton;
    private Button _englishButton;
    private Button _catButton;
    private TMP_Text _titleText;
    private TMP_Text _musicLabel;
    private TMP_Text _sfxLabel;
    private TMP_Text _languageLabel;
    private bool _isInitialized;
    private bool _hasListeners;

    public void SetPanel(GameObject panelObject)
    {
        if (panelObject == null)
        {
            return;
        }

        panel = panelObject;
        Initialize();

        if (isActiveAndEnabled)
        {
            AddListeners();
        }
    }

    private void OnEnable()
    {
        Initialize();
        AddListeners();

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }
    }

    private void Start()
    {
        InitializeVolume();
        RefreshTexts();
        RefreshLanguageButtons();
        Close();
    }

    private void OnDisable()
    {
        RemoveListeners();

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

    private void Initialize()
    {
        if (_isInitialized || panel == null)
        {
            return;
        }

        RectTransform window = FindChildRect(panel.transform, "SettingsWindow");
        if (window == null)
        {
            window = panel.transform as RectTransform;
        }

        TMP_FontAsset sharedFont = ResolveSharedFont(panel);

        _titleText = FindChildText(panel.transform, "SettingsTitle");
        ApplyFont(_titleText, sharedFont);
        HideLegacyPlaceholder(panel.transform);

        _musicLabel = CreateLabel(window, "MusicLabel", new Vector2(42f, -92f), sharedFont);
        _musicSlider = CreateSlider(window, "MusicSlider", new Vector2(42f, -130f));

        _sfxLabel = CreateLabel(window, "SfxLabel", new Vector2(42f, -166f), sharedFont);
        _sfxSlider = CreateSlider(window, "SfxSlider", new Vector2(42f, -204f));

        _languageLabel = CreateLabel(window, "LanguageLabel", new Vector2(42f, -240f), sharedFont);
        _vietnameseButton = CreateLanguageButton(window, "VietnameseButton", LocalizationManager.GetLanguageDisplayName(Language.Vietnamese), new Vector2(42f, -290f), sharedFont);
        _englishButton = CreateLanguageButton(window, "EnglishButton", LocalizationManager.GetLanguageDisplayName(Language.English), new Vector2(215f, -290f), sharedFont);
        _catButton = CreateLanguageButton(window, "CatButton", LocalizationManager.GetLanguageDisplayName(Language.Cat), new Vector2(388f, -290f), sharedFont);

        InitializeVolume();
        RefreshTexts();
        RefreshLanguageButtons();
        _isInitialized = true;
    }

    private void AddListeners()
    {
        if (!_isInitialized || _hasListeners)
        {
            return;
        }

        _musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        _sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        AudioFeedback.AddButtonClick(_vietnameseButton);
        AudioFeedback.AddButtonClick(_englishButton);
        AudioFeedback.AddButtonClick(_catButton);
        _vietnameseButton.onClick.AddListener(SelectVietnamese);
        _englishButton.onClick.AddListener(SelectEnglish);
        _catButton.onClick.AddListener(SelectCat);
        _hasListeners = true;
    }

    private void RemoveListeners()
    {
        if (!_isInitialized || !_hasListeners)
        {
            return;
        }

        _musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        _sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        AudioFeedback.RemoveButtonClick(_vietnameseButton);
        AudioFeedback.RemoveButtonClick(_englishButton);
        AudioFeedback.RemoveButtonClick(_catButton);
        _vietnameseButton.onClick.RemoveListener(SelectVietnamese);
        _englishButton.onClick.RemoveListener(SelectEnglish);
        _catButton.onClick.RemoveListener(SelectCat);
        _hasListeners = false;
    }

    private void InitializeVolume()
    {
        if (!_isInitialized)
        {
            return;
        }

        float musicVolume = PlayerPrefs.GetFloat(MusicVolumePrefKey, DefaultVolume);
        float sfxVolume = PlayerPrefs.GetFloat(SfxVolumePrefKey, DefaultVolume);

        _musicSlider.SetValueWithoutNotify(musicVolume);
        _sfxSlider.SetValueWithoutNotify(sfxVolume);
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

    private void SelectVietnamese()
    {
        SelectLanguage(Language.Vietnamese);
    }

    private void SelectEnglish()
    {
        SelectLanguage(Language.English);
    }

    private void SelectCat()
    {
        SelectLanguage(Language.Cat);
    }

    private void SelectLanguage(Language language)
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.SetLanguage(language);
        }

        RefreshTexts();
        RefreshLanguageButtons();
    }

    private void OnLanguageChanged(Language language)
    {
        RefreshTexts();
        RefreshLanguageButtons();
    }

    private void RefreshTexts()
    {
        if (!_isInitialized)
        {
            return;
        }

        SetText(_titleText, GetText("settings.title", "C\u00E0i \u0111\u1EB7t"));
        SetText(_musicLabel, GetText("settings.music", "Nh\u1EA1c n\u1EC1n"));
        SetText(_sfxLabel, GetText("settings.sfx", "Hi\u1EC7u \u1EE9ng"));
        SetText(_languageLabel, GetText("settings.language", "Ng\u00F4n ng\u1EEF"));
    }

    private void RefreshLanguageButtons()
    {
        if (!_isInitialized)
        {
            return;
        }

        Language current = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.CurrentLanguage
            : Language.Vietnamese;

        SetLanguageButton(_vietnameseButton, Language.Vietnamese, current == Language.Vietnamese);
        SetLanguageButton(_englishButton, Language.English, current == Language.English);
        SetLanguageButton(_catButton, Language.Cat, current == Language.Cat);
    }

    private static void SetLanguageButton(Button button, Language language, bool selected)
    {
        SetButtonText(button, LocalizationManager.GetLanguageDisplayName(language));
        SetButtonSelected(button, selected);
    }

    private static void SetButtonSelected(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        button.interactable = !selected;

        if (button.targetGraphic is Image image)
        {
            image.color = selected ? LanguageSelectedColor : LanguageNormalColor;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = LanguageNormalColor;
        colors.highlightedColor = new Color(0.19f, 0.21f, 0.24f, 1f);
        colors.pressedColor = new Color(0.13f, 0.15f, 0.16f, 1f);
        colors.selectedColor = LanguageSelectedColor;
        colors.disabledColor = LanguageSelectedColor;
        button.colors = colors;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = selected ? LanguageSelectedTextColor : LanguageNormalTextColor;
            label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
        {
            outline = button.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(3f, -3f);
        }

        outline.effectColor = LanguageSelectedOutlineColor;
        outline.enabled = selected;
    }

    private static void SetButtonText(Button button, string value)
    {
        TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (text != null)
        {
            text.text = VietnameseTextUtility.Normalize(value);
        }
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = VietnameseTextUtility.Normalize(value);
        }
    }

    private static string GetText(string key, string fallback)
    {
        return LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(key)
            : fallback;
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

    private static void HideLegacyPlaceholder(Transform root)
    {
        Transform placeholder = FindChild(root, "Placeholder");
        if (placeholder != null)
        {
            placeholder.gameObject.SetActive(false);
        }
    }

    private static TMP_Text FindChildText(Transform root, string childName)
    {
        Transform child = FindChild(root, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static TMP_FontAsset ResolveSharedFont(GameObject root)
    {
        TMP_FontAsset determinationFont = Resources.Load<TMP_FontAsset>(DeterminationFontResourcePath);
        if (determinationFont != null)
        {
            return determinationFont;
        }

        TMP_Text[] texts = root != null
            ? root.GetComponentsInChildren<TMP_Text>(includeInactive: true)
            : Array.Empty<TMP_Text>();

        return texts.Length > 0 ? texts[0].font : null;
    }

    private static void ApplyFont(TMP_Text text, TMP_FontAsset font)
    {
        if (text != null && font != null)
        {
            text.font = font;
        }
    }

    private static RectTransform FindChildRect(Transform root, string childName)
    {
        Transform child = FindChild(root, childName);
        return child != null ? child as RectTransform : null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChild(root.GetChild(i), childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static TMP_Text CreateLabel(RectTransform parent, string name, Vector2 anchoredPosition, TMP_FontAsset font)
    {
        GameObject labelObject = new(name);
        labelObject.transform.SetParent(parent, worldPositionStays: false);

        RectTransform rectTransform = labelObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(-84f, 30f);

        TMP_Text text = labelObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = 22f;
        text.color = new Color(0.9f, 0.93f, 0.96f, 1f);
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Left;
        return text;
    }

    private static Slider CreateSlider(RectTransform parent, string name, Vector2 anchoredPosition)
    {
        GameObject sliderObject = new(name);
        sliderObject.transform.SetParent(parent, worldPositionStays: false);

        RectTransform sliderRect = sliderObject.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 1f);
        sliderRect.anchorMax = new Vector2(1f, 1f);
        sliderRect.pivot = new Vector2(0f, 1f);
        sliderRect.anchoredPosition = anchoredPosition;
        sliderRect.sizeDelta = new Vector2(-84f, 20f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = DefaultVolume;

        RectTransform background = CreateSliderImage(sliderRect, "Background", new Color(0.08f, 0.1f, 0.14f, 1f));
        background.anchorMin = new Vector2(0f, 0.25f);
        background.anchorMax = new Vector2(1f, 0.75f);
        background.sizeDelta = Vector2.zero;

        RectTransform fillArea = CreateRect(sliderRect, "Fill Area");
        fillArea.anchorMin = new Vector2(0f, 0.25f);
        fillArea.anchorMax = new Vector2(1f, 0.75f);
        fillArea.anchoredPosition = new Vector2(-5f, 0f);
        fillArea.sizeDelta = new Vector2(-20f, 0f);

        RectTransform fill = CreateSliderImage(fillArea, "Fill", new Color(0.35f, 0.7f, 0.95f, 1f));
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.sizeDelta = new Vector2(10f, 0f);

        RectTransform handleArea = CreateRect(sliderRect, "Handle Slide Area");
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.sizeDelta = new Vector2(-20f, 0f);

        RectTransform handle = CreateSliderImage(handleArea, "Handle", Color.white);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(20f, 0f);

        slider.targetGraphic = handle.GetComponent<Image>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        return slider;
    }

    private static Button CreateLanguageButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, TMP_FontAsset font)
    {
        GameObject buttonObject = new(name);
        buttonObject.transform.SetParent(parent, worldPositionStays: false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(150f, 48f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = LanguageNormalColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = LanguageNormalColor;
        colors.highlightedColor = new Color(0.19f, 0.21f, 0.24f, 1f);
        colors.pressedColor = new Color(0.13f, 0.15f, 0.16f, 1f);
        colors.selectedColor = LanguageSelectedColor;
        colors.disabledColor = LanguageSelectedColor;
        button.colors = colors;

        TMP_Text text = CreateButtonText(rectTransform, label, font);
        text.color = LanguageNormalTextColor;
        return button;
    }

    private static TMP_Text CreateButtonText(RectTransform parent, string label, TMP_FontAsset font)
    {
        GameObject textObject = new("Label");
        textObject.transform.SetParent(parent, worldPositionStays: false);

        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;

        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            text.font = font;
        }

        text.text = label;
        text.fontSize = 20f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(RectTransform parent, string name)
    {
        GameObject gameObject = new(name);
        gameObject.transform.SetParent(parent, worldPositionStays: false);

        RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.anchoredPosition = Vector2.zero;
        return rectTransform;
    }

    private static RectTransform CreateSliderImage(RectTransform parent, string name, Color color)
    {
        RectTransform rectTransform = CreateRect(parent, name);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        return rectTransform;
    }
}
