using UnityEngine;

public class InteractButton : MonoBehaviour
{
    [SerializeField] private GameObject button;

    private void Awake()
    {
        // Hide the prompt at startup so it only appears when the player enters the trigger,
        // even if the button GameObject was left active in the scene.
        PopUpButton(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PopUpButton(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PopUpButton(false);
        }
    }

    private void PopUpButton(bool isPopUp)
    {
        if (button == null)
        {
            return;
        }

        button.SetActive(isPopUp);
    }
}
