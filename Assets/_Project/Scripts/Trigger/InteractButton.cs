using UnityEngine;

public class InteractButton : MonoBehaviour
{
    [SerializeField] private GameObject button;

    private bool _isPlayerInside;

    private void Awake()
    {
        // Hide the prompt at startup so it only appears when the player enters the trigger,
        // even if the button GameObject was left active in the scene.
        SetPromptVisible(false);
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged += OnGameStateChanged;
        }

        RefreshPrompt();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged -= OnGameStateChanged;
        }

        SetPromptVisible(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInside = true;
            RefreshPrompt();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInside = false;
            RefreshPrompt();
        }
    }

    private void OnGameStateChanged(GameState previousState, GameState currentState)
    {
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        SetPromptVisible(_isPlayerInside && !IsPromptBlocked());
    }

    private static bool IsPromptBlocked()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OnDialog;
    }

    private void SetPromptVisible(bool isVisible)
    {
        if (button == null)
        {
            return;
        }

        button.SetActive(isVisible);
    }
}
