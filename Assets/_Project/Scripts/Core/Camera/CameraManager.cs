using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CinemachineImpulseSource))]
public sealed class CameraManager : MonoBehaviour
{
    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera gameplayCamera;
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Header("Bounds")]
    [SerializeField] private Collider2D defaultCameraBounds;
    [SerializeField] private bool addConfinerIfMissing = true;

    private CinemachineConfiner2D _confiner;

    public static CameraManager Instance { get; private set; }
    public CinemachineCamera GameplayCamera => gameplayCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveSceneReferences();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetCamera(CinemachineCamera camera)
    {
        gameplayCamera = camera;
        ResolveConfiner();
        ApplyBounds(defaultCameraBounds);
    }

    public void SetTarget(Transform target, bool useSameTargetForLookAt = true)
    {
        if (!EnsureCamera())
        {
            return;
        }

        gameplayCamera.Follow = target;

        if (useSameTargetForLookAt)
        {
            gameplayCamera.LookAt = target;
        }
    }

    public void ClearTarget()
    {
        if (!EnsureCamera())
        {
            return;
        }

        gameplayCamera.Follow = null;
        gameplayCamera.LookAt = null;
    }

    public void SetZoom(float orthographicSize)
    {
        if (!EnsureCamera())
        {
            return;
        }

        LensSettings lens = gameplayCamera.Lens;
        lens.OrthographicSize = orthographicSize;
        gameplayCamera.Lens = lens;
        _confiner?.InvalidateLensCache();
    }

    public void SetPriority(int priority)
    {
        if (!EnsureCamera())
        {
            return;
        }

        gameplayCamera.Priority = priority;
    }

    public void Prioritize()
    {
        if (!EnsureCamera())
        {
            return;
        }

        gameplayCamera.Prioritize();
    }

    public void SetBounds(Collider2D cameraBounds)
    {
        defaultCameraBounds = cameraBounds;
        ApplyBounds(defaultCameraBounds);
    }

    public void ClearBounds()
    {
        defaultCameraBounds = null;

        if (_confiner == null)
        {
            return;
        }

        _confiner.BoundingShape2D = null;
        _confiner.InvalidateBoundingShapeCache();
    }

    public void Shake()
    {
        if (!EnsureImpulseSource())
        {
            return;
        }

        impulseSource.GenerateImpulse();
    }

    public void Shake(float force)
    {
        if (!EnsureImpulseSource())
        {
            return;
        }

        impulseSource.GenerateImpulseWithForce(force);
    }

    public void Shake(Vector3 velocity)
    {
        if (!EnsureImpulseSource())
        {
            return;
        }

        impulseSource.GenerateImpulseWithVelocity(velocity);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveSceneReferences();
    }

    private bool EnsureCamera()
    {
        if (gameplayCamera != null)
        {
            return true;
        }

        ResolveSceneReferences();
        return gameplayCamera != null;
    }

    private bool EnsureImpulseSource()
    {
        if (impulseSource != null)
        {
            return true;
        }

        impulseSource = GetComponent<CinemachineImpulseSource>();
        return impulseSource != null;
    }

    private void ResolveSceneReferences()
    {
        if (gameplayCamera == null)
        {
            gameplayCamera = FindFirstObjectByType<CinemachineCamera>();
        }

        ResolveConfiner();
        ApplyBounds(defaultCameraBounds);

        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
        }
    }

    private void ResolveConfiner()
    {
        if (gameplayCamera == null)
        {
            _confiner = null;
            return;
        }

        _confiner = gameplayCamera.GetComponent<CinemachineConfiner2D>();

        if (_confiner == null && addConfinerIfMissing)
        {
            _confiner = gameplayCamera.gameObject.AddComponent<CinemachineConfiner2D>();
        }
    }

    private void ApplyBounds(Collider2D cameraBounds)
    {
        if (cameraBounds == null || !EnsureCameraConfiner())
        {
            return;
        }

        _confiner.BoundingShape2D = cameraBounds;
        _confiner.InvalidateBoundingShapeCache();
    }

    private bool EnsureCameraConfiner()
    {
        if (_confiner != null)
        {
            return true;
        }

        ResolveConfiner();
        return _confiner != null;
    }
}
