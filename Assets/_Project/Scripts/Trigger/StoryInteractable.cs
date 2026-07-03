using UnityEngine;

public class StoryInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactId;
    [SerializeField] private bool interactOnce = true;
    [SerializeField] private StoryFlagCondition condition = new();
    [SerializeField] private StoryFlagAction action = new();

    public bool TryInteract()
    {
        FlagManager flagManager = FlagManager.Instance;
        if (flagManager == null)
        {
            return false;
        }

        if (interactOnce && flagManager.HasFlag($"interact_completed_{interactId}"))
        {    
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

        if (interactOnce)
        {
            flagManager.SetFlag($"interact_completed_{interactId}", true);
        }
    }
}
