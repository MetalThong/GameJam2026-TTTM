using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(MovementInput))]
[RequireComponent(typeof(CatMovementForm))]
[RequireComponent(typeof(GhostMovementForm))]
public sealed class Movement : MonoBehaviour
{
    [Header("Form")]
    [SerializeField] private MovementForm startingForm = MovementForm.Cat;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Physics")]
    [SerializeField] private Rigidbody2D targetRigidbody;

    private MovementInput _input;
    private MovementFormBehaviour[] _forms;
    private MovementFormBehaviour _currentForm;
    private float _defaultGravityScale;

    private void Awake()
    {
        ResolveReferences();
        if (!enabled)
        {
            return;
        }

        _input.Initialize(inputActions);
        _defaultGravityScale = targetRigidbody.gravityScale;

        SetForm(startingForm);
    }

    private void Update()
    {
        _input.Refresh();

        if (_input.WasToggleFormPressed)
        {
            ToggleForm();
        }
    }

    private void FixedUpdate()
    {
        if (_currentForm == null)
        {
            return;
        }

        _currentForm.Move(targetRigidbody, _input.Move);
    }

    private void ToggleForm()
    {
        MovementForm nextForm = _currentForm != null && _currentForm.Form == MovementForm.Cat
            ? MovementForm.Ghost
            : MovementForm.Cat;

        SetForm(nextForm);
    }

    private void SetForm(MovementForm form)
    {
        MovementFormBehaviour nextForm = FindForm(form);
        if (nextForm == null)
        {
            Debug.LogWarning($"Movement: form '{form}' was not found.", this);
            return;
        }

        if (_currentForm == nextForm)
        {
            return;
        }

        _currentForm?.Exit(targetRigidbody, _defaultGravityScale);
        _currentForm = nextForm;
        _currentForm.Enter(targetRigidbody, _defaultGravityScale);
    }

    private MovementFormBehaviour FindForm(MovementForm form)
    {
        for (int i = 0; i < _forms.Length; i++)
        {
            if (_forms[i].Form == form)
            {
                return _forms[i];
            }
        }

        return null;
    }

    private void ResolveReferences()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody2D>();
        }

        if (targetRigidbody == null)
        {
            Debug.LogError("Movement: missing Rigidbody2D reference.", this);
            enabled = false;
            return;
        }

        _input = GetComponent<MovementInput>();
        if (_input == null)
        {
            Debug.LogError("Movement: missing MovementInput component.", this);
            enabled = false;
            return;
        }

        _forms = GetComponents<MovementFormBehaviour>();
    }
}
