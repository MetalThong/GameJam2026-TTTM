using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CatInteractor : MonoBehaviour
{
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private string interactSfxId = AudioFeedback.ButtonClickId;
    [SerializeField, Min(0.05f)] private float dialogueResolveRetryInterval = 0.5f;

    private readonly List<IInteractable> _interactables = new();
    private readonly HashSet<Collider2D> _knownColliders = new();
    private readonly Dictionary<Collider2D, IInteractable> _colliderInteractables = new();
    private readonly Dictionary<IInteractable, int> _interactableOverlapCounts = new();
    private bool _dialogueWasPlaying;
    private int _ignoreInteractionFrame = -1;
    private float _nextDialogueResolveTime;
    private Movement _movement;

    private void Awake()
    {
        _movement = GetComponent<Movement>();
    }

    private void Update()
    {
        bool interactionBlocked = IsDialoguePlaying() || IsInteractionBlockedByGameState();
        if (interactionBlocked)
        {
            _dialogueWasPlaying = true;
            return;
        }

        if (_dialogueWasPlaying)
        {
            _dialogueWasPlaying = false;
            _ignoreInteractionFrame = Time.frameCount;
        }

        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
        {
            return;
        }

        if (Time.frameCount == _ignoreInteractionFrame)
        {
            return;
        }

        PruneInteractables();

        for (int i = _interactables.Count - 1; i >= 0; i--)
        {
            IInteractable interactable = _interactables[i];
            if (!IsAvailable(interactable))
            {
                continue;
            }

            if (interactable.TryInteract())
            {
                AudioFeedback.PlaySfx(interactSfxId);
                break;
            }
        }
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

        if (!_colliderInteractables.TryGetValue(other, out IInteractable interactable))
        {
            return;
        }

        _colliderInteractables.Remove(other);

        int overlapCount = _interactableOverlapCounts.TryGetValue(interactable, out int count)
            ? count - 1
            : 0;

        if (overlapCount > 0)
        {
            _interactableOverlapCounts[interactable] = overlapCount;
            return;
        }

        _interactableOverlapCounts.Remove(interactable);
        _interactables.Remove(interactable);
    }

    private void Register(Collider2D other)
    {
        if (other == null || !_knownColliders.Add(other))
        {
            return;
        }

        if (!other.TryGetComponent(out IInteractable interactable))
        {
            return;
        }

        _colliderInteractables[other] = interactable;
        int overlapCount = _interactableOverlapCounts.TryGetValue(interactable, out int count) ? count : 0;
        _interactableOverlapCounts[interactable] = overlapCount + 1;

        if (overlapCount <= 0)
        {
            _interactables.Add(interactable);
        }
    }

    private void PruneInteractables()
    {
        for (int i = _interactables.Count - 1; i >= 0; i--)
        {
            if (_interactables[i] == null)
            {
                _interactables.RemoveAt(i);
            }
        }
    }

    private bool IsDialoguePlaying()
    {
        if (dialogueManager == null && Time.unscaledTime >= _nextDialogueResolveTime)
        {
            _nextDialogueResolveTime = Time.unscaledTime + dialogueResolveRetryInterval;
            dialogueManager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }

        return dialogueManager != null && dialogueManager.IsPlaying;
    }

    private static bool IsInteractionBlockedByGameState()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OnDialog;
    }

    private bool IsAvailable(IInteractable interactable)
    {
        if (interactable == null)
        {
            return false;
        }

        Component component = interactable as Component;

        if (interactable is Behaviour behaviour)
        {
            if (!behaviour.isActiveAndEnabled || !behaviour.gameObject.activeInHierarchy)
            {
                return false;
            }
        }
        else if (component != null)
        {
            if (!component.gameObject.activeInHierarchy)
            {
                return false;
            }
        }

        return component == null || PassesInteractionAvailability(component);
    }

    private bool PassesInteractionAvailability(Component interactableComponent)
    {
        MonoBehaviour[] behaviours = interactableComponent.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is IInteractionAvailability availability
                && !availability.IsInteractionAvailable(_movement))
            {
                return false;
            }
        }

        return true;
    }
}
