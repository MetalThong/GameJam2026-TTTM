using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private List<UIPanelView> panels;
    private Dictionary<PanelId, UIPanelView> _id2Panel;

    private void Awake()
    {
        _id2Panel = new();
        for (int i = 0; i < panels.Count; i++)
        {
            UIPanelView panel = panels[i];
            _id2Panel[panel.Id] = panel;
        }
    }

    public void OpenPanel(PanelId panelId)
    {
        if (!GetPopupPanel(panelId, out UIPanelView popupPanel))
        {
            return;
        }

        popupPanel.Show();
        popupPanel.transform.SetAsLastSibling();
    }

    public void ClosePanel(PanelId panelId)
    {
        if (!GetPopupPanel(panelId, out UIPanelView popupPanel))
        {
            return;
        }

        popupPanel.Hide();
    }

    public void HideAllPanels()
    {
        foreach (UIPanelView uIPanelView in _id2Panel.Values)
        {
            uIPanelView.Hide();
        }
    }

    private bool GetPopupPanel(PanelId panelId, out UIPanelView popupPanel)
    {
        return _id2Panel.TryGetValue(panelId, out popupPanel);
    }
}
