using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class SpeakerDanceInteractable : MonoBehaviour, IInteractable, IInteractionPromptProvider, IInteractionAvailability
{
    [SerializeField] private Movement playerMovement;
    [SerializeField] private string promptLocalizationKey = "prompt.dance";
    [SerializeField] private string danceStateName = "DancingText";
    [SerializeField, Min(0.05f)] private float fallbackDanceDuration = 1f;
    [SerializeField] private bool requireCatForm;
    [SerializeField] private bool lockGameStateDuringDance = true;

    private bool _isDancing;
    private bool _isPlayerInside;
    private GameState _previousGameState = GameState.Playing;
    private bool _hasLockedGameState;

    public string PromptLocalizationKey => promptLocalizationKey;

    public bool IsInteractionAvailable(Movement movement)
    {
        Movement resolvedMovement = ResolveMovement(movement);
        return !_isDancing
            && resolvedMovement != null
            && (!requireCatForm || resolvedMovement.CurrentForm == MovementForm.Cat);
    }

    public bool TryInteract()
    {
        Movement resolvedMovement = ResolveMovement(null);
        if (!IsInteractionAvailable(resolvedMovement))
        {
            return false;
        }

        PlayDanceAsync(resolvedMovement, this.GetCancellationTokenOnDestroy()).Forget();
        return true;
    }

    private void Update()
    {
        if (!_isPlayerInside
            || IsInteractionBlockedByGameState()
            || Keyboard.current == null
            || !Keyboard.current.eKey.wasPressedThisFrame)
        {
            return;
        }

        TryInteract();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Movement movement = other != null ? other.GetComponentInParent<Movement>() : null;
        if (movement == null)
        {
            return;
        }

        _isPlayerInside = true;
        playerMovement = movement;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Movement movement = other != null ? other.GetComponentInParent<Movement>() : null;
        if (movement == null || (playerMovement != null && movement != playerMovement))
        {
            return;
        }

        _isPlayerInside = false;
    }

    private async UniTaskVoid PlayDanceAsync(Movement movement, CancellationToken cancellationToken)
    {
        _isDancing = true;
        LockGameState();

        try
        {
            if (!movement.TryPlayAnimationState(danceStateName, requireCatForm, out float duration))
            {
                return;
            }

            float waitDuration = duration > 0f ? duration : fallbackDanceDuration;
            await UniTask.Delay(TimeSpan.FromSeconds(waitDuration), cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (movement != null)
            {
                movement.SnapAnimatorToCurrentForm();
            }

            RestoreGameStateIfNeeded();
            _isDancing = false;
        }
    }

    private Movement ResolveMovement(Movement candidate)
    {
        if (candidate != null)
        {
            playerMovement = candidate;
            return playerMovement;
        }

        if (playerMovement == null)
        {
            playerMovement = UnityEngine.Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
        }

        return playerMovement;
    }

    private static bool IsInteractionBlockedByGameState()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OnDialog;
    }

    private void LockGameState()
    {
        if (!lockGameStateDuringDance || _hasLockedGameState || GameManager.Instance == null)
        {
            return;
        }

        _previousGameState = GameManager.Instance.CurrentState;
        _hasLockedGameState = true;

        if (_previousGameState != GameState.OnDialog)
        {
            GameManager.Instance.SetState(GameState.OnDialog);
        }
    }

    private void RestoreGameStateIfNeeded()
    {
        if (!_hasLockedGameState)
        {
            return;
        }

        _hasLockedGameState = false;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OnDialog)
        {
            GameManager.Instance.SetState(_previousGameState == GameState.OnDialog
                ? GameState.Playing
                : _previousGameState);
        }
    }
}
