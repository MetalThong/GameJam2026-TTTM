using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class SceneLoadInteractable : MonoBehaviour, IInteractable, IInteractionPromptProvider, IInteractionAvailability
{
    [SerializeField] private SceneId targetScene;
    [SerializeField] private string promptLocalizationKey = "prompt.pass";

    [Header("Form Requirement")]
    [SerializeField] private bool restrictByForm;
    [SerializeField] private MovementForm requiredForm = MovementForm.Cat;
    [SerializeField] private Movement playerMovement;

    private readonly SceneLoader _sceneLoader = new();
    private bool _isLoading = false;

    public string PromptLocalizationKey => promptLocalizationKey;

    public virtual bool TryInteract()
    {
        if (_isLoading || !IsInteractionAvailable(null))
        {
            return false;
        }

        if (TryGetComponent(out InteractButton interactButton))
        {
            interactButton.HidePromptImmediately();
        }

        LoadAsync().Forget();
        return true;
    }

    public bool IsInteractionAvailable(Movement movement)
    {
        if (!restrictByForm)
        {
            return true;
        }

        Movement resolvedMovement = movement != null ? movement : ResolvePlayerMovement();
        return resolvedMovement != null && resolvedMovement.CurrentForm == requiredForm;
    }

    private async UniTaskVoid LoadAsync()
    {
        _isLoading = true;

        try
        {
            await BeforeLoadAsync();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.Playing);
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveGame();
            }

            await _sceneLoader.FadeLoadAsync(targetScene);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isLoading = false;
        }
    }

    protected virtual UniTask BeforeLoadAsync()
    {
        return UniTask.CompletedTask;
    }

    private Movement ResolvePlayerMovement()
    {
        if (playerMovement == null)
        {
            playerMovement = UnityEngine.Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
        }

        return playerMovement;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (playerMovement == null)
        {
            playerMovement = UnityEngine.Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
        }
    }
#endif
}
