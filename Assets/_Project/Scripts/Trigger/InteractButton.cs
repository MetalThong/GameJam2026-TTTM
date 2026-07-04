using UnityEngine;
using UnityEngine.Serialization;

public class InteractButton : MonoBehaviour, IInteractionAvailability
{
    [SerializeField] private GameObject button;

    [Header("Form Requirement")]
    [FormerlySerializedAs("requiredCatForm")]
    [SerializeField] private bool restrictByForm;
    [SerializeField] private MovementForm requiredForm = MovementForm.Cat;

    private bool _isPlayerInside;
    private Movement _playerMovement;

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

        SetPlayerMovement(null);
        SetPromptVisible(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInside = true;
            SetPlayerMovement(other.GetComponentInParent<Movement>());
            RefreshPrompt();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInside = false;
            SetPlayerMovement(null);
            RefreshPrompt();
        }
    }

    private void OnGameStateChanged(GameState previousState, GameState currentState)
    {
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        SetPromptVisible(_isPlayerInside && !IsPromptBlocked() && IsInteractionAvailable(_playerMovement));
    }

    public bool IsInteractionAvailable(Movement playerMovement)
    {
        return !restrictByForm || (playerMovement != null && playerMovement.CurrentForm == requiredForm);
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

    private void SetPlayerMovement(Movement playerMovement)
    {
        if (_playerMovement == playerMovement)
        {
            return;
        }

        if (_playerMovement != null)
        {
            _playerMovement.FormChanged -= OnPlayerFormChanged;
        }

        _playerMovement = playerMovement;

        if (_playerMovement != null)
        {
            _playerMovement.FormChanged += OnPlayerFormChanged;
        }
    }

    private void OnPlayerFormChanged(MovementForm previousForm, MovementForm currentForm)
    {
        RefreshPrompt();
    }
}
