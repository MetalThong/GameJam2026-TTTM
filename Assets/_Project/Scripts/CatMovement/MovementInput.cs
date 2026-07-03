using UnityEngine;
using UnityEngine.InputSystem;

public sealed class MovementInput : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    private InputActionAsset _runtimeInputActions;
    private InputAction _moveAction;
    private InputAction _toggleFormAction;
    private bool _isInitialized;

    public Vector2 Move { get; private set; }
    public bool WasToggleFormPressed { get; private set; }

    public void Initialize(InputActionAsset fallbackInputActions)
    {
        if (_isInitialized)
        {
            return;
        }

        if (inputActions == null)
        {
            inputActions = fallbackInputActions;
        }

        InitializeLocalInputActions();
        _isInitialized = true;
    }

    public void Refresh()
    {
        Move = ReadMoveInput();
        WasToggleFormPressed = ReadToggleFormPressed();
    }

    private void OnDestroy()
    {
        if (_runtimeInputActions != null)
        {
            Destroy(_runtimeInputActions);
        }
    }

    private Vector2 ReadMoveInput()
    {
        if (InputManager.Instance != null && InputManager.Instance.Reader != null)
        {
            return InputManager.Instance.Reader.Move;
        }

        return _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
    }

    private bool ReadToggleFormPressed()
    {
        bool globalInput = InputManager.Instance != null
            && InputManager.Instance.Reader != null
            && InputManager.Instance.Reader.WasNextPressedThisFrame;

        bool localInput = _toggleFormAction != null && _toggleFormAction.WasPressedThisFrame();
        bool keyboardFallback = Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;

        return globalInput || localInput || keyboardFallback;
    }

    private void InitializeLocalInputActions()
    {
        if (inputActions == null)
        {
            return;
        }

        _runtimeInputActions = Instantiate(inputActions);
        InputActionMap playerMap = _runtimeInputActions.FindActionMap("Player", throwIfNotFound: false);

        if (playerMap == null)
        {
            Debug.LogWarning("MovementInput: input action map 'Player' was not found.", this);
            return;
        }

        _moveAction = playerMap.FindAction("Move", throwIfNotFound: false);
        if (_moveAction == null)
        {
            Debug.LogWarning("MovementInput: input action 'Move' was not found in 'Player'.", this);
            return;
        }

        _toggleFormAction = playerMap.FindAction("Next", throwIfNotFound: false);
        if (_toggleFormAction == null)
        {
            Debug.LogWarning("MovementInput: input action 'Next' was not found in 'Player'. Falling back to T key for form switching.", this);
        }

        playerMap.Enable();
    }
}
