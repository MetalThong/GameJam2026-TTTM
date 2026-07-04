using UnityEngine;

[DisallowMultipleComponent]
public sealed class WashingMinigameInteractable : MonoBehaviour, IInteractable, IInteractionPromptProvider, IInteractionAvailability
{
    [SerializeField] private WashingMinigameController washingMinigame;
    [SerializeField] private string requiredFlag = "go_to_dishes";
    [SerializeField] private string completedFlag = "dishes_washed";
    [SerializeField] private string promptLocalizationKey = "prompt.interact";
    [SerializeField] private bool hidePromptOnStart = true;

    public string PromptLocalizationKey => promptLocalizationKey;

    public bool TryInteract()
    {
        if (!IsInteractionAvailable(null))
        {
            return false;
        }

        WashingMinigameController controller = ResolveWashingMinigame();
        if (controller == null)
        {
            Debug.LogWarning("WashingMinigameInteractable: no WashingMinigameController found in this scene.", this);
            return false;
        }

        if (hidePromptOnStart && TryGetComponent(out InteractButton interactButton))
        {
            interactButton.HidePromptImmediately();
        }

        controller.StartFromInteraction();
        return true;
    }

    public bool IsInteractionAvailable(Movement playerMovement)
    {
        WashingMinigameController controller = ResolveWashingMinigame();
        return HasRequiredFlag()
            && !HasCompletedFlag()
            && controller != null
            && !controller.IsPlaying;
    }

    private WashingMinigameController ResolveWashingMinigame()
    {
        if (washingMinigame == null)
        {
            washingMinigame = Object.FindFirstObjectByType<WashingMinigameController>(FindObjectsInactive.Include);
        }

        return washingMinigame;
    }

    private bool HasRequiredFlag()
    {
        return string.IsNullOrWhiteSpace(requiredFlag)
            || (FlagManager.Instance != null && FlagManager.Instance.HasFlag(requiredFlag));
    }

    private bool HasCompletedFlag()
    {
        return !string.IsNullOrWhiteSpace(completedFlag)
            && FlagManager.Instance != null
            && FlagManager.Instance.HasFlag(completedFlag);
    }
}
