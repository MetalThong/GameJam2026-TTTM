using UnityEngine;

[DisallowMultipleComponent]
public sealed class CarryableInteractable : MonoBehaviour, IInteractable, ICarryable
{
    [SerializeField] private string carryId;
    [SerializeField] private GameObject carryPrefab;
    [SerializeField] private Transform carryScaleSource;
    [SerializeField] private Vector3 carriedScaleMultiplier = Vector3.one;
    [SerializeField] private bool hideSourceOnGrab = true;
    [SerializeField] private bool hideSourceWhenAlreadyCarried = true;
    [SerializeField] private bool allowReplaceCurrentCarry;

    public string CarryId => string.IsNullOrWhiteSpace(carryId) ? gameObject.name : carryId;
    public GameObject CarryPrefab => carryPrefab != null ? carryPrefab : gameObject;
    public Vector3 CarryWorldScale
    {
        get
        {
            Transform scaleSource = carryScaleSource != null ? carryScaleSource : transform;
            return Vector3.Scale(scaleSource.lossyScale, carriedScaleMultiplier);
        }
    }

    private void OnEnable()
    {
        if (!hideSourceWhenAlreadyCarried || CarryManager.Instance == null)
        {
            return;
        }

        if (CarryManager.Instance.IsCarryingId(CarryId))
        {
            gameObject.SetActive(false);
        }
    }

    public bool TryInteract()
    {
        CarryManager carryManager = CarryManager.GetOrCreate();
        if (carryManager == null)
        {
            Debug.LogWarning("CarryableInteractable: no CarryManager was found.", this);
            return false;
        }

        if (CarryPrefab == null)
        {
            Debug.LogWarning("CarryableInteractable: carryPrefab is not assigned.", this);
            return false;
        }

        if (carryManager.IsCarrying)
        {
            if (!allowReplaceCurrentCarry)
            {
                return false;
            }

            carryManager.Drop();
        }

        carryManager.Grab(this);

        if (hideSourceOnGrab)
        {
            gameObject.SetActive(false);
        }

        return true;
    }
}
