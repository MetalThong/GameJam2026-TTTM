using UnityEngine;

public class FlagBasedObject : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private string requiredFlag;
    [SerializeField] private bool activeWhenFlagExists = true;

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

    private void Refresh()
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

        bool hasFlag = FlagManager.Instance.HasFlag(requiredFlag);
        bool shouldBeActive = activeWhenFlagExists ? hasFlag : !hasFlag;

        if (target == gameObject && !shouldBeActive)
        {
            WarnInvalidTarget("[FlagBasedObject] Target should be a separate child/object so this listener can stay active.");
            return;
        }

        target.SetActive(shouldBeActive);
    }

    private void WarnInvalidTarget(string message)
    {
        if (_warnedInvalidTarget)
        {
            return;
        }

        _warnedInvalidTarget = true;
        Debug.LogWarning(message, this);
    }
}
