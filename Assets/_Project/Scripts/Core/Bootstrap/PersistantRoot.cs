using UnityEngine;

public sealed class PersistantRoot : MonoBehaviour
{
    public static PersistantRoot Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsurePersistentEventSystem();
    }

    private void EnsurePersistentEventSystem()
    {
        if (!TryGetComponent(out PersistentEventSystem _))
        {
            gameObject.AddComponent<PersistentEventSystem>();
        }
    }
}
