using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GhostPassThroughCollider : MonoBehaviour
{
    private static readonly List<GhostPassThroughCollider> ActiveMarkers = new();

    [SerializeField] private Collider2D targetCollider;
    [SerializeField] private bool includeChildColliders;
    [SerializeField] private bool ignoreTriggerColliders = true;

    public static event Action RegistryChanged;

    public static void GetActiveColliders(List<Collider2D> results)
    {
        if (results == null)
        {
            return;
        }

        for (int i = ActiveMarkers.Count - 1; i >= 0; i--)
        {
            GhostPassThroughCollider marker = ActiveMarkers[i];
            if (marker == null || !marker.isActiveAndEnabled)
            {
                ActiveMarkers.RemoveAt(i);
                continue;
            }

            marker.AppendColliders(results);
        }
    }

    private void OnEnable()
    {
        if (!ActiveMarkers.Contains(this))
        {
            ActiveMarkers.Add(this);
        }

        RegistryChanged?.Invoke();
    }

    private void OnDisable()
    {
        ActiveMarkers.Remove(this);
        RegistryChanged?.Invoke();
    }

    private void Reset()
    {
        targetCollider = GetComponent<Collider2D>();
    }

    private void AppendColliders(List<Collider2D> results)
    {
        if (includeChildColliders)
        {
            Collider2D[] childColliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                AddColliderIfAllowed(results, childColliders[i]);
            }

            return;
        }

        Collider2D resolvedCollider = targetCollider != null
            ? targetCollider
            : GetComponent<Collider2D>();

        AddColliderIfAllowed(results, resolvedCollider);
    }

    private void AddColliderIfAllowed(List<Collider2D> results, Collider2D candidate)
    {
        if (candidate == null || !candidate.enabled)
        {
            return;
        }

        if (ignoreTriggerColliders && candidate.isTrigger)
        {
            return;
        }

        if (!results.Contains(candidate))
        {
            results.Add(candidate);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetCollider == null)
        {
            targetCollider = GetComponent<Collider2D>();
        }
    }
#endif
}
