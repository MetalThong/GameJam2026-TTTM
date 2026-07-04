using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerFormObject : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private MovementForm requiredForm = MovementForm.Ghost;
    [SerializeField] private bool activeWhenFormMatches = true;
    [SerializeField] private bool hideWhenPlayerMissing = true;

    private Movement _playerMovement;
    private bool _warnedInvalidTarget;

    private void OnEnable()
    {
        ResolvePlayerMovement();
        Refresh();
    }

    private void OnDisable()
    {
        SetPlayerMovement(null);
    }

    private void Update()
    {
        if (_playerMovement == null || !_playerMovement.isActiveAndEnabled)
        {
            ResolvePlayerMovement();
            Refresh();
        }
    }

    private void ResolvePlayerMovement()
    {
        if (_playerMovement != null && _playerMovement.isActiveAndEnabled)
        {
            return;
        }

        SetPlayerMovement(Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude));
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
        Refresh();
    }

    private void Refresh()
    {
        if (target == null)
        {
            WarnInvalidTarget("[PlayerFormObject] Target is not assigned.");
            return;
        }

        if (_playerMovement == null)
        {
            if (hideWhenPlayerMissing)
            {
                target.SetActive(false);
            }

            return;
        }

        bool formMatches = _playerMovement.CurrentForm == requiredForm;
        target.SetActive(activeWhenFormMatches ? formMatches : !formMatches);
    }

    private void WarnInvalidTarget(string message)
    {
        if (_warnedInvalidTarget)
        {
            return;
        }

        _warnedInvalidTarget = true;
        Debug.LogWarning(message, this);
    }
}
