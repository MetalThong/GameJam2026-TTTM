using System;
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
            Transform scaleSource = ResolveCarryScaleSource();
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

        bool grabbed = carryManager.Grab(this);
        if (!grabbed)
        {
            return false;
        }

        if (hideSourceOnGrab)
        {
            gameObject.SetActive(false);
        }

        return true;
    }

    private Transform ResolveCarryScaleSource()
    {
        if (carryScaleSource != null && carryScaleSource != transform)
        {
            return carryScaleSource;
        }

        Transform carriedVisual = FindCarriedVisualTransform();
        if (carriedVisual != null)
        {
            return carriedVisual;
        }

        return carryScaleSource != null ? carryScaleSource : transform;
    }

    private Transform FindCarriedVisualTransform()
    {
        GameObject prefab = CarryPrefab;
        if (prefab == null)
        {
            return null;
        }

        string prefabName = NormalizeObjectName(prefab.name);
        Transform namedMatch = FindChildByNormalizedName(transform, prefabName);
        if (namedMatch != null)
        {
            return namedMatch;
        }

        SpriteRenderer prefabRenderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
        if (prefabRenderer == null || prefabRenderer.sprite == null)
        {
            return null;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer != null && renderer.sprite == prefabRenderer.sprite)
            {
                return renderer.transform;
            }
        }

        return null;
    }

    private static Transform FindChildByNormalizedName(Transform root, string normalizedName)
    {
        if (root == null || string.IsNullOrEmpty(normalizedName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(NormalizeObjectName(child.name), normalizedName, StringComparison.Ordinal))
            {
                return child;
            }

            Transform nestedMatch = FindChildByNormalizedName(child, normalizedName);
            if (nestedMatch != null)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    private static string NormalizeObjectName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return string.Empty;
        }

        return objectName.Replace("(Clone)", string.Empty).Trim();
    }
}
