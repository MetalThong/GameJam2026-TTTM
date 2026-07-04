using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class SceneLoadInteractable : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    [SerializeField] private SceneId targetScene;
    [SerializeField] private string promptLocalizationKey = "prompt.pass";

    private readonly SceneLoader _sceneLoader = new();
    private bool _isLoading = false;

    public string PromptLocalizationKey => promptLocalizationKey;

    public virtual bool TryInteract()
    {
        if (_isLoading)
        {
            return false;
        }

        LoadAsync().Forget();
        return true;
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
}
