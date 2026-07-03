using UnityEngine;
using UnityEngine.InputSystem;

public class CatInteractor : MonoBehaviour
{
    private IInteractable _current;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            _current?.TryInteract();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out IInteractable interactable))
        {
            _current = interactable;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out IInteractable interactable) && interactable == _current)
        {
            _current = null;
        }
    }
}
