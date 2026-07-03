using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class MainMenuGameFlow : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "BedRoom";

    private readonly SceneLoader _sceneLoader = new();

    public void SetGameplaySceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        gameplaySceneName = sceneName;
    }

    public void StartNewGame()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.NewGame();
        }

        LoadGameplayAsync().Forget();
    }

    public void ContinueGame()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.LoadGame();
        }

        LoadGameplayAsync().Forget();
    }

    public void QuitGame()
    {
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #else
                Application.Quit();
        #endif
    }

    private async UniTaskVoid LoadGameplayAsync()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogWarning($"MainMenuGameFlow: scene '{gameplaySceneName}' is not in Build Settings yet.");
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameState.Playing);
        }

        await _sceneLoader.LoadSceneAsync(gameplaySceneName);
    }
}
