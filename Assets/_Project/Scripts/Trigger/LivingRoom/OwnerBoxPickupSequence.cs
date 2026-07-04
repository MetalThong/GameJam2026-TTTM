using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class OwnerBoxPickupSequence : MonoBehaviour
{
    [Header("Owner Spawn")]
    [SerializeField] private GameObject ownerPrefab;
    [SerializeField] private string ownerResourcePath = "Main/thang chu di";
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector3 spawnOffset;
    [SerializeField] private bool useSpawnPositionOverride;
    [SerializeField] private Vector3 spawnPositionOverride;
    [SerializeField] private Vector3 ownerSpawnScale = Vector3.one;
    [Tooltip("Mirror the whole owner hierarchy on X so attached props keep their relative offset.")]
    [SerializeField] private bool flipOwnerOnSpawn;
    [SerializeField] private bool disableAnimatorOnSpawn;
    [SerializeField, Min(0f)] private float spawnFadeDuration = 0.25f;

    [Header("Pickup Animation")]
    [SerializeField] private string pickupAnimationState = "PickUpBox";
    [SerializeField] private bool waitForPickupAnimation = true;
    [SerializeField, Min(0f)] private float fallbackAnimationDuration = 0.8f;
    [SerializeField, Min(0f)] private float postAnimationDelay;

    [Header("Exit")]
    [SerializeField] private bool playExitAfterPickup;
    [SerializeField] private string exitAnimationState = "BossWalk";
    [SerializeField] private Vector3 exitMoveOffset = new(1.6f, 0f, 0f);
    [SerializeField, Min(0f)] private float exitMoveDuration = 1.4f;
    [SerializeField, Min(0f)] private float exitFadeOutDuration = 0.35f;
    [SerializeField] private bool waitForExitAnimation;
    [SerializeField, Min(0f)] private float exitFallbackAnimationDuration = 0.8f;
    [SerializeField, Min(0f)] private float exitPostAnimationDelay;
    [SerializeField] private bool destroyOwnerAfterExit = true;
    [SerializeField] private bool faceExitMoveDirection = true;
    [SerializeField] private bool flipOwnerWhenMovingRight = true;

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogue;
    [SerializeField] private DialogueSO postExitDialogue;

    [Header("Completion")]
    [SerializeField] private string completionFlagId = "picked_up_box";
    [SerializeField] private bool deactivateOnComplete = true;

    private bool _isPlaying;
    private GameObject _spawnedOwner;

    private void OnEnable()
    {
        if (_isPlaying || IsCompleted())
        {
            return;
        }

        PlaySequenceAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void OnDestroy()
    {
        if (_spawnedOwner != null)
        {
            Destroy(_spawnedOwner);
            _spawnedOwner = null;
        }
    }

    private async UniTaskVoid PlaySequenceAsync(CancellationToken cancellationToken)
    {
        _isPlaying = true;

        try
        {
            _spawnedOwner = SpawnOwner();
            await FadeOwnerAsync(_spawnedOwner, 1f, spawnFadeDuration, cancellationToken);
            await PlayPickupAnimationAsync(_spawnedOwner, cancellationToken);
            await PlayDialogueIfAssignedAsync(cancellationToken);
            await PlayExitSequenceAsync(_spawnedOwner, cancellationToken);
            await PlayDialogueIfAssignedAsync(postExitDialogue, cancellationToken);
            SetCompletionFlag();

            if (deactivateOnComplete && !cancellationToken.IsCancellationRequested)
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

    private async UniTask PlayExitSequenceAsync(GameObject owner, CancellationToken cancellationToken)
    {
        if (!playExitAfterPickup || owner == null)
        {
            return;
        }

        float animationDuration = PlayOwnerAnimation(owner, exitAnimationState);
        if (waitForExitAnimation)
        {
            await WaitForDurationAsync(
                animationDuration > 0f ? animationDuration : exitFallbackAnimationDuration,
                exitPostAnimationDelay,
                cancellationToken);
        }

        await MoveOwnerByOffsetAsync(owner, animationDuration, cancellationToken);
        await FadeOwnerAsync(owner, 0f, exitFadeOutDuration, cancellationToken);

        if (destroyOwnerAfterExit && owner != null)
        {
            Destroy(owner);
            if (_spawnedOwner == owner)
            {
                _spawnedOwner = null;
            }
        }
    }

    private float PlayOwnerAnimation(GameObject owner, string stateName)
    {
        if (owner == null || string.IsNullOrWhiteSpace(stateName))
        {
            return 0f;
        }

        Animator animator = FindAnimatorWithState(owner, stateName);
        if (animator != null)
        {
            return PlayAnimatorState(animator, stateName);
        }

        Animation animation = owner.GetComponentInChildren<Animation>(true);
        if (animation != null)
        {
            return PlayLegacyAnimation(animation, stateName);
        }

        return 0f;
    }

    private async UniTask MoveOwnerByOffsetAsync(
        GameObject owner,
        float animationDuration,
        CancellationToken cancellationToken)
    {
        if (owner == null || exitMoveOffset.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 targetPosition = owner.transform.position + exitMoveOffset;
        targetPosition.z = owner.transform.position.z;
        OrientOwnerToward(owner, targetPosition);

        float moveDuration = Mathf.Max(0f, exitMoveDuration);
        if (!waitForExitAnimation)
        {
            moveDuration = Mathf.Max(moveDuration, animationDuration + exitPostAnimationDelay);
        }

        if (moveDuration <= 0f)
        {
            owner.transform.position = targetPosition;
            return;
        }

        Tween moveTween = owner.transform.DOMove(targetPosition, moveDuration).SetEase(Ease.Linear);
        using (cancellationToken.Register(() => moveTween.Kill()))
        {
            await moveTween.AsyncWaitForCompletion();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private GameObject SpawnOwner()
    {
        GameObject prefab = ownerPrefab;
        if (prefab == null && !string.IsNullOrWhiteSpace(ownerResourcePath))
        {
            prefab = Resources.Load<GameObject>(ownerResourcePath);
        }

        if (prefab == null)
        {
            Debug.LogWarning($"OwnerBoxPickupSequence: owner prefab could not be resolved from Resources path '{ownerResourcePath}'.", this);
            return null;
        }

        Vector3 spawnPosition = ResolveSpawnPosition();
        GameObject owner = Instantiate(prefab, spawnPosition + spawnOffset, Quaternion.identity);
        owner.name = prefab.name;
        owner.SetActive(true);
        ApplyOwnerSpawnPresentation(owner);
        SetOwnerAlpha(owner, 0f);
        return owner;
    }

    private Vector3 ResolveSpawnPosition()
    {
        if (useSpawnPositionOverride)
        {
            return spawnPositionOverride;
        }

        Transform resolvedSpawnPoint = spawnPoint != null ? spawnPoint : transform;
        return resolvedSpawnPoint.position;
    }

    private void ApplyOwnerSpawnPresentation(GameObject owner)
    {
        if (owner == null)
        {
            return;
        }

        Vector3 spawnScale = ownerSpawnScale;
        if (flipOwnerOnSpawn)
        {
            float xScale = Mathf.Approximately(spawnScale.x, 0f) ? 1f : Mathf.Abs(spawnScale.x);
            spawnScale.x = -xScale;
        }

        owner.transform.localScale = spawnScale;

        if (!disableAnimatorOnSpawn)
        {
            return;
        }

        foreach (Animator animator in owner.GetComponentsInChildren<Animator>(true))
        {
            if (animator != null)
            {
                animator.enabled = false;
            }
        }
    }

    private async UniTask PlayPickupAnimationAsync(GameObject owner, CancellationToken cancellationToken)
    {
        if (owner == null || string.IsNullOrWhiteSpace(pickupAnimationState))
        {
            await WaitFallbackAnimationAsync(cancellationToken);
            return;
        }

        Animator animator = FindAnimatorWithState(owner, pickupAnimationState);
        if (animator != null)
        {
            float duration = PlayAnimatorState(animator);
            await WaitAnimationAsync(duration, cancellationToken);
            return;
        }

        Animation animation = owner.GetComponentInChildren<Animation>(true);
        if (animation != null)
        {
            float duration = PlayLegacyAnimation(animation);
            await WaitAnimationAsync(duration, cancellationToken);
            return;
        }

        await WaitFallbackAnimationAsync(cancellationToken);
    }

    private float PlayAnimatorState(Animator animator)
    {
        return PlayAnimatorState(animator, pickupAnimationState);
    }

    private Animator FindAnimatorWithState(GameObject owner, string stateName)
    {
        if (owner == null)
        {
            return null;
        }

        Animator fallbackAnimator = null;
        foreach (Animator animator in owner.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null)
            {
                continue;
            }

            fallbackAnimator ??= animator;
            if (!string.IsNullOrWhiteSpace(ResolveAnimatorStateName(animator, stateName)))
            {
                return animator;
            }
        }

        return fallbackAnimator;
    }

    private float PlayAnimatorState(Animator animator, string requestedState)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return 0f;
        }

        animator.gameObject.SetActive(true);
        animator.enabled = true;

        string stateName = ResolveAnimatorStateName(animator, requestedState);
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return 0f;
        }

        animator.Play(Animator.StringToHash(stateName), 0, 0f);
        animator.Update(0f);
        return ResolveClipLength(animator, stateName, requestedState);
    }

    private string ResolveAnimatorStateName(Animator animator)
    {
        return ResolveAnimatorStateName(animator, pickupAnimationState);
    }

    private string ResolveAnimatorStateName(Animator animator, string requestedState)
    {
        if (string.IsNullOrWhiteSpace(requestedState))
        {
            return null;
        }

        if (animator.HasState(0, Animator.StringToHash(requestedState)))
        {
            return requestedState;
        }

        string alternateState = requestedState.EndsWith("_clip", StringComparison.Ordinal)
            ? requestedState[..^"_clip".Length]
            : $"{requestedState}_clip";

        if (animator.HasState(0, Animator.StringToHash(alternateState)))
        {
            return alternateState;
        }

        return null;
    }

    private float ResolveClipLength(Animator animator, string stateName)
    {
        return ResolveClipLength(animator, stateName, pickupAnimationState);
    }

    private float ResolveClipLength(Animator animator, string stateName, string requestedState)
    {
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip == null)
            {
                continue;
            }

            if (string.Equals(clip.name, requestedState, StringComparison.Ordinal)
                || string.Equals(clip.name, stateName, StringComparison.Ordinal)
                || stateName.Contains(clip.name, StringComparison.Ordinal)
                || clip.name.Contains(stateName, StringComparison.Ordinal))
            {
                return clip.length;
            }
        }

        return 0f;
    }

    private float PlayLegacyAnimation(Animation animation)
    {
        return PlayLegacyAnimation(animation, pickupAnimationState);
    }

    private float PlayLegacyAnimation(Animation animation, string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return 0f;
        }

        AnimationClip clip = animation.GetClip(stateName);
        if (clip == null)
        {
            return 0f;
        }

        animation.clip = clip;
        animation.Play(clip.name);
        return clip.length;
    }

    private async UniTask WaitAnimationAsync(float animationDuration, CancellationToken cancellationToken)
    {
        if (!waitForPickupAnimation)
        {
            return;
        }

        float waitDuration = animationDuration > 0f ? animationDuration : fallbackAnimationDuration;
        waitDuration += postAnimationDelay;
        await WaitForDurationAsync(waitDuration, 0f, cancellationToken);
    }

    private async UniTask WaitFallbackAnimationAsync(CancellationToken cancellationToken)
    {
        if (!waitForPickupAnimation || fallbackAnimationDuration <= 0f)
        {
            return;
        }

        await WaitForDurationAsync(fallbackAnimationDuration, postAnimationDelay, cancellationToken);
    }

    private static async UniTask WaitForDurationAsync(
        float duration,
        float postDelay,
        CancellationToken cancellationToken)
    {
        float waitDuration = duration + postDelay;
        if (waitDuration <= 0f)
        {
            return;
        }

        await UniTask.Delay(TimeSpan.FromSeconds(waitDuration), cancellationToken: cancellationToken);
    }

    private async UniTask PlayDialogueIfAssignedAsync(CancellationToken cancellationToken)
    {
        await PlayDialogueIfAssignedAsync(dialogue, cancellationToken);
    }

    private async UniTask PlayDialogueIfAssignedAsync(DialogueSO dialogueToPlay, CancellationToken cancellationToken)
    {
        if (dialogueToPlay == null)
        {
            return;
        }

        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            Debug.LogWarning("OwnerBoxPickupSequence: dialogue assigned but no DialogueManager was found.", this);
            return;
        }

        await manager.PlayDialogueAsync(dialogueToPlay, cancellationToken);
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

    private void SetCompletionFlag()
    {
        if (string.IsNullOrWhiteSpace(completionFlagId) || FlagManager.Instance == null)
        {
            return;
        }

        FlagManager.Instance.SetFlag(completionFlagId, true);
    }

    private bool IsCompleted()
    {
        return !string.IsNullOrWhiteSpace(completionFlagId)
            && FlagManager.Instance != null
            && FlagManager.Instance.HasFlag(completionFlagId);
    }

    private async UniTask FadeOwnerAsync(GameObject owner, float targetAlpha, float duration, CancellationToken cancellationToken)
    {
        if (owner == null)
        {
            return;
        }

        SpriteRenderer[] renderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length <= 0)
        {
            return;
        }

        if (duration <= 0f)
        {
            SetOwnerAlpha(owner, targetAlpha);
            return;
        }

        Sequence sequence = DOTween.Sequence();
        bool hasTween = false;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            DOTween.Kill(renderer);
            sequence.Join(renderer.DOFade(targetAlpha, duration));
            hasTween = true;
        }

        if (!hasTween)
        {
            sequence.Kill();
            return;
        }

        using (cancellationToken.Register(() => sequence.Kill()))
        {
            await sequence.AsyncWaitForCompletion();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void SetOwnerAlpha(GameObject owner, float alpha)
    {
        if (owner == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in owner.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    private void OrientOwnerToward(GameObject owner, Vector3 targetPosition)
    {
        if (!faceExitMoveDirection || owner == null)
        {
            return;
        }

        float directionX = targetPosition.x - owner.transform.position.x;
        if (Mathf.Approximately(directionX, 0f))
        {
            return;
        }

        Vector3 scale = owner.transform.localScale;
        float xScale = Mathf.Approximately(scale.x, 0f) ? 1f : Mathf.Abs(scale.x);
        bool shouldFlip = directionX > 0f ? flipOwnerWhenMovingRight : !flipOwnerWhenMovingRight;
        scale.x = shouldFlip ? -xScale : xScale;
        owner.transform.localScale = scale;
    }
}
