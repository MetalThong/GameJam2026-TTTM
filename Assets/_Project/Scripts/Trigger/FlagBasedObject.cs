using UnityEngine;

public class FlagBasedObject : MonoBehaviour
{
    [SerializeField] protected GameObject target;
    [SerializeField] protected string requiredFlag;
    [SerializeField] protected bool activeWhenFlagExists = true;

    private bool _warnedInvalidTarget;

    private void OnEnable()
    {
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        Refresh();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);
    }

    private void OnFlagChanged(FlagChangedEvent eventData)
    {
        if (eventData.FlagId == requiredFlag)
        {
            Refresh();
        }
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        Refresh();
    }

    protected virtual void Refresh()
    {
        if (FlagManager.Instance == null)
        {
            return;
        }

        if (target == null)
        {
            WarnInvalidTarget("[FlagBasedObject] Target is not assigned.");
            return;
        }

        SetTargetActive();
    }

    protected void SetTargetActive()
    {
        if (!TryGetTargetActiveState(out bool shouldBeActive))
        {
            return;
        }

        target.SetActive(shouldBeActive);
    }

    protected bool TryGetTargetActiveState(out bool shouldBeActive)
    {
        shouldBeActive = false;

        if (FlagManager.Instance == null)
        {
            return false;
        }

        if (target == null)
        {
            WarnInvalidTarget("[FlagBasedObject] Target is not assigned.");
            return false;
        }

        bool hasFlag = FlagManager.Instance.HasFlag(requiredFlag);
        shouldBeActive = activeWhenFlagExists ? hasFlag : !hasFlag;

        if (target == gameObject && !shouldBeActive)
        {
            WarnInvalidTarget("[FlagBasedObject] Target should be a separate child/object so this listener can stay active.");
            return false;
        }

        return true;
    }

    protected void WarnInvalidTarget(string message)
    {
        if (_warnedInvalidTarget)
        {
            return;
        }

        _warnedInvalidTarget = true;
        Debug.LogWarning(message, this);
    }
}
