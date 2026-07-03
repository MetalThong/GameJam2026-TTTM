using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// StoryInteractable that plays a DialogueSO (and optional SFX) on success, then runs the flag action.
// Reusable for any flag-gated interaction that should show dialogue, e.g. the WakeUp panel or a cat meow.
public class DialogueStoryInteractable : StoryInteractable
{
    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogue;
    [SerializeField] private string sfxId;

    [Header("Pre Dialogue")]
    [Tooltip("Optional visual hidden right after the press, before the dialogue starts (e.g. WakeUpPanel Visual).")]
    [SerializeField] private GameObject hideBeforeDialogue;
    [SerializeField, Min(0f)] private float hideBeforeDialogueFadeDuration = 1f;
    [SerializeField, Min(0f)] private float dialogueDelayAfterHide = 2f;

    [Header("Completion")]
    [Tooltip("Deactivate this GameObject after the dialogue and action complete.")]
    [SerializeField] private bool deactivateOnComplete;

    private bool _isPlaying;
    private Tween _hideBeforeDialogueTween;

    private void OnDestroy()
    {
        _hideBeforeDialogueTween?.Kill();
    }

    protected override void OnInteractSucceeded()
    {
        // Guard against re-entry while the dialogue is running.
        if (_isPlaying)
        {
            return;
        }

        PlaySequenceAsync().Forget();
    }

    private async UniTaskVoid PlaySequenceAsync()
    {
        _isPlaying = true;
        CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();

        try
        {
            if (AudioManager.Instance != null && !string.IsNullOrWhiteSpace(sfxId))
            {
                AudioManager.Instance.PlaySfx(sfxId);
            }

            // Hide the visual first (e.g. WakeUpPanel), then show the dialogue.
            if (hideBeforeDialogue != null)
            {
                await HideBeforeDialogueAsync(destroyToken);

                if (dialogueDelayAfterHide > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(dialogueDelayAfterHide), cancellationToken: destroyToken);
                }
            }

            // Play dialogue before ExecuteAction/deactivate: DialogueManager lives on a separate
            // scene object, so it keeps running even after this GameObject is disabled.
            DialogueManager manager = ResolveDialogueManager();
            if (manager != null && dialogue != null)
            {
                await manager.PlayDialogueAsync(dialogue, destroyToken);
            }
            else if (dialogue != null)
            {
                Debug.LogWarning("DialogueStoryInteractable: dialogue assigned but no DialogueManager was found.", this);
            }

            ExecuteAction();

            if (deactivateOnComplete)
            {
                gameObject.SetActive(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isPlaying = false;
        }
    }

    private async UniTask HideBeforeDialogueAsync(CancellationToken cancellationToken)
    {
        hideBeforeDialogue.SetActive(true);
        _hideBeforeDialogueTween?.Kill();

        float fadeDuration = Mathf.Max(0f, hideBeforeDialogueFadeDuration);
        if (fadeDuration <= 0f)
        {
            SetVisualAlpha(hideBeforeDialogue, 0f);
            hideBeforeDialogue.SetActive(false);
            return;
        }

        Sequence sequence = DOTween.Sequence();
        int tweenCount = 0;

        foreach (SpriteRenderer spriteRenderer in hideBeforeDialogue.GetComponentsInChildren<SpriteRenderer>(true))
        {
            DOTween.Kill(spriteRenderer);
            sequence.Join(spriteRenderer.DOFade(0f, fadeDuration));
            tweenCount++;
        }

        foreach (Graphic graphic in hideBeforeDialogue.GetComponentsInChildren<Graphic>(true))
        {
            DOTween.Kill(graphic);
            sequence.Join(graphic.DOFade(0f, fadeDuration));
            tweenCount++;
        }

        if (tweenCount == 0)
        {
            hideBeforeDialogue.SetActive(false);
            return;
        }

        _hideBeforeDialogueTween = sequence;
        await sequence.AsyncWaitForCompletion();

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_hideBeforeDialogueTween == sequence)
        {
            _hideBeforeDialogueTween = null;
        }

        hideBeforeDialogue.SetActive(false);
    }

    private static void SetVisualAlpha(GameObject visual, float alpha)
    {
        foreach (SpriteRenderer spriteRenderer in visual.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        foreach (Graphic graphic in visual.GetComponentsInChildren<Graphic>(true))
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
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
