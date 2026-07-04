using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class DialogueStoryTrigger : StoryTrigger
{
    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogue;
    [SerializeField, Min(0f)] private float startDelay = 0.1f;
    [SerializeField] private bool waitForCurrentDialogue = true;

    [Header("Completion")]
    [SerializeField] private string flagAfterDialogue = "went_to_store";

    private bool _isPlaying;

    protected override void Trigger()
    {
        if (_isPlaying)
        {
            return;
        }

        PlaySequenceAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid PlaySequenceAsync(CancellationToken cancellationToken)
    {
        _isPlaying = true;

        try
        {
            if (startDelay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: cancellationToken);
            }

            DialogueManager manager = ResolveDialogueManager();
            if (manager != null && waitForCurrentDialogue)
            {
                while (manager.IsPlaying)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }

            if (manager != null && dialogue != null)
            {
                await manager.PlayDialogueAsync(dialogue, cancellationToken);
            }
            else if (dialogue != null)
            {
                Debug.LogWarning("DialogueStoryTrigger: dialogue assigned but no DialogueManager was found.", this);
            }

            SetFlagAfterDialogue();
            ExecuteAction();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isPlaying = false;
        }
    }

    private void SetFlagAfterDialogue()
    {
        if (!string.IsNullOrWhiteSpace(flagAfterDialogue) && FlagManager.Instance != null)
        {
            FlagManager.Instance.SetFlag(flagAfterDialogue, true);
        }
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
