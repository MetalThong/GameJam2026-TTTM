using System;
using UnityEngine;

[Serializable]
public sealed class DialogueLine
{
    [SerializeField] private string speakerName;
    [TextArea(2, 4)]
    [SerializeField] private string text;
    [SerializeField] private Sprite portrait;

    public string SpeakerName => speakerName;
    public string Text => text;
    public Sprite Portrait => portrait;

    public DialogueLine(string speakerName, string text, Sprite portrait)
    {
        this.speakerName = speakerName;
        this.text = text;
        this.portrait = portrait;
    }
}
