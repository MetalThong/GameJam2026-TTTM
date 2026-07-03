using Cysharp.Threading.Tasks;
using UnityEngine;

public class SceneLoadInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private SceneId targetScene;

    private readonly SceneLoader _sceneLoader = new();
    private bool _isLoading = false;

    public void TryInteract()
    {
        if (_isLoading)
        {
            return;
        }

        LoadAsync().Forget();
    }

    private async UniTaskVoid LoadAsync()
    {
        _isLoading = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameState.Playing);
        }

        await _sceneLoader.LoadSceneAsync(targetScene);

        _isLoading = false;
    }
}
