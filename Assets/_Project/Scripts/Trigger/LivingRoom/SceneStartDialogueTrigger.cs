using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SceneStartDialogueTrigger : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogue;
    [SerializeField, Min(0f)] private float startDelay = 0.1f;
    [SerializeField] private bool waitForCurrentDialogue = true;

    [Header("Flags")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private string completionFlagId = "lr4_left_hint_seen";
    [SerializeField] private StoryFlagCondition condition = new();
    [SerializeField] private StoryFlagAction action = new();

    private bool _isPlaying;

    private void Start()
    {
        TryPlayAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid TryPlayAsync(CancellationToken cancellationToken)
    {
        if (_isPlaying || !CanPlay())
        {
            return;
        }

        _isPlaying = true;

        try
        {
            while (FlagManager.Instance == null)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (startDelay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: cancellationToken);
            }

            if (!CanPlay())
            {
                return;
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
                Debug.LogWarning("SceneStartDialogueTrigger: dialogue assigned but no DialogueManager was found.", this);
            }

            ExecuteCompletion();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isPlaying = false;
        }
    }

    private bool CanPlay()
    {
        FlagManager flagManager = FlagManager.Instance;
        if (flagManager == null)
        {
            return false;
        }

        if (triggerOnce
            && !string.IsNullOrWhiteSpace(completionFlagId)
            && flagManager.HasFlag(completionFlagId))
        {
            return false;
        }

        return condition == null || condition.IsMet(flagManager.Flags);
    }

    private void ExecuteCompletion()
    {
        FlagManager flagManager = FlagManager.Instance;
        if (flagManager == null)
        {
            return;
        }

        action?.Execute(flagManager);

        if (!string.IsNullOrWhiteSpace(completionFlagId))
        {
            flagManager.SetFlag(completionFlagId, true);
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
