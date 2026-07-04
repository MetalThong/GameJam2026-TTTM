using UnityEngine;
using UnityEngine.UI;

public sealed class SceneUIController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeSettingsButton;
    [SerializeField] private GameObject settingsPanel;

    public void SetReferences(Button openButton, Button closeButton, GameObject panel)
    {
        settingsButton = openButton;
        closeSettingsButton = closeButton;
        settingsPanel = panel;
    }

    private void OnEnable()
    {
        if (settingsButton != null)
        {
            AudioFeedback.AddButtonClick(settingsButton);
            settingsButton.onClick.AddListener(OpenSettings);
        }

        if (closeSettingsButton != null)
        {
            AudioFeedback.AddButtonClick(closeSettingsButton);
            closeSettingsButton.onClick.AddListener(CloseSettings);
        }
    }

    private void Start()
    {
        CloseSettings();
    }

    private void OnDisable()
    {
        if (settingsButton != null)
        {
            AudioFeedback.RemoveButtonClick(settingsButton);
            settingsButton.onClick.RemoveListener(OpenSettings);
        }

        if (closeSettingsButton != null)
        {
            AudioFeedback.RemoveButtonClick(closeSettingsButton);
            closeSettingsButton.onClick.RemoveListener(CloseSettings);
        }
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }
}
