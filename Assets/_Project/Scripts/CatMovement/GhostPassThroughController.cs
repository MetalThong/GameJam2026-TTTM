using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Movement))]
public sealed class GhostPassThroughController : MonoBehaviour
{
    [SerializeField] private Movement movement;
    [SerializeField] private bool ignorePlayerTriggerColliders = true;

    private readonly List<Collider2D> _playerColliders = new();
    private readonly List<Collider2D> _passThroughColliders = new();
    private readonly List<IgnoredCollisionPair> _ignoredPairs = new();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (movement != null)
        {
            movement.FormChanged += OnFormChanged;
        }

        GhostPassThroughCollider.RegistryChanged += OnPassThroughRegistryChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshPassThroughState();
    }

    private void FixedUpdate()
    {
        RefreshPassThroughState();
    }

    private void OnDisable()
    {
        if (movement != null)
        {
            movement.FormChanged -= OnFormChanged;
        }

        GhostPassThroughCollider.RegistryChanged -= OnPassThroughRegistryChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RestoreAllIgnoredPairs();
    }

    private void OnFormChanged(MovementForm previousForm, MovementForm currentForm)
    {
        if (previousForm == MovementForm.Ghost && currentForm == MovementForm.Cat)
        {
            RefreshPlayerColliders();
            RefreshPassThroughColliders();
            EnsureIgnoredPairs();
        }

        RefreshPassThroughState();
    }

    private void OnPassThroughRegistryChanged()
    {
        RefreshPassThroughState();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshPassThroughState();
    }

    private void RefreshPassThroughState()
    {
        if (movement == null)
        {
            ResolveReferences();
        }

        RefreshPlayerColliders();
        RefreshPassThroughColliders();

        if (movement != null && movement.CurrentForm == MovementForm.Ghost)
        {
            EnsureIgnoredPairs();
            RemovePairsWithoutActiveColliders();
            return;
        }

        RestoreCatClearPairs();
    }

    private void ResolveReferences()
    {
        if (movement == null)
        {
            movement = GetComponent<Movement>();
        }
    }

    private void RefreshPlayerColliders()
    {
        _playerColliders.Clear();

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (ignorePlayerTriggerColliders && collider.isTrigger)
            {
                continue;
            }

            if (!_playerColliders.Contains(collider))
            {
                _playerColliders.Add(collider);
            }
        }
    }

    private void RefreshPassThroughColliders()
    {
        _passThroughColliders.Clear();
        GhostPassThroughCollider.GetActiveColliders(_passThroughColliders);
    }

    private void EnsureIgnoredPairs()
    {
        for (int i = 0; i < _playerColliders.Count; i++)
        {
            Collider2D playerCollider = _playerColliders[i];
            if (playerCollider == null)
            {
                continue;
            }

            for (int j = 0; j < _passThroughColliders.Count; j++)
            {
                Collider2D passThroughCollider = _passThroughColliders[j];
                if (!CanIgnorePair(playerCollider, passThroughCollider))
                {
                    continue;
                }

                EnsureIgnoredPair(playerCollider, passThroughCollider);
            }
        }
    }

    private void EnsureIgnoredPair(Collider2D playerCollider, Collider2D passThroughCollider)
    {
        for (int i = 0; i < _ignoredPairs.Count; i++)
        {
            if (_ignoredPairs[i].Matches(playerCollider, passThroughCollider))
            {
                return;
            }
        }

        Physics2D.IgnoreCollision(playerCollider, passThroughCollider, true);
        _ignoredPairs.Add(new IgnoredCollisionPair(playerCollider, passThroughCollider));
    }

    private bool CanIgnorePair(Collider2D playerCollider, Collider2D passThroughCollider)
    {
        return playerCollider != null
            && passThroughCollider != null
            && playerCollider != passThroughCollider
            && !passThroughCollider.transform.IsChildOf(transform);
    }

    private void RemovePairsWithoutActiveColliders()
    {
        for (int i = _ignoredPairs.Count - 1; i >= 0; i--)
        {
            IgnoredCollisionPair pair = _ignoredPairs[i];
            if (IsTrackedPlayerCollider(pair.PlayerCollider)
                && IsActivePassThroughCollider(pair.PassThroughCollider))
            {
                continue;
            }

            RestorePair(pair);
            _ignoredPairs.RemoveAt(i);
        }
    }

    private void RestoreCatClearPairs()
    {
        for (int i = _ignoredPairs.Count - 1; i >= 0; i--)
        {
            IgnoredCollisionPair pair = _ignoredPairs[i];
            if (IsTrackedPlayerCollider(pair.PlayerCollider)
                && IsActivePassThroughCollider(pair.PassThroughCollider)
                && AreCollidersOverlapping(pair.PlayerCollider, pair.PassThroughCollider))
            {
                continue;
            }

            RestorePair(pair);
            _ignoredPairs.RemoveAt(i);
        }
    }

    private bool IsTrackedPlayerCollider(Collider2D collider)
    {
        return collider != null && _playerColliders.Contains(collider);
    }

    private bool IsActivePassThroughCollider(Collider2D collider)
    {
        return collider != null && _passThroughColliders.Contains(collider);
    }

    private static bool AreCollidersOverlapping(Collider2D first, Collider2D second)
    {
        if (first == null
            || second == null
            || !first.enabled
            || !second.enabled
            || !first.gameObject.activeInHierarchy
            || !second.gameObject.activeInHierarchy)
        {
            return false;
        }

        ColliderDistance2D distance = first.Distance(second);
        return distance.isValid && distance.isOverlapped;
    }

    private void RestoreAllIgnoredPairs()
    {
        for (int i = _ignoredPairs.Count - 1; i >= 0; i--)
        {
            RestorePair(_ignoredPairs[i]);
        }

        _ignoredPairs.Clear();
    }

    private static void RestorePair(IgnoredCollisionPair pair)
    {
        if (pair.PlayerCollider != null && pair.PassThroughCollider != null)
        {
            Physics2D.IgnoreCollision(pair.PlayerCollider, pair.PassThroughCollider, false);
        }
    }

    private readonly struct IgnoredCollisionPair
    {
        public readonly Collider2D PlayerCollider;
        public readonly Collider2D PassThroughCollider;

        public IgnoredCollisionPair(Collider2D playerCollider, Collider2D passThroughCollider)
        {
            PlayerCollider = playerCollider;
            PassThroughCollider = passThroughCollider;
        }

        public bool Matches(Collider2D playerCollider, Collider2D passThroughCollider)
        {
            return PlayerCollider == playerCollider && PassThroughCollider == passThroughCollider;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (movement == null)
        {
            movement = GetComponent<Movement>();
        }
    }
#endif
}
