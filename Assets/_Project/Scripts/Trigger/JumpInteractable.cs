using UnityEngine;

public sealed class JumpInteractable : MonoBehaviour, IInteractable
{
    [Header("Jump")]
    [SerializeField] private Vector2 jumpVelocity = new(0f, 8f);
    [SerializeField, Min(0f)] private float delayDuration;
    [SerializeField, Min(0f)] private float minGroundCheckDelay = 0.1f;
    [SerializeField] private bool useFacingDirection = true;
    [SerializeField] private bool allowWhileStoryLocked;
    [SerializeField] private bool allowGhostForm;

    private Movement _currentMovement;

    public bool TryInteract()
    {
        if (_currentMovement == null)
        {
            return false;
        }

        return _currentMovement.TryInteractJump(
            jumpVelocity,
            minGroundCheckDelay,
            delayDuration,
            useFacingDirection,
            allowWhileStoryLocked,
            allowGhostForm
        );
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Register(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        Register(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Movement movement = other.GetComponentInParent<Movement>();
        if (movement != null && movement == _currentMovement)
        {
            _currentMovement = null;
        }
    }

    private void Register(Collider2D other)
    {
        Movement movement = other.GetComponentInParent<Movement>();
        if (movement != null)
        {
            _currentMovement = movement;
        }
    }
}
