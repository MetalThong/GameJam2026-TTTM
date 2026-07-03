using UnityEngine;

public class InteractButton : MonoBehaviour
{
    [SerializeField] private GameObject button;

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
        button.SetActive(isPopUp);
    }
}
