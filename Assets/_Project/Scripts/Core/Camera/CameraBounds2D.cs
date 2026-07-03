using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class CameraBounds2D : MonoBehaviour
{
    [SerializeField] private Collider2D boundsCollider;

    private void Awake()
    {
        if (boundsCollider == null)
        {
            boundsCollider = GetComponent<Collider2D>();
        }
    }

    private void Start()
    {
        RegisterBounds();
    }

    private void OnEnable()
    {
        RegisterBounds();
    }

    private void RegisterBounds()
    {
        if (boundsCollider == null || CameraManager.Instance == null)
        {
            return;
        }

        CameraManager.Instance.SetBounds(boundsCollider);
    }
}
