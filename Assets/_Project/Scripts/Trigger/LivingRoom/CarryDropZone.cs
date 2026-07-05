using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CarryDropZone : MonoBehaviour
{
    [Header("Drop Match")]
    [SerializeField] private SceneId sceneId = SceneId.LivingRoomPart4;
    [SerializeField] private string carryId = "fish";
    [SerializeField] private Collider2D dropZoneCollider;
    [SerializeField] private bool requireDropInsideCollider = true;

    [Header("Completion")]
    [SerializeField] private string completionFlagId = "lr4_fish_delivered";
    [SerializeField] private StoryFlagCondition condition = new();
    [SerializeField] private StoryFlagAction action = new();
    [SerializeField] private GameObject[] hideOnComplete;
    [SerializeField] private GameObject[] showOnComplete;

    [Header("Dropped Object")]
    [SerializeField] private bool destroyDroppedObject = true;
    [SerializeField, Min(0f)] private float droppedObjectFadeDuration = 0.2f;

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO completionDialogue;
    [SerializeField, Min(0f)] private float startDelay = 0.1f;

    private bool _isPlaying;

    private void OnEnable()
    {
        CarryManager.CarriedObjectDropped += OnCarriedObjectDropped;
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        RefreshState();
    }

    private void OnDisable()
    {
        CarryManager.CarriedObjectDropped -= OnCarriedObjectDropped;
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);
    }

    private void OnCarriedObjectDropped(CarryManager.CarryDropInfo dropInfo)
    {
        if (_isPlaying || IsCompleted() || !IsMatchingDrop(dropInfo))
        {
            return;
        }

        CompleteAsync(dropInfo, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid CompleteAsync(CarryManager.CarryDropInfo dropInfo, CancellationToken cancellationToken)
    {
        _isPlaying = true;
        SetDropZoneEnabled(false);

        try
        {
            if (startDelay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: cancellationToken);
            }

            await HideDroppedObjectAsync(dropInfo.DroppedObject, cancellationToken);
            await PlayDialogueIfAssignedAsync(cancellationToken);
            ExecuteCompletion();
            SetTargetsActive(hideOnComplete, false);
            SetTargetsActive(showOnComplete, true);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isPlaying = false;
            RefreshState();
        }
    }

    private bool IsMatchingDrop(CarryManager.CarryDropInfo dropInfo)
    {
        if (!dropInfo.DropScene.IsValid()
            || !string.Equals(dropInfo.DropScene.name, sceneId.ToString(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(carryId)
            || !string.Equals(dropInfo.CarryId, carryId, StringComparison.Ordinal))
        {
            return false;
        }

        if (condition != null && FlagManager.Instance != null && !condition.IsMet(FlagManager.Instance.Flags))
        {
            return false;
        }

        if (!requireDropInsideCollider)
        {
            return true;
        }

        Collider2D targetCollider = ResolveDropZoneCollider();
        return targetCollider != null && targetCollider.OverlapPoint(dropInfo.DropPosition);
    }

    private async UniTask HideDroppedObjectAsync(GameObject droppedObject, CancellationToken cancellationToken)
    {
        if (!destroyDroppedObject || droppedObject == null)
        {
            return;
        }

        PrepareDroppedObjectForHide(droppedObject);

        SpriteRenderer[] renderers = droppedObject.GetComponentsInChildren<SpriteRenderer>(true);
        if (droppedObjectFadeDuration > 0f && renderers.Length > 0)
        {
            Sequence sequence = DOTween.Sequence();
            bool hasTween = false;

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                DOTween.Kill(renderer);
                sequence.Join(renderer.DOFade(0f, droppedObjectFadeDuration));
                hasTween = true;
            }

            if (hasTween)
            {
                using (cancellationToken.Register(() => sequence.Kill()))
                {
                    await sequence.AsyncWaitForCompletion();
                }
            }
            else
            {
                sequence.Kill();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (droppedObject != null)
        {
            Destroy(droppedObject);
        }
    }

    private async UniTask PlayDialogueIfAssignedAsync(CancellationToken cancellationToken)
    {
        if (completionDialogue == null)
        {
            return;
        }

        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            Debug.LogWarning("CarryDropZone: completionDialogue assigned but no DialogueManager was found.", this);
            return;
        }

        await manager.PlayDialogueAsync(completionDialogue, cancellationToken);
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
        SetDropZoneEnabled(!IsCompleted());
    }

    private bool IsCompleted()
    {
        return !string.IsNullOrWhiteSpace(completionFlagId)
            && FlagManager.Instance != null
            && FlagManager.Instance.HasFlag(completionFlagId);
    }

    private Collider2D ResolveDropZoneCollider()
    {
        if (dropZoneCollider != null)
        {
            return dropZoneCollider;
        }

        dropZoneCollider = GetComponent<Collider2D>();
        return dropZoneCollider;
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

    private void SetDropZoneEnabled(bool shouldEnable)
    {
        Collider2D targetCollider = ResolveDropZoneCollider();
        if (targetCollider != null)
        {
            targetCollider.enabled = shouldEnable;
        }
    }

    private static void PrepareDroppedObjectForHide(GameObject droppedObject)
    {
        foreach (Collider2D targetCollider in droppedObject.GetComponentsInChildren<Collider2D>(true))
        {
            targetCollider.enabled = false;
        }

        foreach (Rigidbody2D targetRigidbody in droppedObject.GetComponentsInChildren<Rigidbody2D>(true))
        {
            targetRigidbody.simulated = false;
        }

        foreach (MonoBehaviour behaviour in droppedObject.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is IInteractable)
            {
                behaviour.enabled = false;
            }
        }
    }

    private static void SetTargetsActive(GameObject[] targets, bool active)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetActive(active);
            }
        }
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
