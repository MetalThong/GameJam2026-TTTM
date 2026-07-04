public interface IInteractable
{
    bool TryInteract();
}

public interface IInteractionAvailability
{
    bool IsInteractionAvailable(Movement playerMovement);
}
