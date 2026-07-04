using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class RandomDialogueSpawner : MonoBehaviour
{
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private List<DialogueSO> dialogues = new();
    [SerializeField] private bool avoidImmediateRepeat = true;

    private int _lastDialogueIndex = -1;

    public async UniTask<bool> PlayRandomAsync(CancellationToken cancellationToken = default)
    {
        DialogueSO dialogue = PickDialogue();
        if (dialogue == null)
        {
            return false;
        }

        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            Debug.LogWarning("RandomDialogueSpawner: dialogue selected but no DialogueManager was found.", this);
            return false;
        }

        await manager.PlayDialogueAsync(dialogue, cancellationToken);
        return true;
    }

    public void PlayRandom()
    {
        PlayRandomAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private DialogueSO PickDialogue()
    {
        if (dialogues == null || dialogues.Count <= 0)
        {
            return null;
        }

        if (dialogues.Count == 1)
        {
            _lastDialogueIndex = 0;
            return dialogues[0];
        }

        int index = UnityEngine.Random.Range(0, dialogues.Count);
        if (avoidImmediateRepeat && index == _lastDialogueIndex)
        {
            index = (index + UnityEngine.Random.Range(1, dialogues.Count)) % dialogues.Count;
        }

        _lastDialogueIndex = index;
        return dialogues[index];
    }

    private DialogueManager ResolveDialogueManager()
    {
        if (dialogueManager != null)
        {
            return dialogueManager;
        }

        dialogueManager = UnityEngine.Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        return dialogueManager;
    }
}
