using UnityEngine;

public class StoryTrigger : MonoBehaviour
{
    [SerializeField] private string triggerId;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private StoryFlagCondition condition = new();
    [SerializeField] private StoryFlagAction action = new();

    private Collider2D[] _triggerColliders;

    private string CompletedFlagId => string.IsNullOrWhiteSpace(triggerId)
        ? string.Empty
        : $"trigger_completed_{triggerId}";

    private void OnEnable()
    {
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        RefreshCompletedState();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            TryTrigger();
        }
    }

    public void TryTrigger()
    {
        FlagManager flagManager = FlagManager.Instance;
        if (flagManager == null)
        {
            Debug.LogWarning("[StoryTrigger] Cannot trigger because FlagManager is missing.", this);
            return;
        }

        if (IsCompleted(flagManager))
        {
            RefreshCompletedState();
            return;
        }

        if (condition != null && !condition.IsMet(flagManager.Flags))
        {
            return;
        }

        Trigger();
    }

    protected virtual void Trigger()
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

        if (triggerOnce && !string.IsNullOrEmpty(CompletedFlagId))
        {
            flagManager.SetFlag(CompletedFlagId, true);
        }
    }

    private void OnFlagChanged(FlagChangedEvent eventData)
    {
        if (eventData.FlagId == CompletedFlagId)
        {
            RefreshCompletedState();
        }
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        RefreshCompletedState();
    }

    private bool IsCompleted(FlagManager flagManager)
    {
        return flagManager != null
            && triggerOnce
            && !string.IsNullOrEmpty(CompletedFlagId)
            && flagManager.HasFlag(CompletedFlagId);
    }

    private void RefreshCompletedState()
    {
        bool shouldEnable = !IsCompleted(FlagManager.Instance);
        SetTriggerCollidersEnabled(shouldEnable);
    }

    private void SetTriggerCollidersEnabled(bool shouldEnable)
    {
        if (_triggerColliders == null || _triggerColliders.Length == 0)
        {
            _triggerColliders = GetComponents<Collider2D>();
        }

        for (int i = 0; i < _triggerColliders.Length; i++)
        {
            Collider2D triggerCollider = _triggerColliders[i];
            if (triggerCollider != null && triggerCollider.isTrigger)
            {
                triggerCollider.enabled = shouldEnable;
            }
        }
    }
}
