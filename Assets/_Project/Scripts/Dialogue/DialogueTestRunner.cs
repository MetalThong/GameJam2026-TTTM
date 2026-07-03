using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DialogueTestRunner : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private DialogueView dialogueView;

    [Header("Input")]
    [SerializeField] private Key triggerKey = Key.Enter;

    [Header("Test Content")]
    [SerializeField] private DialogueLine[] testLines;

    private DialogueView _view;
    private int _lineIndex = -1;

    public void SetReferences(DialogueView view, DialogueLine[] lines)
    {
        dialogueView = view;
        testLines = lines;
    }

    private void Awake()
    {
        _view = dialogueView;
        if (_view == null)
        {
            TryGetComponent(out _view);
        }
    }

    private void Start()
    {
        if (_view != null)
        {
            _view.Hide();
        }
    }

    private void Update()
    {
        if (WasTriggerPressed())
        {
            Advance();
        }
    }

    private void Advance()
    {
        if (_view == null || testLines == null || testLines.Length == 0)
        {
            Debug.LogWarning("DialogueTestRunner: missing DialogueView or test lines.", this);
            return;
        }

        if (!_view.IsVisible)
        {
            _lineIndex = 0;
            _view.Show(testLines[_lineIndex]);
            return;
        }

        _lineIndex++;
        if (_lineIndex >= testLines.Length)
        {
            _lineIndex = -1;
            _view.Hide();
            return;
        }

        _view.Show(testLines[_lineIndex]);
    }

    private bool WasTriggerPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        return keyboard[triggerKey].wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
    }
}
