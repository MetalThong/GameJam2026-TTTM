using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class WorldTargetUIFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Vector3 worldOffset = new(0f, 1.25f, 0f);
    [SerializeField] private Vector2 screenOffset;
    [SerializeField] private bool autoResolvePlayerTarget = true;
    [SerializeField] private bool hideWhenTargetMissing = true;
    [SerializeField] private bool hideWhenBehindCamera = true;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        FollowTarget();
    }

    private void LateUpdate()
    {
        FollowTarget();
    }

    private void FollowTarget()
    {
        ResolveReferences();

        if (_rectTransform == null || target == null)
        {
            SetVisible(!hideWhenTargetMissing);
            return;
        }

        Vector3 worldPosition = target.position + worldOffset;
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
        {
            _rectTransform.position = worldPosition;
            SetVisible(true);
            return;
        }

        Camera resolvedWorldCamera = ResolveWorldCamera();
        if (hideWhenBehindCamera && resolvedWorldCamera != null)
        {
            Vector3 viewportPosition = resolvedWorldCamera.WorldToViewportPoint(worldPosition);
            if (viewportPosition.z < 0f)
            {
                SetVisible(false);
                return;
            }
        }

        RectTransform parentRect = _rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            SetVisible(false);
            return;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(resolvedWorldCamera, worldPosition) + screenOffset;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            _rectTransform.anchoredPosition = localPoint;
            SetVisible(true);
            return;
        }

        SetVisible(false);
    }

    private void ResolveReferences()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (target == null && autoResolvePlayerTarget)
        {
            Movement parentMovement = GetComponentInParent<Movement>();
            if (parentMovement != null)
            {
                target = parentMovement.transform;
            }
        }

        if (target == null && autoResolvePlayerTarget)
        {
            Movement movement = Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
            if (movement != null)
            {
                target = movement.transform;
            }
        }
    }

    private Camera ResolveWorldCamera()
    {
        if (worldCamera != null)
        {
            return worldCamera;
        }

        if (canvas != null && canvas.worldCamera != null)
        {
            worldCamera = canvas.worldCamera;
            return worldCamera;
        }

        worldCamera = Camera.main;
        return worldCamera;
    }

    private void SetVisible(bool isVisible)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = isVisible ? 1f : 0f;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif
}
