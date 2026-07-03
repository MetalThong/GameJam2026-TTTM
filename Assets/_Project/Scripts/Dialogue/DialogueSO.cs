using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_Dialogue_", menuName = "TTTM/Dialogue")]
public sealed class DialogueSO : ScriptableObject
{
    [Header("Optional Presentation")]
    [SerializeField] private Sprite background;

    [Header("Lines")]
    [SerializeField] private List<DialogueLine> lines = new();

    public Sprite Background => background;
    public IReadOnlyList<DialogueLine> Lines => lines;
    public bool HasLines => lines != null && lines.Count > 0;
}
