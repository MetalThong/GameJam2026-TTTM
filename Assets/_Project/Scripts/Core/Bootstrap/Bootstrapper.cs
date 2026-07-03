using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class Bootstrapper : MonoBehaviour
{
    [SerializeField] private GameObject persistentRootPrefab;
    [SerializeField] private SceneId startScene;

    private async void Start()
    {
        EnsurePersistantRoot();
        InitializeManagers();

        await LoadStartSceneAsync();
    }

    private void EnsurePersistantRoot()
    {
        if (PersistantRoot.Instance != null)
        {
            return;
        }

        Instantiate(persistentRootPrefab);
    }

    private void InitializeManagers()
    {
        GameManager.Instance.Initialize();
    }

    private async UniTask LoadStartSceneAsync()
    {
        SceneLoader sceneLoader = new();
        await sceneLoader.LoadSceneAsync(startScene);

        if (startScene == SceneId.Gameplay)
        {
            GameManager.Instance.SetState(GameState.Playing);
        }
        else
        {
            GameManager.Instance.SetState(GameState.MainMenu);
        }
    }
}
