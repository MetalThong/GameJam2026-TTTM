using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MouthPickupInteractable : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    [Header("Prompt")]
    [SerializeField] private string promptLocalizationKey = "prompt.interact";

    [Header("Pickup")]
    [SerializeField] private Transform itemTransform;
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform mouthAnchor;
    [SerializeField] private string mouthAnchorName = "CarryAnchor";
    [SerializeField] private Vector3 mouthLocalPosition;
    [SerializeField] private Vector3 mouthLocalEulerAngles;
    [SerializeField, Min(0f)] private float flyDuration = 0.35f;
    [SerializeField] private Ease flyEase = Ease.OutQuad;
    [SerializeField] private bool parentToMouthAnchorOnComplete = true;
    [SerializeField] private bool restoreToMouthWhenFlagExists = true;

    [Header("Availability")]
    [SerializeField] private bool interactOnce = true;
    [SerializeField] private string completionFlagId = "lr4_cookie_in_mouth";
    [SerializeField] private string blockedFlagId = "lr4_cookie_given_to_right_cat";
    [SerializeField] private bool hideVisualWhenBlocked = true;
    [SerializeField] private StoryFlagCondition condition = new();
    [SerializeField] private StoryFlagAction action = new();

    [Header("After Pickup")]
    [SerializeField] private bool disableCollidersOnPickup = true;
    [SerializeField] private bool disableRigidbodiesOnPickup = true;
    [SerializeField] private bool disableInteractablesOnPickup = true;

    private Collider2D[] _colliders;
    private bool _isPlaying;
    private Tween _flyTween;

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
        _flyTween?.Kill();
    }

    private void OnDestroy()
    {
        _flyTween?.Kill();
    }

    public bool TryInteract()
    {
        if (_isPlaying || !IsAvailable())
        {
            return false;
        }

        PickupAsync(this.GetCancellationTokenOnDestroy()).Forget();
        return true;
    }

    private async UniTaskVoid PickupAsync(CancellationToken cancellationToken)
    {
        _isPlaying = true;
        SetInteractionCollidersEnabled(false);

        try
        {
            Transform item = ResolveItemTransform();
            Transform anchor = ResolveMouthAnchor();
            if (item == null || anchor == null)
            {
                Debug.LogWarning("MouthPickupInteractable: itemTransform or mouthAnchor could not be resolved.", this);
                return;
            }

            GameObject root = ResolveVisualRoot();
            if (root != null)
            {
                root.SetActive(true);
            }

            Vector3 targetPosition = anchor.TransformPoint(mouthLocalPosition);
            if (flyDuration <= 0f)
            {
                item.position = targetPosition;
            }
            else
            {
                _flyTween?.Kill();
                _flyTween = item.DOMove(targetPosition, flyDuration).SetEase(flyEase);
                using (cancellationToken.Register(() => _flyTween?.Kill()))
                {
                    await _flyTween.AsyncWaitForCompletion();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            AttachToMouth(item, anchor);
            PreparePickedUpObject();
            ExecuteCompletion();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _flyTween = null;
            _isPlaying = false;
            RefreshState();
        }
    }

    private void RefreshState()
    {
        if (FlagManager.Instance == null)
        {
            return;
        }

        bool isBlocked = IsBlocked();
        if (isBlocked && hideVisualWhenBlocked)
        {
            GameObject root = ResolveVisualRoot();
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        if (!isBlocked && IsCompleted() && restoreToMouthWhenFlagExists)
        {
            Transform item = ResolveItemTransform();
            Transform anchor = ResolveMouthAnchor();
            if (item != null && anchor != null)
            {
                AttachToMouth(item, anchor);
                PreparePickedUpObject();
            }
        }

        SetInteractionCollidersEnabled(IsAvailable());
    }

    private bool IsAvailable()
    {
        FlagManager flagManager = FlagManager.Instance;
        if (flagManager == null || _isPlaying || IsBlocked())
        {
            return false;
        }

        if (interactOnce && IsCompleted())
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

    private bool IsBlocked()
    {
        return !string.IsNullOrWhiteSpace(blockedFlagId)
            && FlagManager.Instance != null
            && FlagManager.Instance.HasFlag(blockedFlagId);
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

    private void AttachToMouth(Transform item, Transform anchor)
    {
        if (!parentToMouthAnchorOnComplete || item == null || anchor == null)
        {
            return;
        }

        item.SetParent(anchor, true);
        item.localPosition = mouthLocalPosition;
        item.localRotation = Quaternion.Euler(mouthLocalEulerAngles);
    }

    private void PreparePickedUpObject()
    {
        GameObject root = ResolveVisualRoot();
        if (root == null)
        {
            return;
        }

        if (disableCollidersOnPickup)
        {
            foreach (Collider2D targetCollider in root.GetComponentsInChildren<Collider2D>(true))
            {
                targetCollider.enabled = false;
            }
        }

        if (disableRigidbodiesOnPickup)
        {
            foreach (Rigidbody2D targetRigidbody in root.GetComponentsInChildren<Rigidbody2D>(true))
            {
                targetRigidbody.simulated = false;
            }
        }

        if (!disableInteractablesOnPickup)
        {
            return;
        }

        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != this && behaviour is IInteractable)
            {
                behaviour.enabled = false;
            }
        }
    }

    private Transform ResolveItemTransform()
    {
        if (itemTransform != null)
        {
            return itemTransform;
        }

        itemTransform = transform;
        return itemTransform;
    }

    private GameObject ResolveVisualRoot()
    {
        if (visualRoot != null)
        {
            return visualRoot;
        }

        visualRoot = ResolveItemTransform() != null ? ResolveItemTransform().gameObject : gameObject;
        return visualRoot;
    }

    private Transform ResolveMouthAnchor()
    {
        if (mouthAnchor != null)
        {
            return mouthAnchor;
        }

        GameObject player = string.IsNullOrWhiteSpace(playerTag)
            ? null
            : GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            return null;
        }

        Transform namedAnchor = FindChildRecursive(player.transform, mouthAnchorName);
        mouthAnchor = namedAnchor != null ? namedAnchor : player.transform;
        return mouthAnchor;
    }

    private void SetInteractionCollidersEnabled(bool shouldEnable)
    {
        if (_colliders == null || _colliders.Length == 0)
        {
            _colliders = GetComponents<Collider2D>();
        }

        for (int i = 0; i < _colliders.Length; i++)
        {
            Collider2D targetCollider = _colliders[i];
            if (targetCollider != null)
            {
                targetCollider.enabled = shouldEnable;
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

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
