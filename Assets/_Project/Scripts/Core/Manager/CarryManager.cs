using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class CarryManager : MonoBehaviour
{
    public static CarryManager Instance { get; private set; }

    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string carryAnchorName = "CarryAnchor";
    [SerializeField] private Vector3 carryLocalPosition = new(0f, 0.45f, 0f);
    [SerializeField] private Vector3 carryLocalEulerAngles;
    [SerializeField] private Key dropKey = Key.Q;
    [SerializeField] private Vector3 dropLocalPosition = new(0f, -0.25f, 0f);
    [SerializeField] private Vector3 dropLocalEulerAngles;
    [SerializeField] private bool disableCarriedColliders = true;
    [SerializeField] private bool disableCarriedRigidbodies = true;
    [SerializeField] private bool disableCarriedInteractables = true;

    private string _carryId;
    private GameObject _carryPrefab;
    private GameObject _runtimeCarryTemplate;
    private GameObject _visual;

    public bool IsCarrying => _carryPrefab != null;
    public string CurrentCarryId => _carryId;

    public static CarryManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject carryManagerObject = new GameObject("CarryManager");
        return carryManagerObject.AddComponent<CarryManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        AttachToCurrentPlayerNextFrameAsync().Forget();
    }

    private void Update()
    {
        if (!IsCarrying || dropKey == Key.None)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[dropKey].wasPressedThisFrame)
        {
            Drop();
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    public void Grab(ICarryable carryable)
    {
        if (carryable == null)
        {
            return;
        }

        GameObject carryPrefab = ResolveCarryPrefab(carryable.CarryPrefab);
        if (carryPrefab == null)
        {
            return;
        }

        _carryId = carryable.CarryId;
        _carryPrefab = carryPrefab;
        AttachToCurrentPlayer();
    }

    public void Drop()
    {
        GameObject prefabToDrop = _carryPrefab;
        if (prefabToDrop == null)
        {
            ClearCarriedState();
            return;
        }

        ResolveDropPose(out Vector3 dropPosition, out Quaternion dropRotation);
        GameObject droppedObject = Instantiate(prefabToDrop, dropPosition, dropRotation);
        droppedObject.name = GetDroppedObjectName(prefabToDrop);
        droppedObject.SetActive(true);

        ClearCarriedState();
    }

    private void ClearCarriedState()
    {
        _carryId = null;
        _carryPrefab = null;

        if (_visual != null)
        {
            Destroy(_visual);
            _visual = null;
        }

        if (_runtimeCarryTemplate != null)
        {
            Destroy(_runtimeCarryTemplate);
            _runtimeCarryTemplate = null;
        }
    }

    public bool IsCarryingId(string carryId)
    {
        return IsCarrying
            && !string.IsNullOrWhiteSpace(carryId)
            && string.Equals(_carryId, carryId, StringComparison.Ordinal);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToCurrentPlayerNextFrameAsync().Forget();
    }

    private GameObject ResolveCarryPrefab(GameObject carryPrefab)
    {
        if (carryPrefab == null)
        {
            return null;
        }

        if (!carryPrefab.scene.IsValid())
        {
            DestroyRuntimeCarryTemplate();
            return carryPrefab;
        }

        DestroyRuntimeCarryTemplate();
        _runtimeCarryTemplate = Instantiate(carryPrefab, transform);
        _runtimeCarryTemplate.name = $"{carryPrefab.name}_CarryTemplate";
        _runtimeCarryTemplate.SetActive(false);
        return _runtimeCarryTemplate;
    }

    private void DestroyRuntimeCarryTemplate()
    {
        if (_runtimeCarryTemplate == null)
        {
            return;
        }

        Destroy(_runtimeCarryTemplate);
        _runtimeCarryTemplate = null;
    }

    private static string GetDroppedObjectName(GameObject prefabToDrop)
    {
        const string TemplateSuffix = "_CarryTemplate";
        if (prefabToDrop == null)
        {
            return "DroppedCarryObject";
        }

        string objectName = prefabToDrop.name;
        return objectName.EndsWith(TemplateSuffix, StringComparison.Ordinal)
            ? objectName[..^TemplateSuffix.Length]
            : objectName;
    }

    private async UniTaskVoid AttachToCurrentPlayerNextFrameAsync()
    {
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        AttachToCurrentPlayer();
    }

    private void AttachToCurrentPlayer()
    {
        if (_carryPrefab == null)
        {
            return;
        }

        Transform anchor = FindCarryAnchor();
        if (anchor == null)
        {
            return;
        }

        if (_visual != null)
        {
            Destroy(_visual);
        }

        _visual = Instantiate(_carryPrefab, anchor);
        _visual.name = _carryPrefab.name;
        _visual.transform.localPosition = carryLocalPosition;
        _visual.transform.localRotation = Quaternion.Euler(carryLocalEulerAngles);
        _visual.SetActive(true);

        PrepareCarriedVisual(_visual);
    }

    private Transform FindCarryAnchor()
    {
        Transform player = FindPlayerTransform();
        if (player == null)
        {
            return null;
        }

        Transform anchor = FindChildRecursive(player, carryAnchorName);
        return anchor != null ? anchor : player;
    }

    private void ResolveDropPose(out Vector3 position, out Quaternion rotation)
    {
        Transform player = FindPlayerTransform();
        if (player != null)
        {
            position = player.TransformPoint(dropLocalPosition);
            rotation = player.rotation * Quaternion.Euler(dropLocalEulerAngles);
            return;
        }

        if (_visual != null)
        {
            position = _visual.transform.position;
            rotation = _visual.transform.rotation;
            return;
        }

        position = transform.position;
        rotation = Quaternion.Euler(dropLocalEulerAngles);
    }

    private Transform FindPlayerTransform()
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return null;
        }

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            return null;
        }

        return player.transform;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void PrepareCarriedVisual(GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        if (disableCarriedColliders)
        {
            foreach (Collider2D collider in visual.GetComponentsInChildren<Collider2D>(true))
            {
                collider.enabled = false;
            }
        }

        if (disableCarriedRigidbodies)
        {
            foreach (Rigidbody2D rigidbody in visual.GetComponentsInChildren<Rigidbody2D>(true))
            {
                rigidbody.simulated = false;
            }
        }

        if (!disableCarriedInteractables)
        {
            return;
        }

        foreach (MonoBehaviour behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is IInteractable)
            {
                behaviour.enabled = false;
            }
        }
    }
}
