using UnityEngine;
using UnityEngine.Serialization;

public class InteractButton : MonoBehaviour, IInteractionAvailability
{
    [SerializeField] private GameObject button;
    [SerializeField] private string fallbackPromptKey = "prompt.interact";

    [Header("Form Requirement")]
    [FormerlySerializedAs("requiredCatForm")]
    [SerializeField] private bool restrictByForm;
    [SerializeField] private MovementForm requiredForm = MovementForm.Cat;

    private bool _isPlayerInside;
    private IInteractionPromptProvider _promptProvider;
    private InteractionPromptView _promptView;
    private Movement _playerMovement;

    private void Awake()
    {
        _promptProvider = GetComponent<IInteractionPromptProvider>();
        SetLocalPromptVisible(false);
        HideGlobalPrompt();
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
        SetLocalPromptVisible(false);
        HideGlobalPrompt();
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
        bool shouldShowPrompt = _isPlayerInside
            && !IsPromptBlocked()
            && IsInteractionAvailable(_playerMovement);

        SetLocalPromptVisible(shouldShowPrompt);

        if (shouldShowPrompt)
        {
            ShowGlobalPrompt();
            return;
        }

        HideGlobalPrompt();
    }

    public bool IsInteractionAvailable(Movement playerMovement)
    {
        return !restrictByForm || (playerMovement != null && playerMovement.CurrentForm == requiredForm);
    }

    private static bool IsPromptBlocked()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OnDialog;
    }

    private void ShowGlobalPrompt()
    {
        InteractionPromptView promptView = ResolvePromptView();
        if (promptView == null)
        {
            return;
        }

        string promptKey = _promptProvider != null
            ? _promptProvider.PromptLocalizationKey
            : fallbackPromptKey;

        promptView.Show(this, promptKey);
    }

    private void HideGlobalPrompt()
    {
        if (_promptView != null)
        {
            _promptView.Hide(this);
        }
    }

    private InteractionPromptView ResolvePromptView()
    {
        if (_promptView != null)
        {
            return _promptView;
        }

        _promptView = Object.FindFirstObjectByType<InteractionPromptView>(FindObjectsInactive.Include);
        return _promptView;
    }

    private void SetLocalPromptVisible(bool isVisible)
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
