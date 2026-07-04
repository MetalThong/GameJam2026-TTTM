using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class CutSceneDialoguePlayer : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private Animation legacyAnimation;
    [Tooltip("Empty = use the first controller clip/state that can be resolved.")]
    [SerializeField] private string animationStateName;
    [SerializeField, Min(0)] private int animatorLayer;
    [SerializeField] private bool freezeOnLastFrame = true;
    [SerializeField, Min(0f)] private float delayAfterAnimation;

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogue;

    [Header("Playback")]
    [SerializeField] private bool playOnEnable = true;

    [Header("Completion")]
    [SerializeField] private bool deactivateOnComplete;

    private CancellationTokenSource _enableCts;
    private bool _isPlaying;
    private bool _isStartingManually;
    private bool _hasCachedAnimatorSpeed;
    private float _cachedAnimatorSpeed = 1f;

    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        ResolveReferences();
        CacheAnimatorSpeed();
    }

    private void OnEnable()
    {
        if (_isStartingManually || !playOnEnable)
        {
            return;
        }

        CancelSequence();
        _isPlaying = false;
        ResolveReferences();
        _enableCts = new CancellationTokenSource();
        PlaySequenceFromOnEnableAsync(_enableCts.Token).Forget();
    }

    private void OnDisable()
    {
        CancelSequence();
        _isPlaying = false;
    }

    private void OnDestroy()
    {
        CancelSequence();
    }

    public async UniTask PlayAsync(CancellationToken cancellationToken = default)
    {
        CancelSequence();
        _isPlaying = false;
        ResolveReferences();
        _enableCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            if (!gameObject.activeSelf)
            {
                _isStartingManually = true;
                gameObject.SetActive(true);
            }

            await PlaySequenceAsync(_enableCts.Token);
        }
        finally
        {
            _isStartingManually = false;
        }
    }

    public void PrepareForManualPlayback()
    {
        ResolveReferences();

        if (animator != null)
        {
            PrepareAnimatorStartFrame();
            return;
        }

        if (legacyAnimation != null)
        {
            PrepareLegacyAnimationStartFrame();
        }
    }

    private async UniTaskVoid PlaySequenceFromOnEnableAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PlaySequenceAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTask PlaySequenceAsync(CancellationToken cancellationToken)
    {
        if (_isPlaying)
        {
            return;
        }

        _isPlaying = true;

        try
        {
            await PlayAnimationOnceAsync(cancellationToken);

            if (delayAfterAnimation > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delayAfterAnimation), cancellationToken: cancellationToken);
            }

            await PlayDialogueIfAssignedAsync(cancellationToken);

            if (deactivateOnComplete && !cancellationToken.IsCancellationRequested)
            {
                gameObject.SetActive(false);
            }
        }
        finally
        {
            _isPlaying = false;
        }
    }

    private async UniTask PlayAnimationOnceAsync(CancellationToken cancellationToken)
    {
        if (animator != null)
        {
            await PlayAnimatorOnceAsync(cancellationToken);
            return;
        }

        if (legacyAnimation != null)
        {
            await PlayLegacyAnimationOnceAsync(cancellationToken);
            return;
        }

        Debug.LogWarning("CutSceneDialoguePlayer: no Animator or Animation reference was found.", this);
    }

    private async UniTask PlayAnimatorOnceAsync(CancellationToken cancellationToken)
    {
        if (animator == null)
        {
            return;
        }

        if (!animator.gameObject.activeInHierarchy || !animator.isActiveAndEnabled)
        {
            Debug.LogWarning("CutSceneDialoguePlayer: Animator must be active in the hierarchy before playback.", animator);
            return;
        }

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null)
        {
            Debug.LogWarning("CutSceneDialoguePlayer: Animator has no controller.", animator);
            return;
        }

        int layerCount = animator.layerCount;
        if (layerCount <= 0)
        {
            Debug.LogWarning("CutSceneDialoguePlayer: Animator has no playable layers.", animator);
            return;
        }

        int layer = Mathf.Clamp(animatorLayer, 0, layerCount - 1);
        string stateName = ResolveAnimatorStateName(animator, layer);
        if (string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogWarning("CutSceneDialoguePlayer: no playable Animator state was resolved.", animator);
            return;
        }

        CacheAnimatorSpeed();
        animator.enabled = true;
        animator.speed = Mathf.Approximately(_cachedAnimatorSpeed, 0f) ? 1f : _cachedAnimatorSpeed;

        int stateHash = Animator.StringToHash(stateName);
        animator.Play(stateHash, layer, 0f);
        animator.Update(0f);

        float clipLength = ResolveAnimatorClipLength(animator, layer, stateName);
        float speed = Mathf.Max(0.01f, Mathf.Abs(animator.speed));
        if (clipLength > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(clipLength / speed), cancellationToken: cancellationToken);
        }

        if (!freezeOnLastFrame || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        animator.Play(stateHash, layer, 0.999f);
        animator.Update(0f);
        animator.speed = 0f;
    }

    private void PrepareAnimatorStartFrame()
    {
        if (animator == null || !animator.gameObject.activeInHierarchy)
        {
            return;
        }

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null || animator.layerCount <= 0)
        {
            return;
        }

        int layer = Mathf.Clamp(animatorLayer, 0, animator.layerCount - 1);
        string stateName = ResolveAnimatorStateName(animator, layer);
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        CacheAnimatorSpeed();
        animator.enabled = true;
        animator.speed = 0f;
        animator.Play(Animator.StringToHash(stateName), layer, 0f);
        animator.Update(0f);
    }

    private async UniTask PlayLegacyAnimationOnceAsync(CancellationToken cancellationToken)
    {
        AnimationClip clip = ResolveLegacyClip(legacyAnimation);
        if (clip == null)
        {
            Debug.LogWarning("CutSceneDialoguePlayer: no playable legacy Animation clip was resolved.", legacyAnimation);
            return;
        }

        legacyAnimation.gameObject.SetActive(true);
        legacyAnimation.clip = clip;

        AnimationState animationState = legacyAnimation[clip.name];
        if (animationState != null)
        {
            animationState.wrapMode = WrapMode.Once;
            if (Mathf.Approximately(animationState.speed, 0f))
            {
                animationState.speed = 1f;
            }
        }

        legacyAnimation.Play(clip.name);

        float speed = animationState != null ? Mathf.Abs(animationState.speed) : 1f;
        speed = Mathf.Max(0.01f, speed);
        await UniTask.Delay(TimeSpan.FromSeconds(clip.length / speed), cancellationToken: cancellationToken);

        if (!freezeOnLastFrame || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        clip.SampleAnimation(legacyAnimation.gameObject, clip.length);
        legacyAnimation.Stop();
    }

    private void PrepareLegacyAnimationStartFrame()
    {
        AnimationClip clip = ResolveLegacyClip(legacyAnimation);
        if (clip == null)
        {
            return;
        }

        legacyAnimation.gameObject.SetActive(true);
        legacyAnimation.clip = clip;
        clip.SampleAnimation(legacyAnimation.gameObject, 0f);
        legacyAnimation.Stop();
    }

    private async UniTask PlayDialogueIfAssignedAsync(CancellationToken cancellationToken)
    {
        if (dialogue == null)
        {
            return;
        }

        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            Debug.LogWarning("CutSceneDialoguePlayer: dialogue assigned but no DialogueManager was found.", this);
            return;
        }

        await manager.PlayDialogueAsync(dialogue, cancellationToken);
    }

    private string ResolveAnimatorStateName(Animator targetAnimator, int layer)
    {
        if (!string.IsNullOrWhiteSpace(animationStateName))
        {
            string requestedState = animationStateName.Trim();
            if (targetAnimator.HasState(layer, Animator.StringToHash(requestedState)))
            {
                return requestedState;
            }

            string alternateState = requestedState.EndsWith("_clip", StringComparison.Ordinal)
                ? requestedState[..^"_clip".Length]
                : $"{requestedState}_clip";

            if (targetAnimator.HasState(layer, Animator.StringToHash(alternateState)))
            {
                return alternateState;
            }
        }

        RuntimeAnimatorController controller = targetAnimator.runtimeAnimatorController;
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip == null)
            {
                continue;
            }

            if (targetAnimator.HasState(layer, Animator.StringToHash(clip.name)))
            {
                return clip.name;
            }

            string clipStateName = $"{clip.name}_clip";
            if (targetAnimator.HasState(layer, Animator.StringToHash(clipStateName)))
            {
                return clipStateName;
            }
        }

        return null;
    }

    private float ResolveAnimatorClipLength(Animator targetAnimator, int layer, string stateName)
    {
        RuntimeAnimatorController controller = targetAnimator.runtimeAnimatorController;
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(animationStateName)
                || string.Equals(clip.name, animationStateName, StringComparison.Ordinal)
                || string.Equals(clip.name, stateName, StringComparison.Ordinal)
                || stateName.Contains(clip.name, StringComparison.Ordinal)
                || clip.name.Contains(stateName, StringComparison.Ordinal))
            {
                return clip.length;
            }
        }

        AnimatorStateInfo stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(layer);
        return stateInfo.length;
    }

    private AnimationClip ResolveLegacyClip(Animation animation)
    {
        if (animation == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(animationStateName))
        {
            AnimationClip namedClip = animation.GetClip(animationStateName.Trim());
            if (namedClip != null)
            {
                return namedClip;
            }
        }

        if (animation.clip != null)
        {
            return animation.clip;
        }

        foreach (AnimationState state in animation)
        {
            return state.clip;
        }

        return null;
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

    private void ResolveReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (legacyAnimation == null && animator == null)
        {
            legacyAnimation = GetComponentInChildren<Animation>(true);
        }
    }

    private void CacheAnimatorSpeed()
    {
        if (_hasCachedAnimatorSpeed || animator == null)
        {
            return;
        }

        _cachedAnimatorSpeed = animator.speed;
        _hasCachedAnimatorSpeed = true;
    }

    private void CancelSequence()
    {
        if (_enableCts == null)
        {
            return;
        }

        _enableCts.Cancel();
        _enableCts.Dispose();
        _enableCts = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif
}
