using UnityEngine;

public class StoryTrigger : MonoBehaviour
{
    [SerializeField] private string triggerId;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private StoryFlagCondition condition = new();
    [SerializeField] private StoryFlagAction action = new();

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

        if (triggerOnce && flagManager.HasFlag($"trigger_completed_{triggerId}"))
        {
            // Debug.Log("[StoryTrigger] This trigger has triggered");
            return;
        }

        if (condition != null && !condition.IsMet(flagManager.Flags))
        {
            return;
        }

        ExecuteTrigger();
    }

    protected virtual void ExecuteTrigger()
    {
        FlagManager flagManager = FlagManager.Instance;
        if (flagManager == null)
        {
            return;
        }

        action?.Execute(flagManager);

        if (triggerOnce)
        {
            flagManager.SetFlag($"trigger_completed_{triggerId}", true);
        }
    }
}
