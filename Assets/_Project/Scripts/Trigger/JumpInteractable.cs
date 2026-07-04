using System.Collections.Generic;
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

    private readonly HashSet<Collider2D> _knownColliders = new();
    private readonly HashSet<Collider2D> _movementColliders = new();
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
        _knownColliders.Remove(other);

        if (_movementColliders.Remove(other) && _movementColliders.Count <= 0)
        {
            _currentMovement = null;
        }
    }

    private void Register(Collider2D other)
    {
        if (other == null || !_knownColliders.Add(other))
        {
            return;
        }

        Movement movement = other.GetComponentInParent<Movement>();
        if (movement != null)
        {
            _movementColliders.Add(other);
            _currentMovement = movement;
        }
    }
}
