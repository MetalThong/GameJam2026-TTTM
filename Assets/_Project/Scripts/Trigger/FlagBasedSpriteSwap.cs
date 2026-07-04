using UnityEngine;

[DisallowMultipleComponent]
public sealed class FlagBasedSpriteSwap : MonoBehaviour
{
    [SerializeField] private string flagId = "boss_wake_up";
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private string targetChildName;
    [SerializeField] private Sprite flaggedSprite;
    [SerializeField] private bool restoreOriginalWhenFlagMissing = true;

    private Sprite _originalSprite;
    private bool _hasOriginalSprite;
    private bool _warnedMissingRenderer;

    private void Awake()
    {
        ResolveTargetRenderer();
        CacheOriginalSprite();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        Refresh();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);
    }

    private void OnFlagChanged(FlagChangedEvent eventData)
    {
        if (eventData.FlagId == flagId)
        {
            Refresh();
        }
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (string.IsNullOrWhiteSpace(flagId) || FlagManager.Instance == null)
        {
            return;
        }

        ResolveTargetRenderer();
        if (targetRenderer == null)
        {
            WarnMissingRenderer();
            return;
        }

        CacheOriginalSprite();

        bool hasFlag = FlagManager.Instance.HasFlag(flagId);
        if (hasFlag)
        {
            if (flaggedSprite != null)
            {
                targetRenderer.sprite = flaggedSprite;
            }

            return;
        }

        if (restoreOriginalWhenFlagMissing && _hasOriginalSprite)
        {
            targetRenderer.sprite = _originalSprite;
        }
    }

    private void ResolveTargetRenderer()
    {
        if (targetRenderer != null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(targetChildName))
        {
            Transform namedChild = FindChildByName(targetChildName);
            if (namedChild != null)
            {
                targetRenderer = namedChild.GetComponentInChildren<SpriteRenderer>(true);
                if (targetRenderer != null)
                {
                    return;
                }
            }
        }

        targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private Transform FindChildByName(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.gameObject.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private void CacheOriginalSprite()
    {
        if (_hasOriginalSprite || targetRenderer == null)
        {
            return;
        }

        _originalSprite = targetRenderer.sprite;
        _hasOriginalSprite = true;
    }

    private void WarnMissingRenderer()
    {
        if (_warnedMissingRenderer)
        {
            return;
        }

        _warnedMissingRenderer = true;
        Debug.LogWarning("[FlagBasedSpriteSwap] Target SpriteRenderer is missing.", this);
    }
}
