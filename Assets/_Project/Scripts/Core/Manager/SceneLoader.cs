using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneLoader
{
    public bool IsLoading { get; private set; }

    public async UniTask LoadSceneAsync(SceneId sceneId)
    {
        await LoadSceneAsync(sceneId.ToString());
    }

    public async UniTask LoadSceneAsync(string sceneName)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;

        try
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (operation == null)
            {
                Debug.LogError($"SceneLoader: failed to load scene '{sceneName}'. Check Build Settings.");
                return;
            }

            while (!operation.isDone)
            {
                await UniTask.Yield();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async UniTask ReloadCurrentSceneAsync()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        await LoadSceneAsync(currentSceneName);
    }
}
