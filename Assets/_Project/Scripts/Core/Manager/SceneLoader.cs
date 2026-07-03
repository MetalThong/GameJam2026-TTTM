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
            await LoadSceneInternalAsync(sceneName);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async UniTask FadeLoadAsync(SceneId sceneId)
    {
        await FadeLoadAsync(sceneId.ToString());
    }

    public async UniTask FadeLoadAsync(string sceneName)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        FadePanel fadePanel = ResolveFadePanel();

        try
        {
            if (fadePanel != null)
            {
                await fadePanel.FadeInAsync();
            }

            await LoadSceneInternalAsync(sceneName);
            await UniTask.Yield();

            fadePanel = ResolveFadePanel();
            if (fadePanel != null)
            {
                await fadePanel.FadeOutAsync();
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

    private static async UniTask LoadSceneInternalAsync(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("SceneLoader: failed to load because scene name is empty.");
            return;
        }

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

    private static FadePanel ResolveFadePanel()
    {
        FadePanel fadePanel = Object.FindFirstObjectByType<FadePanel>(FindObjectsInactive.Include);
        if (fadePanel == null)
        {
            Debug.LogWarning("SceneLoader: no FadePanel found. Scene will load without a fade transition.");
        }

        return fadePanel;
    }
}
