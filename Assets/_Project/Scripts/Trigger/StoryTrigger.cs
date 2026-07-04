using UnityEngine;

public class StoryTrigger : MonoBehaviour
{
    [SerializeField] private string triggerId;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private StoryFlagCondition condition = new();
    [SerializeField] private StoryFlagAction action = new();

    [Header("Game State Lock")]
    [SerializeField] private bool lockGameStateAfterTrigger;
    [SerializeField] private string unlockGameStateFlag;

    private Collider2D[] _triggerColliders;
    private bool _hasLockedGameState;
    private GameState _previousGameState = GameState.Playing;

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
        ReleaseGameStateLock();
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
        LockGameStateIfNeeded();
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

        if (eventData.Value && eventData.FlagId == unlockGameStateFlag)
        {
            ReleaseGameStateLock();
        }
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        RefreshCompletedState();
        ReleaseGameStateLockIfUnlocked();
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

    private void LockGameStateIfNeeded()
    {
        if (!lockGameStateAfterTrigger
            || _hasLockedGameState
            || string.IsNullOrWhiteSpace(unlockGameStateFlag)
            || GameManager.Instance == null)
        {
            return;
        }

        _previousGameState = GameManager.Instance.CurrentState;
        _hasLockedGameState = true;

        if (_previousGameState != GameState.OnDialog)
        {
            GameManager.Instance.SetState(GameState.OnDialog);
        }
    }

    private void ReleaseGameStateLockIfUnlocked()
    {
        if (!_hasLockedGameState
            || string.IsNullOrWhiteSpace(unlockGameStateFlag)
            || FlagManager.Instance == null
            || !FlagManager.Instance.HasFlag(unlockGameStateFlag))
        {
            return;
        }

        ReleaseGameStateLock();
    }

    private void ReleaseGameStateLock()
    {
        if (!_hasLockedGameState)
        {
            return;
        }

        _hasLockedGameState = false;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OnDialog)
        {
            GameManager.Instance.SetState(_previousGameState == GameState.OnDialog
                ? GameState.Playing
                : _previousGameState);
        }
    }
}
