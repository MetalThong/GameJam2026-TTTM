using System;
using UnityEngine;

[Serializable]
public sealed class DialogueLine
{
    [SerializeField] private string speakerName;
    [SerializeField] private string englishSpeakerName;
    [TextArea(2, 4)]
    [SerializeField] private string text;
    [TextArea(2, 4)]
    [SerializeField] private string englishText;
    [SerializeField] private Sprite portrait;

    public string SpeakerName => speakerName;
    public string EnglishSpeakerName => englishSpeakerName;
    public string Text => text;
    public string EnglishText => englishText;
    public Sprite Portrait => portrait;

    public DialogueLine(string speakerName, string text, Sprite portrait)
    {
        this.speakerName = speakerName;
        this.text = text;
        this.portrait = portrait;
    }

    public string GetSpeakerName(LocalizationManager localizationManager)
    {
        return localizationManager != null
            ? localizationManager.GetDialogueSpeaker(speakerName, englishSpeakerName)
            : speakerName;
    }

    public string GetText(LocalizationManager localizationManager)
    {
        return localizationManager != null
            ? localizationManager.GetDialogueText(text, englishText)
            : text;
    }
}
