using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RightCatCookieTradeInteractable : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    [Header("Prompt")]
    [SerializeField] private string promptLocalizationKey = "prompt.interact";

    [Header("Cookie")]
    [SerializeField] private string cookieInMouthFlagId = "lr4_cookie_in_mouth";
    [SerializeField] private string cookieGivenFlagId = "lr4_cookie_given_to_right_cat";
    [SerializeField] private GameObject cookieInMouthObject;
    [SerializeField] private bool hideCookieObjectOnTrade = true;
    [SerializeField] private bool unsetCookieInMouthFlagOnTrade = true;

    [Header("Fish Drop")]
    [SerializeField] private GameObject fishObject;
    [SerializeField] private Transform fishTransform;
    [SerializeField] private Transform fishDropTarget;
    [SerializeField] private string fishDeliveredFlagId = "lr4_fish_delivered";
    [SerializeField] private Vector3 fishDropOffset;
    [SerializeField, Min(0f)] private float fishDropStartDelay = 0.1f;
    [SerializeField, Min(0f)] private float fishDropDuration = 0.4f;
    [SerializeField] private Ease fishDropEase = Ease.OutBounce;
    [SerializeField] private bool preserveFishZ = true;
    [SerializeField] private bool showFishObjectOnTrade = true;
    [SerializeField] private bool disableFishInteractionUntilDropped = true;

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogueBeforeDrop;
    [SerializeField] private DialogueSO dialogueAfterDrop;

    [Header("Flags")]
    [SerializeField] private string completionFlagId = "lr4_fish_dropped";
    [SerializeField] private StoryFlagCondition condition = new();
    [SerializeField] private StoryFlagAction action = new();

    [Header("SFX")]
    [SerializeField] private string tradeSfxId;
    [SerializeField] private string fishDropSfxId;
    [SerializeField] private bool playFishDropSfxAtFishPosition = true;

    private Collider2D[] _interactionColliders;
    private bool _isPlaying;
    private Tween _fishTween;

    public string PromptLocalizationKey => promptLocalizationKey;

    private void OnEnable()
    {
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        RefreshState();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        _fishTween?.Kill();
    }

    private void OnDestroy()
    {
        _fishTween?.Kill();
    }

    public bool TryInteract()
    {
        if (_isPlaying || !IsAvailable())
        {
            return false;
        }

        TradeAsync(this.GetCancellationTokenOnDestroy()).Forget();
        return true;
    }

    private async UniTaskVoid TradeAsync(CancellationToken cancellationToken)
    {
        _isPlaying = true;
        SetInteractionCollidersEnabled(false);

        try
        {
            PlaySfx(tradeSfxId, transform.position);
            await PlayDialogueIfAssignedAsync(dialogueBeforeDrop, cancellationToken);

            ConsumeCookie();

            if (fishDropStartDelay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(fishDropStartDelay), cancellationToken: cancellationToken);
            }

            await DropFishAsync(cancellationToken);
            PlaySfx(fishDropSfxId, ResolveFishPosition());
            await PlayDialogueIfAssignedAsync(dialogueAfterDrop, cancellationToken);

            ExecuteCompletion();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _fishTween = null;
            _isPlaying = false;
            RefreshState();
        }
    }

    private async UniTask DropFishAsync(CancellationToken cancellationToken)
    {
        Transform targetFishTransform = ResolveFishTransform();
        if (targetFishTransform == null)
        {
            Debug.LogWarning("RightCatCookieTradeInteractable: fishObject or fishTransform is not assigned.", this);
            return;
        }

        if (showFishObjectOnTrade && fishObject != null)
        {
            fishObject.SetActive(true);
        }

        SetFishInteractionEnabled(false);

        Vector3 targetPosition = fishDropTarget != null
            ? fishDropTarget.position
            : targetFishTransform.position + fishDropOffset;

        if (preserveFishZ)
        {
            targetPosition.z = targetFishTransform.position.z;
        }

        if (fishDropDuration <= 0f)
        {
            targetFishTransform.position = targetPosition;
        }
        else
        {
            _fishTween?.Kill();
            _fishTween = targetFishTransform.DOMove(targetPosition, fishDropDuration).SetEase(fishDropEase);
            using (cancellationToken.Register(() => _fishTween?.Kill()))
            {
                await _fishTween.AsyncWaitForCompletion();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        SetFishInteractionEnabled(true);
    }

    private void ConsumeCookie()
    {
        if (hideCookieObjectOnTrade && cookieInMouthObject != null)
        {
            cookieInMouthObject.SetActive(false);
        }

        FlagManager flagManager = FlagManager.Instance;
        if (flagManager == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(cookieGivenFlagId))
        {
            flagManager.SetFlag(cookieGivenFlagId, true);
        }

        if (unsetCookieInMouthFlagOnTrade && !string.IsNullOrWhiteSpace(cookieInMouthFlagId))
        {
            flagManager.SetFlag(cookieInMouthFlagId, false);
        }
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

    private void RefreshState()
    {
        if (FlagManager.Instance == null)
        {
            return;
        }

        bool completed = IsCompleted();
        bool delivered = IsFishDelivered();

        if (delivered)
        {
            if (fishObject != null)
            {
                fishObject.SetActive(false);
            }

            SetFishInteractionEnabled(false);
            SetInteractionCollidersEnabled(false);
            return;
        }

        if (completed)
        {
            RestoreDroppedFishPose();
        }

        SetInteractionCollidersEnabled(IsAvailable());

        if (disableFishInteractionUntilDropped)
        {
            SetFishInteractionEnabled(completed);
        }
    }

    private bool IsAvailable()
    {
        FlagManager flagManager = FlagManager.Instance;
        if (flagManager == null || _isPlaying || IsCompleted())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(cookieInMouthFlagId) || !flagManager.HasFlag(cookieInMouthFlagId))
        {
            return false;
        }

        return condition == null || condition.IsMet(flagManager.Flags);
    }

    private bool IsCompleted()
    {
        return !string.IsNullOrWhiteSpace(completionFlagId)
            && FlagManager.Instance != null
            && FlagManager.Instance.HasFlag(completionFlagId);
    }

    private bool IsFishDelivered()
    {
        return !string.IsNullOrWhiteSpace(fishDeliveredFlagId)
            && FlagManager.Instance != null
            && FlagManager.Instance.HasFlag(fishDeliveredFlagId);
    }

    private void RestoreDroppedFishPose()
    {
        Transform targetFishTransform = ResolveFishTransform();
        if (targetFishTransform == null)
        {
            return;
        }

        if (fishObject != null && showFishObjectOnTrade)
        {
            fishObject.SetActive(true);
        }

        Vector3 targetPosition = fishDropTarget != null
            ? fishDropTarget.position
            : targetFishTransform.position + fishDropOffset;

        if (preserveFishZ)
        {
            targetPosition.z = targetFishTransform.position.z;
        }

        targetFishTransform.position = targetPosition;
    }

    private async UniTask PlayDialogueIfAssignedAsync(DialogueSO dialogue, CancellationToken cancellationToken)
    {
        if (dialogue == null)
        {
            return;
        }

        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            Debug.LogWarning("RightCatCookieTradeInteractable: dialogue assigned but no DialogueManager was found.", this);
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

    private Transform ResolveFishTransform()
    {
        if (fishTransform != null)
        {
            return fishTransform;
        }

        if (fishObject != null)
        {
            fishTransform = fishObject.transform;
        }

        return fishTransform;
    }

    private Vector3 ResolveFishPosition()
    {
        Transform targetFishTransform = ResolveFishTransform();
        return targetFishTransform != null ? targetFishTransform.position : transform.position;
    }

    private void SetFishInteractionEnabled(bool shouldEnable)
    {
        if (!disableFishInteractionUntilDropped || fishObject == null)
        {
            return;
        }

        foreach (Collider2D targetCollider in fishObject.GetComponentsInChildren<Collider2D>(true))
        {
            targetCollider.enabled = shouldEnable;
        }

        foreach (MonoBehaviour behaviour in fishObject.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != this && behaviour is IInteractable)
            {
                behaviour.enabled = shouldEnable;
            }
        }
    }

    private void SetInteractionCollidersEnabled(bool shouldEnable)
    {
        if (_interactionColliders == null || _interactionColliders.Length == 0)
        {
            _interactionColliders = GetComponents<Collider2D>();
        }

        for (int i = 0; i < _interactionColliders.Length; i++)
        {
            Collider2D targetCollider = _interactionColliders[i];
            if (targetCollider != null)
            {
                targetCollider.enabled = shouldEnable;
            }
        }
    }

    private void PlaySfx(string sfxId, Vector3 position)
    {
        if (AudioManager.Instance == null || string.IsNullOrWhiteSpace(sfxId))
        {
            return;
        }

        if (playFishDropSfxAtFishPosition)
        {
            AudioManager.Instance.PlaySfx(sfxId, position);
            return;
        }

        AudioManager.Instance.PlaySfx(sfxId);
    }

    private void OnFlagChanged(FlagChangedEvent eventData)
    {
        RefreshState();
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        RefreshState();
    }
}
