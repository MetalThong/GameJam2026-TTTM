using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CatMeowMashInteractable : MashStoryInteractable
{
    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogue;
    [SerializeField] private string meowSfxId = "cat_meow";

    [Header("Owner Spawn")]
    [Tooltip("Optional direct prefab reference. If empty, ownerResourcePath is loaded from Resources.")]
    [SerializeField] private GameObject ownerPrefab;
    [SerializeField] private string ownerResourcePath = "Main/thang chu di";
    [SerializeField] private Transform ownerSpawnPoint;
    [SerializeField] private Vector3 ownerSpawnOffset;

    [Header("Owner Exit")]
    [SerializeField] private DialogueSO ownerExitDialogue;
    [SerializeField] private string ownerExitAnimationState = "thang chu di_clip";
    [SerializeField] private bool waitForOwnerAnimation = true;
    [SerializeField, Min(0f)] private float postOwnerAnimationDelay;

    [Header("Screen Fade")]
    [SerializeField] private bool fadeScreenAfterOwnerDialogue = true;
    [SerializeField, Range(0f, 1f)] private float screenFadeMaxAlpha = 0.45f;
    [SerializeField, Min(0f)] private float screenFadeToBlackDuration = 0.35f;
    [SerializeField, Min(0f)] private float screenFadeHoldDuration = 0.2f;
    [SerializeField, Min(0f)] private float screenFadeFromBlackDuration = 0.35f;

    [Header("Completion Cleanup")]
    [SerializeField] private GameObject[] hideOnComplete;

    private bool _isPlaying;
    private GameObject _spawnedOwner;
    private GameObject _screenFadeRoot;
    private CanvasGroup _screenFadeGroup;
    private Tween _screenFadeTween;

    private void OnDestroy()
    {
        _screenFadeTween?.Kill();

        if (_screenFadeRoot != null)
        {
            Destroy(_screenFadeRoot);
        }
    }

    protected override void OnInteractSucceeded()
    {
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
            if (AudioManager.Instance != null && !string.IsNullOrWhiteSpace(meowSfxId))
            {
                AudioManager.Instance.PlaySfx(meowSfxId);
            }

            await PlayDialogueIfAssignedAsync(dialogue, destroyToken);

            _spawnedOwner = SpawnOwner();

            await PlayDialogueIfAssignedAsync(ownerExitDialogue, destroyToken);

            float animationDuration = PlayOwnerExitAnimation(_spawnedOwner);
            await PlayFadeAndAnimationWaitAsync(animationDuration, destroyToken);

            ExecuteAction();
            HideCompletionTargets();
            gameObject.SetActive(false);
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

        Vector3 spawnPosition = ownerSpawnPoint != null ? ownerSpawnPoint.position : transform.position;
        GameObject owner = Instantiate(prefab, spawnPosition + ownerSpawnOffset, Quaternion.identity);
        owner.name = prefab.name;
        owner.SetActive(true);
        return owner;
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

    private async UniTask PlayFadeAndAnimationWaitAsync(float animationDuration, CancellationToken cancellationToken)
    {
        UniTask fadeTask = PlayScreenFadeAsync(cancellationToken);

        if (!waitForOwnerAnimation || animationDuration <= 0f)
        {
            await fadeTask;
            return;
        }

        float waitDuration = animationDuration + postOwnerAnimationDelay;
        UniTask animationWaitTask = UniTask.Delay(TimeSpan.FromSeconds(waitDuration), cancellationToken: cancellationToken);
        await UniTask.WhenAll(fadeTask, animationWaitTask);
    }

    private async UniTask PlayScreenFadeAsync(CancellationToken cancellationToken)
    {
        if (!fadeScreenAfterOwnerDialogue || screenFadeMaxAlpha <= 0f)
        {
            return;
        }

        CanvasGroup fadeGroup = EnsureScreenFade();
        fadeGroup.alpha = 0f;
        fadeGroup.gameObject.SetActive(true);

        _screenFadeTween?.Kill();

        Sequence sequence = DOTween.Sequence();
        sequence.Append(fadeGroup.DOFade(screenFadeMaxAlpha, screenFadeToBlackDuration));

        if (screenFadeHoldDuration > 0f)
        {
            sequence.AppendInterval(screenFadeHoldDuration);
        }

        sequence.Append(fadeGroup.DOFade(0f, screenFadeFromBlackDuration));
        _screenFadeTween = sequence;

        await sequence.AsyncWaitForCompletion();

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_screenFadeTween == sequence)
        {
            _screenFadeTween = null;
        }

        fadeGroup.gameObject.SetActive(false);
    }

    private CanvasGroup EnsureScreenFade()
    {
        if (_screenFadeGroup != null)
        {
            return _screenFadeGroup;
        }

        _screenFadeRoot = new GameObject("OwnerExitScreenFade", typeof(Canvas), typeof(CanvasGroup));
        Canvas canvas = _screenFadeRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        _screenFadeGroup = _screenFadeRoot.GetComponent<CanvasGroup>();
        _screenFadeGroup.blocksRaycasts = false;
        _screenFadeGroup.interactable = false;

        GameObject imageObject = new("Black");
        imageObject.transform.SetParent(_screenFadeRoot.transform, false);

        RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = imageObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        _screenFadeRoot.SetActive(false);
        return _screenFadeGroup;
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
