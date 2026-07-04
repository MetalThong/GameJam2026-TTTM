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
    [SerializeField] private bool flipOwnerOnSpawn;
    [SerializeField] private bool disableAnimatorOnSpawn;
    [SerializeField, Min(0f)] private float spawnFadeDuration = 0.25f;

    [Header("Pickup Animation")]
    [SerializeField] private string pickupAnimationState = "PickUpBox";
    [SerializeField] private bool waitForPickupAnimation = true;
    [SerializeField, Min(0f)] private float fallbackAnimationDuration = 0.8f;
    [SerializeField, Min(0f)] private float postAnimationDelay;

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogue;

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

        owner.transform.localScale = ownerSpawnScale;

        foreach (SpriteRenderer renderer in owner.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer != null)
            {
                renderer.flipX = flipOwnerOnSpawn;
            }
        }

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

        Animator animator = owner.GetComponentInChildren<Animator>(true);
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
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return 0f;
        }

        animator.gameObject.SetActive(true);
        animator.enabled = true;

        string stateName = ResolveAnimatorStateName(animator);
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return 0f;
        }

        animator.Play(Animator.StringToHash(stateName), 0, 0f);
        animator.Update(0f);
        return ResolveClipLength(animator, stateName);
    }

    private string ResolveAnimatorStateName(Animator animator)
    {
        if (animator.HasState(0, Animator.StringToHash(pickupAnimationState)))
        {
            return pickupAnimationState;
        }

        string alternateState = pickupAnimationState.EndsWith("_clip", StringComparison.Ordinal)
            ? pickupAnimationState[..^"_clip".Length]
            : $"{pickupAnimationState}_clip";

        if (animator.HasState(0, Animator.StringToHash(alternateState)))
        {
            return alternateState;
        }

        return null;
    }

    private float ResolveClipLength(Animator animator, string stateName)
    {
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip == null)
            {
                continue;
            }

            if (string.Equals(clip.name, pickupAnimationState, StringComparison.Ordinal)
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
        AnimationClip clip = animation.GetClip(pickupAnimationState);
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
        if (waitDuration <= 0f)
        {
            return;
        }

        await UniTask.Delay(TimeSpan.FromSeconds(waitDuration), cancellationToken: cancellationToken);
    }

    private async UniTask WaitFallbackAnimationAsync(CancellationToken cancellationToken)
    {
        if (!waitForPickupAnimation || fallbackAnimationDuration <= 0f)
        {
            return;
        }

        await UniTask.Delay(TimeSpan.FromSeconds(fallbackAnimationDuration + postAnimationDelay), cancellationToken: cancellationToken);
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
            Debug.LogWarning("OwnerBoxPickupSequence: dialogue assigned but no DialogueManager was found.", this);
            return;
        }

        await manager.PlayDialogueAsync(dialogue, cancellationToken);
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
}
