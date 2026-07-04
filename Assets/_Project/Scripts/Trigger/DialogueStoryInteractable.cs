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

    [Header("Pre Hide Animation")]
    [SerializeField] private bool playPreHideAnimationBeforeFade;
    [SerializeField] private Animator preHideAnimator;
    [SerializeField] private string preHideAnimationStateName = "OpenEye";
    [SerializeField, Min(0f)] private float preHideAnimationFallbackDuration = 0.92f;
    [SerializeField] private bool disablePreHideAnimatorUntilPlayed = true;

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

    private void OnEnable()
    {
        if (playPreHideAnimationBeforeFade && disablePreHideAnimatorUntilPlayed)
        {
            Animator animator = ResolvePreHideAnimator();
            if (animator != null)
            {
                animator.enabled = false;
            }
        }
    }

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

    protected override bool CanInteract()
    {
        return !_isPlaying;
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
                hideBeforeDialogue.SetActive(true);
                await PlayPreHideAnimationAsync(destroyToken);
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

    private async UniTask PlayPreHideAnimationAsync(CancellationToken cancellationToken)
    {
        if (!playPreHideAnimationBeforeFade)
        {
            return;
        }

        Animator animator = ResolvePreHideAnimator();
        if (animator == null)
        {
            return;
        }

        animator.enabled = true;
        animator.speed = 1f;

        if (!string.IsNullOrWhiteSpace(preHideAnimationStateName))
        {
            int stateHash = Animator.StringToHash(preHideAnimationStateName);
            if (animator.HasState(0, stateHash))
            {
                animator.Play(stateHash, 0, 0f);
                animator.Update(0f);
            }
        }

        float waitDuration = ResolvePreHideAnimationDuration(animator);
        if (waitDuration <= 0f)
        {
            waitDuration = preHideAnimationFallbackDuration;
        }

        if (waitDuration > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(waitDuration), cancellationToken: cancellationToken);
        }
    }

    private float ResolvePreHideAnimationDuration(Animator animator)
    {
        RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
        if (controller == null || string.IsNullOrWhiteSpace(preHideAnimationStateName))
        {
            return 0f;
        }

        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip == null || !MatchesAnimationName(clip.name, preHideAnimationStateName))
            {
                continue;
            }

            float speed = Mathf.Abs(animator.speed);
            if (Mathf.Approximately(speed, 0f))
            {
                speed = 1f;
            }

            return clip.length / speed;
        }

        return 0f;
    }

    private Animator ResolvePreHideAnimator()
    {
        if (preHideAnimator != null)
        {
            return preHideAnimator;
        }

        if (hideBeforeDialogue == null)
        {
            return null;
        }

        preHideAnimator = hideBeforeDialogue.GetComponentInChildren<Animator>(true);
        return preHideAnimator;
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

    private static bool MatchesAnimationName(string clipName, string stateName)
    {
        return string.Equals(clipName, stateName, StringComparison.Ordinal)
            || clipName.Contains(stateName, StringComparison.Ordinal)
            || stateName.Contains(clipName, StringComparison.Ordinal);
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
