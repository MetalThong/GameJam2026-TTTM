using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialogueView : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Sprite defaultPortrait;

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

    public void SetReferences(
        GameObject root,
        Image background,
        Image portrait,
        TMP_Text speakerName,
        TMP_Text body,
        Sprite fallbackPortrait)
    {
        panelRoot = root;
        backgroundImage = background;
        portraitImage = portrait;
        speakerNameText = speakerName;
        bodyText = body;
        defaultPortrait = fallbackPortrait;
    }

    public void Show(DialogueLine line)
    {
        if (!HasRequiredReferences())
        {
            Debug.LogWarning("DialogueView: missing scene UI references.", this);
            return;
        }

        if (line == null)
        {
            Hide();
            return;
        }

        speakerNameText.text = string.IsNullOrWhiteSpace(line.SpeakerName) ? "???" : line.SpeakerName;
        bodyText.text = string.IsNullOrWhiteSpace(line.Text) ? string.Empty : line.Text;

        if (portraitImage != null)
        {
            portraitImage.sprite = line.Portrait != null ? line.Portrait : defaultPortrait;
            portraitImage.enabled = portraitImage.sprite != null;
        }

        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void SetBackground(Sprite sprite)
    {
        if (backgroundImage == null)
        {
            return;
        }

        backgroundImage.sprite = sprite;
        backgroundImage.type = Image.Type.Simple;
    }

    private bool HasRequiredReferences()
    {
        return panelRoot != null
            && speakerNameText != null
            && bodyText != null;
    }
}
