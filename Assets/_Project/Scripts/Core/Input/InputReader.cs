using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InputReader : IInputReader, IDisposable
{
    private readonly InputActionAsset _actions;
    private readonly InputActionMap _playerMap;
    private readonly InputAction _moveAction;
    private readonly InputAction _lookAction;
    private readonly InputAction _attackAction;
    private readonly InputAction _interactAction;
    private readonly InputAction _crouchAction;
    private readonly InputAction _jumpAction;
    private readonly InputAction _previousAction;
    private readonly InputAction _nextAction;
    private readonly InputAction _sprintAction;

    public Vector2 Move => _moveAction.ReadValue<Vector2>();
    public Vector2 Look => _lookAction.ReadValue<Vector2>();

    public bool IsAttackHeld => _attackAction.IsPressed();
    public bool IsInteractHeld => _interactAction.IsPressed();
    public bool IsCrouchHeld => _crouchAction.IsPressed();
    public bool IsJumpHeld => _jumpAction.IsPressed();
    public bool IsSprintHeld => _sprintAction.IsPressed();

    public event Action AttackPressed;
    public event Action AttackReleased;
    public event Action InteractPressed;
    public event Action InteractReleased;
    public event Action CrouchPressed;
    public event Action CrouchReleased;
    public event Action JumpPressed;
    public event Action JumpReleased;
    public event Action PreviousPressed;
    public event Action NextPressed;
    public event Action SprintPressed;
    public event Action SprintReleased;

    public InputReader(InputActionAsset inputActions)
    {
        if (inputActions == null)
        {
            throw new ArgumentNullException(nameof(inputActions));
        }

        _actions = UnityEngine.Object.Instantiate(inputActions);
        _playerMap = FindActionMap("Player");
        _moveAction = FindAction("Move");
        _lookAction = FindAction("Look");
        _attackAction = FindAction("Attack");
        _interactAction = FindAction("Interact");
        _crouchAction = FindAction("Crouch");
        _jumpAction = FindAction("Jump");
        _previousAction = FindAction("Previous");
        _nextAction = FindAction("Next");
        _sprintAction = FindAction("Sprint");

        RegisterCallbacks();
    }

    public void Enable()
    {
        _playerMap.Enable();
    }

    public void Disable()
    {
        _playerMap.Disable();
    }

    public void Dispose()
    {
        UnregisterCallbacks();
        UnityEngine.Object.Destroy(_actions);
    }

    private InputActionMap FindActionMap(string mapName)
    {
        InputActionMap map = _actions.FindActionMap(mapName, throwIfNotFound: false) 
            ?? throw new InvalidOperationException($"Input action map '{mapName}' was not found.");

        return map;
    }

    private InputAction FindAction(string actionName)
    {
        InputAction action = _playerMap.FindAction(actionName, throwIfNotFound: false) 
            ?? throw new InvalidOperationException($"Input action '{actionName}' was not found in '{_playerMap.name}'.");

        return action;
    }

    private void RegisterCallbacks()
    {
        _attackAction.started += OnAttackStarted;
        _attackAction.canceled += OnAttackCanceled;
        _interactAction.started += OnInteractStarted;
        _interactAction.canceled += OnInteractCanceled;
        _crouchAction.started += OnCrouchStarted;
        _crouchAction.canceled += OnCrouchCanceled;
        _jumpAction.started += OnJumpStarted;
        _jumpAction.canceled += OnJumpCanceled;
        _previousAction.performed += OnPreviousPerformed;
        _nextAction.performed += OnNextPerformed;
        _sprintAction.started += OnSprintStarted;
        _sprintAction.canceled += OnSprintCanceled;
    }

    private void UnregisterCallbacks()
    {
        _attackAction.started -= OnAttackStarted;
        _attackAction.canceled -= OnAttackCanceled;
        _interactAction.started -= OnInteractStarted;
        _interactAction.canceled -= OnInteractCanceled;
        _crouchAction.started -= OnCrouchStarted;
        _crouchAction.canceled -= OnCrouchCanceled;
        _jumpAction.started -= OnJumpStarted;
        _jumpAction.canceled -= OnJumpCanceled;
        _previousAction.performed -= OnPreviousPerformed;
        _nextAction.performed -= OnNextPerformed;
        _sprintAction.started -= OnSprintStarted;
        _sprintAction.canceled -= OnSprintCanceled;
    }

    private void OnAttackStarted(InputAction.CallbackContext context) => AttackPressed?.Invoke();
    private void OnAttackCanceled(InputAction.CallbackContext context) => AttackReleased?.Invoke();
    private void OnInteractStarted(InputAction.CallbackContext context) => InteractPressed?.Invoke();
    private void OnInteractCanceled(InputAction.CallbackContext context) => InteractReleased?.Invoke();
    private void OnCrouchStarted(InputAction.CallbackContext context) => CrouchPressed?.Invoke();
    private void OnCrouchCanceled(InputAction.CallbackContext context) => CrouchReleased?.Invoke();
    private void OnJumpStarted(InputAction.CallbackContext context) => JumpPressed?.Invoke();
    private void OnJumpCanceled(InputAction.CallbackContext context) => JumpReleased?.Invoke();
    private void OnPreviousPerformed(InputAction.CallbackContext context) => PreviousPressed?.Invoke();
    private void OnNextPerformed(InputAction.CallbackContext context) => NextPressed?.Invoke();
    private void OnSprintStarted(InputAction.CallbackContext context) => SprintPressed?.Invoke();
    private void OnSprintCanceled(InputAction.CallbackContext context) => SprintReleased?.Invoke();
}
