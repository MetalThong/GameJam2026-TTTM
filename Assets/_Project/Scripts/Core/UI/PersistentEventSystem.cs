using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PersistentEventSystem : MonoBehaviour
{
    [SerializeField] private bool destroySceneDuplicates = true;

    private EventSystem _eventSystem;
    private InputSystemUIInputModule _inputModule;

    private void Awake()
    {
        EnsureEventSystem();
        SceneManager.sceneLoaded += OnSceneLoaded;
        RemoveDuplicateEventSystems();
    }

    private void OnEnable()
    {
        RemoveDuplicateEventSystems();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RemoveDuplicateEventSystems();
    }

    private void EnsureEventSystem()
    {
        if (!TryGetComponent(out _eventSystem))
        {
            _eventSystem = gameObject.AddComponent<EventSystem>();
        }

        if (!TryGetComponent(out _inputModule))
        {
            _inputModule = gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    private void RemoveDuplicateEventSystems()
    {
        if (!destroySceneDuplicates || _eventSystem == null)
        {
            return;
        }

        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem eventSystem = eventSystems[i];
            if (eventSystem == null || eventSystem == _eventSystem)
            {
                continue;
            }

            Destroy(eventSystem.gameObject);
        }
    }
}
