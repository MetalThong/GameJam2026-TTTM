using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CatInteractor : MonoBehaviour
{
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private string interactSfxId = AudioFeedback.ButtonClickId;

    private readonly List<IInteractable> _interactables = new();
    private bool _dialogueWasPlaying;
    private int _ignoreInteractionFrame = -1;

    private void Update()
    {
        bool dialoguePlaying = IsDialoguePlaying();
        if (dialoguePlaying)
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
        if (other.TryGetComponent(out IInteractable interactable))
        {
            _interactables.Remove(interactable);
        }
    }

    private void Register(Collider2D other)
    {
        if (!other.TryGetComponent(out IInteractable interactable) || _interactables.Contains(interactable))
        {
            return;
        }

        _interactables.Add(interactable);
    }

    private void PruneInteractables()
    {
        for (int i = _interactables.Count - 1; i >= 0; i--)
        {
            if (!IsAvailable(_interactables[i]))
            {
                _interactables.RemoveAt(i);
            }
        }
    }

    private bool IsDialoguePlaying()
    {
        if (dialogueManager == null)
        {
            dialogueManager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }

        return dialogueManager != null && dialogueManager.IsPlaying;
    }

    private static bool IsAvailable(IInteractable interactable)
    {
        if (interactable == null)
        {
            return false;
        }

        if (interactable is Behaviour behaviour)
        {
            return behaviour.isActiveAndEnabled && behaviour.gameObject.activeInHierarchy;
        }

        if (interactable is Component component)
        {
            return component.gameObject.activeInHierarchy;
        }

        return true;
    }
}
