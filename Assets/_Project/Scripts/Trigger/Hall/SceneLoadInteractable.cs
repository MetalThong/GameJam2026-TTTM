using Cysharp.Threading.Tasks;
using UnityEngine;

public class SceneLoadInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private SceneId targetScene;

    private readonly SceneLoader _sceneLoader = new();
    private bool _isLoading = false;

    public bool TryInteract()
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
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.Playing);
            }

            await _sceneLoader.FadeLoadAsync(targetScene);
        }
        finally
        {
            _isLoading = false;
        }
    }
}
