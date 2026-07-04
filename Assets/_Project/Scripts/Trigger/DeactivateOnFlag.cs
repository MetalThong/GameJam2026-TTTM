using UnityEngine;

[DisallowMultipleComponent]
public sealed class DeactivateOnFlag : MonoBehaviour
{
    [SerializeField] private string flagId;
    [SerializeField] private GameObject target;

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
        if (eventData.Value && eventData.FlagId == flagId)
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
        if (string.IsNullOrWhiteSpace(flagId)
            || FlagManager.Instance == null
            || !FlagManager.Instance.HasFlag(flagId))
        {
            return;
        }

        GameObject targetObject = target != null ? target : gameObject;
        targetObject.SetActive(false);
    }
}
