using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InputManager : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private bool enableOnAwake = true;

    private InputReader _reader;

    public static InputManager Instance { get; private set; }
    public IInputReader Reader => _reader;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _reader = new InputReader(inputActions);

        if (enableOnAwake)
        {
            _reader.Enable();
        }
    }

    private void OnEnable()
    {
        if (enableOnAwake)
        {
            _reader?.Enable();
        }
    }

    private void OnDisable()
    {
        _reader?.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        _reader?.Dispose();
        _reader = null;
    }

    public void EnableInput()
    {
        _reader.Enable();
    }

    public void DisableInput()
    {
        _reader.Disable();
    }
}
