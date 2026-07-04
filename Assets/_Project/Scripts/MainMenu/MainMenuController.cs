using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button closeSettingsButton;

    [Header("Legacy Scene References")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private string gameplaySceneName = "BedRoom";

    [Header("Runtime Components")]
    [SerializeField] private MainMenuGameFlow gameFlow;
    [SerializeField] private MainMenuSettingsPanel settingsPanelView;

    private void Awake()
    {
        ResolveComponents();
    }

    private void OnEnable()
    {
        if (startButton != null)
        {
            AudioFeedback.AddButtonClick(startButton);
            startButton.onClick.AddListener(gameFlow.StartNewGame);
        }

        if (continueButton != null)
        {
            AudioFeedback.AddButtonClick(continueButton);
            continueButton.onClick.AddListener(gameFlow.ContinueGame);
        }

        if (settingsButton != null)
        {
            AudioFeedback.AddButtonClick(settingsButton);
            settingsButton.onClick.AddListener(settingsPanelView.Open);
        }

        if (quitButton != null)
        {
            AudioFeedback.AddButtonClick(quitButton);
            quitButton.onClick.AddListener(gameFlow.QuitGame);
        }

        if (closeSettingsButton != null)
        {
            AudioFeedback.AddButtonClick(closeSettingsButton);
            closeSettingsButton.onClick.AddListener(settingsPanelView.Close);
        }

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameState.MainMenu);
        }

        settingsPanelView.Close();
        RefreshContinueButton();
        RefreshTexts();
    }

    private void OnDisable()
    {
        if (startButton != null)
        {
            AudioFeedback.RemoveButtonClick(startButton);
            startButton.onClick.RemoveListener(gameFlow.StartNewGame);
        }

        if (continueButton != null)
        {
            AudioFeedback.RemoveButtonClick(continueButton);
            continueButton.onClick.RemoveListener(gameFlow.ContinueGame);
        }

        if (settingsButton != null)
        {
            AudioFeedback.RemoveButtonClick(settingsButton);
            settingsButton.onClick.RemoveListener(settingsPanelView.Open);
        }

        if (quitButton != null)
        {
            AudioFeedback.RemoveButtonClick(quitButton);
            quitButton.onClick.RemoveListener(gameFlow.QuitGame);
        }

        if (closeSettingsButton != null)
        {
            AudioFeedback.RemoveButtonClick(closeSettingsButton);
            closeSettingsButton.onClick.RemoveListener(settingsPanelView.Close);
        }

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
        }
    }

    private void OnLanguageChanged(Language language)
    {
        RefreshTexts();
    }

    private void RefreshTexts()
    {
        SetButtonText(startButton, GetText("main.start", "Bắt đầu"));
        SetButtonText(continueButton, GetText("main.continue", "Tiếp tục"));
        SetButtonText(settingsButton, GetText("main.settings", "Cài đặt"));
        SetButtonText(quitButton, GetText("main.quit", "Thoát"));
    }

    private void RefreshContinueButton()
    {
        if (continueButton == null)
        {
            return;
        }

        bool hasSaveFile = SaveManager.Instance != null && SaveManager.Instance.HasSaveFile;
        continueButton.gameObject.SetActive(hasSaveFile);
        continueButton.interactable = hasSaveFile;
    }

    private void ResolveComponents()
    {
        if (gameFlow == null && !TryGetComponent(out gameFlow))
        {
            gameFlow = gameObject.AddComponent<MainMenuGameFlow>();
        }

        if (settingsPanelView == null && !TryGetComponent(out settingsPanelView))
        {
            settingsPanelView = gameObject.AddComponent<MainMenuSettingsPanel>();
        }

        gameFlow.SetGameplaySceneName(gameplaySceneName);
        settingsPanelView.SetPanel(settingsPanel);
    }

    private static void SetButtonText(Button button, string text)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null)
        {
            label.text = text;
        }
    }

    private static string GetText(string key, string fallback)
    {
        return LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(key)
            : fallback;
    }
}
