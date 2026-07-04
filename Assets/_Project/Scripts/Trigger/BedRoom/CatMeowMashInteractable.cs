using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class CatMeowMashInteractable : MashStoryInteractable
{
    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogue;
    [SerializeField] private string meowSfxId = "cat_meow";
    [SerializeField, Min(0f)] private float meowSfxCooldown = 0.25f;

    [Header("Owner Spawn")]
    [Tooltip("Optional direct prefab reference. If empty, ownerResourcePath is loaded from Resources.")]
    [SerializeField] private GameObject ownerPrefab;
    [SerializeField] private string ownerResourcePath = "Main/thang chu di";
    [SerializeField] private Transform ownerSpawnPoint;
    [SerializeField] private Vector3 ownerSpawnOffset;
    [SerializeField] private bool useOwnerSpawnPosition;
    [SerializeField] private Vector3 ownerSpawnPosition;
    [SerializeField] private bool disableOwnerAnimatorUntilWalk = true;
    [SerializeField, Min(0f)] private float ownerSpawnFadeDuration = 0.35f;
    [SerializeField] private string ownerSpawnedFlag = "boss_wake_up";

    [Header("Owner Exit")]
    [SerializeField] private DialogueSO ownerExitDialogue;
    [SerializeField] private string ownerExitAnimationState = "BossWalk";
    [SerializeField] private Transform ownerExitPoint;
    [SerializeField] private Vector3 ownerExitOffset;
    [SerializeField] private Vector3 ownerWalkOffset;
    [SerializeField, Min(0f)] private float ownerWalkDuration = 1.4f;
    [SerializeField, Min(0f)] private float ownerDespawnFadeDuration = 0.35f;
    [SerializeField] private bool faceMoveDirection = true;
    [SerializeField] private bool flipOwnerWhenMovingRight = true;
    [SerializeField] private bool waitForOwnerAnimation = true;
    [SerializeField, Min(0f)] private float postOwnerAnimationDelay;

    [Header("Completion Cleanup")]
    [SerializeField] private GameObject[] hideOnComplete;

    private bool _isPlaying;
    private GameObject _spawnedOwner;
    private float _lastMeowSfxTime = float.NegativeInfinity;

    protected override void Interact()
    {
        PlayMeowSfx();
        base.Interact();
    }

    protected override void OnInteractSucceeded()
    {
        if (_isPlaying)
        {
            return;
        }

        HideCompletionTargets();
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
            await PlayDialogueIfAssignedAsync(dialogue, destroyToken);

            _spawnedOwner = SpawnOwner();
            SetOwnerSpawnedFlag(_spawnedOwner);
            await FadeOwnerAsync(_spawnedOwner, 1f, ownerSpawnFadeDuration, destroyToken);

            await PlayDialogueIfAssignedAsync(ownerExitDialogue, destroyToken);

            float animationDuration = PlayOwnerExitAnimation(_spawnedOwner);
            await MoveOwnerToExitAsync(_spawnedOwner, animationDuration, destroyToken);
            await FadeOwnerAsync(_spawnedOwner, 0f, ownerDespawnFadeDuration, destroyToken);
            DespawnOwner();

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

    private void HideCompletionTargets()
    {
        if (hideOnComplete == null)
        {
            return;
        }

        foreach (GameObject target in hideOnComplete)
        {
            if (target != null)
            {
                target.SetActive(false);
            }
        }
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
            Debug.LogWarning("CatMeowMashInteractable: dialogue assigned but no DialogueManager was found.", this);
            return;
        }

        await manager.PlayDialogueAsync(dialogueToPlay, cancellationToken);
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
            Debug.LogWarning($"CatMeowMashInteractable: owner prefab could not be resolved from Resources path '{ownerResourcePath}'.", this);
            return null;
        }

        Vector3 spawnPosition = ResolveOwnerSpawnPosition();
        GameObject owner = Instantiate(prefab, spawnPosition + ownerSpawnOffset, Quaternion.identity);
        owner.name = prefab.name;
        owner.SetActive(true);
        SetOwnerAnimatorsEnabled(owner, !disableOwnerAnimatorUntilWalk);
        SetOwnerAlpha(owner, 0f);
        return owner;
    }

    private void PlayMeowSfx()
    {
        if (Time.time < _lastMeowSfxTime + meowSfxCooldown)
        {
            return;
        }

        _lastMeowSfxTime = Time.time;
        AudioFeedback.PlaySfx(meowSfxId);
    }

    private void SetOwnerSpawnedFlag(GameObject owner)
    {
        if (owner == null || string.IsNullOrWhiteSpace(ownerSpawnedFlag) || FlagManager.Instance == null)
        {
            return;
        }

        FlagManager.Instance.SetFlag(ownerSpawnedFlag, true);
    }

    private Vector3 ResolveOwnerSpawnPosition()
    {
        if (useOwnerSpawnPosition)
        {
            return ownerSpawnPosition;
        }

        return ownerSpawnPoint != null ? ownerSpawnPoint.position : transform.position;
    }

    private float PlayOwnerExitAnimation(GameObject owner)
    {
        if (owner == null || string.IsNullOrWhiteSpace(ownerExitAnimationState))
        {
            return 0f;
        }

        Animator animator = owner.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            return PlayAnimatorState(animator);
        }

        Animation animation = owner.GetComponentInChildren<Animation>(true);
        if (animation != null)
        {
            return PlayLegacyAnimation(animation);
        }

        Debug.LogWarning("CatMeowMashInteractable: spawned owner has no Animator or Animation component.", owner);
        return 0f;
    }

    private float PlayAnimatorState(Animator animator)
    {
        if (animator.runtimeAnimatorController == null || animator.layerCount <= 0)
        {
            Debug.LogWarning("CatMeowMashInteractable: owner Animator has no controller.", animator);
            return 0f;
        }

        string stateName = ResolveAnimatorStateName(animator);
        if (string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogWarning($"CatMeowMashInteractable: owner Animator has no state matching '{ownerExitAnimationState}'.", animator);
            return 0f;
        }

        animator.gameObject.SetActive(true);
        animator.enabled = true;
        animator.Play(Animator.StringToHash(stateName), 0, 0f);
        animator.Update(0f);

        return ResolveClipLength(animator, stateName);
    }

    private string ResolveAnimatorStateName(Animator animator)
    {
        if (animator.HasState(0, Animator.StringToHash(ownerExitAnimationState)))
        {
            return ownerExitAnimationState;
        }

        string fallbackWithoutSuffix = ownerExitAnimationState.EndsWith("_clip", StringComparison.Ordinal)
            ? ownerExitAnimationState[..^"_clip".Length]
            : $"{ownerExitAnimationState}_clip";

        if (animator.HasState(0, Animator.StringToHash(fallbackWithoutSuffix)))
        {
            return fallbackWithoutSuffix;
        }

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (animator.HasState(0, Animator.StringToHash(clip.name)))
            {
                return clip.name;
            }

            string clipStateName = $"{clip.name}_clip";
            if (animator.HasState(0, Animator.StringToHash(clipStateName)))
            {
                return clipStateName;
            }
        }

        return null;
    }

    private float ResolveClipLength(Animator animator, string stateName)
    {
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (string.Equals(clip.name, ownerExitAnimationState, StringComparison.Ordinal)
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
        AnimationClip clip = animation.GetClip(ownerExitAnimationState);
        if (clip == null)
        {
            foreach (AnimationState state in animation)
            {
                clip = state.clip;
                break;
            }
        }

        if (clip == null)
        {
            Debug.LogWarning($"CatMeowMashInteractable: owner Animation has no clip matching '{ownerExitAnimationState}'.", animation);
            return 0f;
        }

        animation.clip = clip;
        animation.Play(clip.name);
        return clip.length;
    }

    private async UniTask WaitForOwnerAnimationAsync(float animationDuration, CancellationToken cancellationToken)
    {
        if (!waitForOwnerAnimation || animationDuration <= 0f)
        {
            return;
        }

        float waitDuration = animationDuration + postOwnerAnimationDelay;
        if (waitDuration <= 0f)
        {
            return;
        }

        await UniTask.Delay(TimeSpan.FromSeconds(waitDuration), cancellationToken: cancellationToken);
    }

    private async UniTask MoveOwnerToExitAsync(GameObject owner, float animationDuration, CancellationToken cancellationToken)
    {
        if (owner == null)
        {
            return;
        }

        bool hasExitPoint = ownerExitPoint != null;
        bool hasWalkOffset = ownerWalkOffset.sqrMagnitude > 0.0001f;
        if (!hasExitPoint && !hasWalkOffset)
        {
            await WaitForOwnerAnimationAsync(animationDuration, cancellationToken);
            return;
        }

        Vector3 targetPosition = hasExitPoint
            ? ownerExitPoint.position + ownerExitOffset
            : owner.transform.position + ownerWalkOffset;
        targetPosition.z = owner.transform.position.z;

        OrientOwnerToward(owner, targetPosition);

        float moveDuration = Mathf.Max(0f, ownerWalkDuration);
        if (waitForOwnerAnimation)
        {
            moveDuration = Mathf.Max(moveDuration, animationDuration + postOwnerAnimationDelay);
        }

        if (moveDuration <= 0f)
        {
            owner.transform.position = targetPosition;
            return;
        }

        Tween tween = owner.transform.DOMove(targetPosition, moveDuration).SetEase(Ease.Linear);
        using (cancellationToken.Register(() => tween.Kill()))
        {
            await tween.AsyncWaitForCompletion();
        }

        cancellationToken.ThrowIfCancellationRequested();
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

    private void OrientOwnerToward(GameObject owner, Vector3 targetPosition)
    {
        if (!faceMoveDirection || owner == null)
        {
            return;
        }

        float directionX = targetPosition.x - owner.transform.position.x;
        if (Mathf.Abs(directionX) <= 0.01f)
        {
            return;
        }

        bool shouldFlip = directionX > 0f ? flipOwnerWhenMovingRight : !flipOwnerWhenMovingRight;
        foreach (SpriteRenderer renderer in owner.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer != null)
            {
                renderer.flipX = shouldFlip;
            }
        }
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

    private static void SetOwnerAnimatorsEnabled(GameObject owner, bool enabled)
    {
        if (owner == null)
        {
            return;
        }

        foreach (Animator animator in owner.GetComponentsInChildren<Animator>(true))
        {
            if (animator != null)
            {
                animator.enabled = enabled;
            }
        }
    }

    private void DespawnOwner()
    {
        if (_spawnedOwner == null)
        {
            return;
        }

        Destroy(_spawnedOwner);
        _spawnedOwner = null;
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
