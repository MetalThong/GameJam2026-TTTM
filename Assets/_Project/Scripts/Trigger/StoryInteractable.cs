using UnityEngine;

public class StoryInteractable : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    [SerializeField] private string interactId;
    [SerializeField] private bool interactOnce = true;
    [SerializeField] private StoryFlagCondition condition = new();
    [SerializeField] private StoryFlagAction action = new();
    [SerializeField] private string promptLocalizationKey = "prompt.interact";

    private Collider2D[] _interactionColliders;

    public virtual string PromptLocalizationKey => promptLocalizationKey;

    private string CompletedFlagId => string.IsNullOrWhiteSpace(interactId)
        ? string.Empty
        : $"interact_completed_{interactId}";

    private void OnEnable()
    {
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        RefreshAvailabilityState();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);
    }

    public bool TryInteract()
    {
        FlagManager flagManager = FlagManager.Instance;
        if (flagManager == null)
        {
            return false;
        }

        if (IsCompleted(flagManager))
        {
            RefreshAvailabilityState();
            return false;
        }

        if (condition != null && !condition.IsMet(flagManager.Flags))
        {
            return false;
        }

        if (!CanInteract())
        {
            return false;
        }

        Interact();
        return true;
    }

    protected virtual bool CanInteract()
    {
        return true;
    }

    protected virtual void Interact()
    {
        OnInteractSucceeded();
    }

    protected virtual void OnInteractSucceeded()
    {
        ExecuteAction();
    }

    protected void ExecuteAction()
    {
        FlagManager flagManager = FlagManager.Instance;
        if (flagManager == null)
        {
            return;
        }

        action?.Execute(flagManager);

        if (interactOnce && !string.IsNullOrEmpty(CompletedFlagId))
        {
            flagManager.SetFlag(CompletedFlagId, true);
        }
    }

    private void OnFlagChanged(FlagChangedEvent eventData)
    {
        RefreshAvailabilityState();
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        RefreshAvailabilityState();
    }

    private bool IsCompleted(FlagManager flagManager)
    {
        return flagManager != null
            && interactOnce
            && !string.IsNullOrEmpty(CompletedFlagId)
            && flagManager.HasFlag(CompletedFlagId);
    }

    private void RefreshAvailabilityState()
    {
        bool shouldEnable = IsAvailable(FlagManager.Instance);
        SetInteractionCollidersEnabled(shouldEnable);
    }

    private bool IsAvailable(FlagManager flagManager)
    {
        return flagManager != null
            && !IsCompleted(flagManager)
            && (condition == null || condition.IsMet(flagManager.Flags));
    }

    private void SetInteractionCollidersEnabled(bool shouldEnable)
    {
        if (_interactionColliders == null || _interactionColliders.Length == 0)
        {
            _interactionColliders = GetComponents<Collider2D>();
        }

        for (int i = 0; i < _interactionColliders.Length; i++)
        {
            Collider2D interactionCollider = _interactionColliders[i];
            if (interactionCollider != null)
            {
                interactionCollider.enabled = shouldEnable;
            }
        }
    }
}
