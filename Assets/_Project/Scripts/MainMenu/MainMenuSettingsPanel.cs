using UnityEngine;

public sealed class MainMenuSettingsPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    public void SetPanel(GameObject panelObject)
    {
        if (panelObject == null)
        {
            return;
        }

        panel = panelObject;
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
}
