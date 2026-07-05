using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class CarryManager : MonoBehaviour
{
    public struct CarryDropInfo
    {
        public CarryDropInfo(string carryId, GameObject droppedObject, Vector3 dropPosition, Scene dropScene)
        {
            CarryId = carryId;
            DroppedObject = droppedObject;
            DropPosition = dropPosition;
            DropScene = dropScene;
        }

        public string CarryId { get; }
        public GameObject DroppedObject { get; }
        public Vector3 DropPosition { get; }
        public Scene DropScene { get; }
    }

    public static CarryManager Instance { get; private set; }
    public static event Action<CarryDropInfo> CarriedObjectDropped;

    [SerializeField] private string playerTag = "Player";
    [SerializeField] private SceneId[] paintingScenes = { SceneId.GhostKitchen, SceneId.Picture };
    [SerializeField] private string carryAnchorName = "CarryAnchor";
    [SerializeField] private Vector3 carryLocalPosition;
    [SerializeField] private Vector3 carryLocalEulerAngles;
    [SerializeField] private string carriedSortingLayerName = "Player";
    [SerializeField] private int carriedSortingOrder = 2;
    [SerializeField] private string carriedObjectLayerName = "Player";
    [SerializeField] private bool preserveCarriedWorldScale = true;
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
    private Vector3 _carryWorldScale = Vector3.one;
    private SceneId _currentSceneId;
    private bool _hasCurrentSceneId;

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
        CacheCurrentScene(SceneManager.GetActiveScene());
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

    public bool Grab(ICarryable carryable)
    {
        if (carryable == null)
        {
            return false;
        }

        if (!IsCurrentScenePainting())
        {
            return false;
        }

        GameObject requestedCarryPrefab = carryable.CarryPrefab;
        Vector3 carryWorldScale = ResolveCarryWorldScale(carryable, requestedCarryPrefab);
        GameObject carryPrefab = ResolveCarryPrefab(requestedCarryPrefab);
        if (carryPrefab == null)
        {
            return false;
        }

        _carryId = carryable.CarryId;
        _carryPrefab = carryPrefab;
        _carryWorldScale = carryWorldScale;
        AttachToCurrentPlayer();
        return true;
    }

    public void Drop()
    {
        DropAtOverride(null);
    }

    public void DropAt(Vector3 dropPosition)
    {
        DropAtOverride(dropPosition);
    }

    private void DropAtOverride(Vector3? dropPositionOverride)
    {
        string droppedCarryId = _carryId;
        GameObject prefabToDrop = _carryPrefab;
        if (prefabToDrop == null)
        {
            ClearCarriedState();
            return;
        }

        ResolveDropPose(out Vector3 dropPosition, out Quaternion dropRotation);
        if (dropPositionOverride.HasValue)
        {
            dropPosition = dropPositionOverride.Value;
        }

        GameObject droppedObject = Instantiate(prefabToDrop, dropPosition, dropRotation);
        droppedObject.name = GetDroppedObjectName(prefabToDrop);
        ApplyDroppedScale(droppedObject);
        droppedObject.SetActive(true);
        ApplyDroppedScale(droppedObject);

        CarriedObjectDropped?.Invoke(new CarryDropInfo(
            droppedCarryId,
            droppedObject,
            dropPosition,
            SceneManager.GetActiveScene()));

        ClearCarriedState();
    }

    private void ClearCarriedState()
    {
        _carryId = null;
        _carryPrefab = null;
        _carryWorldScale = Vector3.one;

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
        bool wasInPaintingScene = _hasCurrentSceneId && IsPaintingScene(_currentSceneId);
        CacheCurrentScene(scene);

        if (IsCarrying && wasInPaintingScene && !IsCurrentScenePainting())
        {
            DropAfterSceneLoadAsync().Forget();
            return;
        }

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

    private async UniTaskVoid DropAfterSceneLoadAsync()
    {
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        Drop();
    }

    private void AttachToCurrentPlayer()
    {
        if (_carryPrefab == null)
        {
            return;
        }

        if (!IsCurrentScenePainting())
        {
            DestroyVisual();
            return;
        }

        Transform anchor = FindCarryAnchor();
        if (anchor == null)
        {
            return;
        }

        DestroyVisual();

        _visual = Instantiate(_carryPrefab, anchor);
        _visual.name = _carryPrefab.name;
        _visual.transform.localPosition = carryLocalPosition;
        _visual.transform.localRotation = Quaternion.Euler(carryLocalEulerAngles);
        ApplyCarriedScale(_visual.transform);

        ApplyCarriedVisualPresentation(_visual);
        PrepareCarriedVisual(_visual);
        SetHierarchyActive(_visual, true);
        HideCarriedInteractionPrompts(_visual);
    }

    private void DestroyVisual()
    {
        if (_visual == null)
        {
            return;
        }

        Destroy(_visual);
        _visual = null;
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

        DisableCarriedInteractables(visual);
    }

    private void DisableCarriedInteractables(GameObject visual)
    {
        if (visual == null)
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

    private static void HideCarriedInteractionPrompts(GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        foreach (InteractButton prompt in visual.GetComponentsInChildren<InteractButton>(true))
        {
            if (prompt == null)
            {
                continue;
            }

            prompt.HidePromptImmediately();
            prompt.enabled = false;
        }
    }

    private void ApplyCarriedVisualPresentation(GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        int objectLayer = LayerMask.NameToLayer(carriedObjectLayerName);
        if (objectLayer >= 0)
        {
            SetLayerRecursively(visual.transform, objectLayer);
        }

        SpriteRenderer[] renderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(carriedSortingLayerName))
            {
                renderer.sortingLayerName = carriedSortingLayerName;
            }

            renderer.sortingOrder = carriedSortingOrder;
        }
    }

    private static void SetHierarchyActive(GameObject root, bool active)
    {
        if (root == null)
        {
            return;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null)
            {
                children[i].gameObject.SetActive(active);
            }
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null)
            {
                children[i].gameObject.layer = layer;
            }
        }
    }

    private Vector3 ResolveCarryWorldScale(ICarryable carryable, GameObject carryPrefab)
    {
        if (!preserveCarriedWorldScale)
        {
            return Vector3.one;
        }

        if (carryable != null && IsValidScale(carryable.CarryWorldScale))
        {
            return carryable.CarryWorldScale;
        }

        if (carryable is Component component && IsValidScale(component.transform.lossyScale))
        {
            return component.transform.lossyScale;
        }

        if (carryPrefab != null && IsValidScale(carryPrefab.transform.lossyScale))
        {
            return carryPrefab.transform.lossyScale;
        }

        return Vector3.one;
    }

    private void ApplyCarriedScale(Transform carriedTransform)
    {
        if (!preserveCarriedWorldScale || carriedTransform == null)
        {
            return;
        }

        carriedTransform.localScale = WorldScaleToLocalScale(carriedTransform.parent, _carryWorldScale);
    }

    private void ApplyDroppedScale(GameObject droppedObject)
    {
        if (!preserveCarriedWorldScale || droppedObject == null)
        {
            return;
        }

        droppedObject.transform.localScale = _carryWorldScale;
    }

    private static Vector3 WorldScaleToLocalScale(Transform parent, Vector3 worldScale)
    {
        if (parent == null)
        {
            return worldScale;
        }

        Vector3 parentScale = parent.lossyScale;
        return new Vector3(
            SafeDivide(worldScale.x, parentScale.x),
            SafeDivide(worldScale.y, parentScale.y),
            SafeDivide(worldScale.z, parentScale.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Approximately(divisor, 0f) ? value : value / divisor;
    }

    private static bool IsValidScale(Vector3 scale)
    {
        return !Mathf.Approximately(scale.x, 0f)
            && !Mathf.Approximately(scale.y, 0f)
            && !Mathf.Approximately(scale.z, 0f);
    }

    private void CacheCurrentScene(Scene scene)
    {
        if (scene.IsValid() && Enum.TryParse(scene.name, out SceneId sceneId))
        {
            _currentSceneId = sceneId;
            _hasCurrentSceneId = true;
            return;
        }

        _hasCurrentSceneId = false;
    }

    private bool IsCurrentScenePainting()
    {
        if (!_hasCurrentSceneId)
        {
            CacheCurrentScene(SceneManager.GetActiveScene());
        }

        return _hasCurrentSceneId && IsPaintingScene(_currentSceneId);
    }

    private bool IsPaintingScene(SceneId sceneId)
    {
        if (paintingScenes == null)
        {
            return false;
        }

        for (int i = 0; i < paintingScenes.Length; i++)
        {
            if (paintingScenes[i] == sceneId)
            {
                return true;
            }
        }

        return false;
    }
}
