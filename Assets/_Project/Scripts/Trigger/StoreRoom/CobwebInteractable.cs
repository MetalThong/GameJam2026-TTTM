using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CobwebInteractable : MonoBehaviour, IInteractable, IInteractionPromptProvider
{
    [Header("Fade")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField, Min(0f)] private float fadeDuration = 0.35f;
    [SerializeField] private Ease fadeEase = Ease.OutQuad;
    [SerializeField] private bool deactivateVisualAfterFade = true;
    [SerializeField] private bool disableCollidersOnInteract = true;

    [Header("Completion")]
    [SerializeField] private bool interactOnce = true;
    [SerializeField] private string completionFlagId;
    [SerializeField] private bool hideWhenCompletionFlagExists = true;

    [Header("Player Scratch Animation")]
    [SerializeField] private bool playScratchAnimation = true;
    [SerializeField] private Movement playerMovement;
    [SerializeField] private string scratchAnimationTrigger = "IsScratch";
    [SerializeField] private bool requireCatFormForScratchAnimation = true;

    [Header("Prompt")]
    [SerializeField] private string promptLocalizationKey = "prompt.interact";

    private Collider2D[] _colliders;
    private SpriteRenderer[] _spriteRenderers;
    private Graphic[] _graphics;
    private Tween _fadeTween;
    private bool _isFading;
    private bool _isDone;

    public string PromptLocalizationKey => promptLocalizationKey;

    private GameObject VisualRoot => visualRoot != null ? visualRoot : gameObject;

    private void Awake()
    {
        ResolveColliders();
        ResolveVisualTargets();
        RefreshDoneStateFromFlag();

        if (_isDone && hideWhenCompletionFlagExists)
        {
            ApplyCompletedStateInstant();
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);

        RefreshDoneStateFromFlag();
        if (_isDone && hideWhenCompletionFlagExists)
        {
            ApplyCompletedStateInstant();
        }
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
    }

    public bool TryInteract()
    {
        if (_isFading || (interactOnce && _isDone))
        {
            return false;
        }

        PlayScratchAnimation();
        FadeOut();
        return true;
    }

    private void PlayScratchAnimation()
    {
        if (!playScratchAnimation || string.IsNullOrWhiteSpace(scratchAnimationTrigger))
        {
            return;
        }

        Movement movement = ResolvePlayerMovement();
        movement?.TryPlayAnimationTrigger(scratchAnimationTrigger, requireCatFormForScratchAnimation);
    }

    private Movement ResolvePlayerMovement()
    {
        if (playerMovement == null)
        {
            playerMovement = Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
        }

        return playerMovement;
    }

    private void FadeOut()
    {
        _isFading = true;

        if (disableCollidersOnInteract)
        {
            SetCollidersEnabled(false);
        }

        GameObject root = VisualRoot;
        if (root != null)
        {
            root.SetActive(true);
        }

        ResolveVisualTargets();
        _fadeTween?.Kill();

        float duration = Mathf.Max(0f, fadeDuration);
        if (duration <= 0f)
        {
            SetAlpha(0f);
            CompleteFade();
            return;
        }

        Sequence sequence = DOTween.Sequence()
            .SetTarget(gameObject)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        int tweenCount = 0;

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = _spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            DOTween.Kill(spriteRenderer);
            sequence.Join(spriteRenderer.DOFade(0f, duration).SetEase(fadeEase));
            tweenCount++;
        }

        for (int i = 0; i < _graphics.Length; i++)
        {
            Graphic graphic = _graphics[i];
            if (graphic == null)
            {
                continue;
            }

            DOTween.Kill(graphic);
            sequence.Join(graphic.DOFade(0f, duration).SetEase(fadeEase));
            tweenCount++;
        }

        if (tweenCount <= 0)
        {
            CompleteFade();
            return;
        }

        _fadeTween = sequence.OnComplete(CompleteFade);
    }

    private void CompleteFade()
    {
        _fadeTween = null;
        _isFading = false;
        _isDone = true;

        SetCompletionFlag();

        if (deactivateVisualAfterFade && VisualRoot != null)
        {
            VisualRoot.SetActive(false);
        }
    }

    private void ApplyCompletedStateInstant()
    {
        _fadeTween?.Kill();
        _fadeTween = null;
        _isFading = false;
        _isDone = true;

        SetAlpha(0f);
        SetCollidersEnabled(false);

        if (deactivateVisualAfterFade && VisualRoot != null)
        {
            VisualRoot.SetActive(false);
        }
    }

    private void ResolveColliders()
    {
        _colliders = GetComponents<Collider2D>();
    }

    private void ResolveVisualTargets()
    {
        GameObject root = VisualRoot;
        if (root == null)
        {
            _spriteRenderers = System.Array.Empty<SpriteRenderer>();
            _graphics = System.Array.Empty<Graphic>();
            return;
        }

        _spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        _graphics = root.GetComponentsInChildren<Graphic>(true);
    }

    private void SetAlpha(float alpha)
    {
        ResolveVisualTargets();

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = _spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        for (int i = 0; i < _graphics.Length; i++)
        {
            Graphic graphic = _graphics[i];
            if (graphic == null)
            {
                continue;
            }

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }

    private void SetCollidersEnabled(bool isEnabled)
    {
        ResolveColliders();

        for (int i = 0; i < _colliders.Length; i++)
        {
            Collider2D collider = _colliders[i];
            if (collider != null)
            {
                collider.enabled = isEnabled;
            }
        }
    }

    private void RefreshDoneStateFromFlag()
    {
        if (string.IsNullOrWhiteSpace(completionFlagId) || FlagManager.Instance == null)
        {
            return;
        }

        _isDone = FlagManager.Instance.HasFlag(completionFlagId);
    }

    private void SetCompletionFlag()
    {
        if (!string.IsNullOrWhiteSpace(completionFlagId) && FlagManager.Instance != null)
        {
            FlagManager.Instance.SetFlag(completionFlagId, true);
        }
    }

    private void OnFlagChanged(FlagChangedEvent eventData)
    {
        if (eventData.FlagId == completionFlagId && eventData.Value && hideWhenCompletionFlagExists)
        {
            ApplyCompletedStateInstant();
        }
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        RefreshDoneStateFromFlag();

        if (_isDone && hideWhenCompletionFlagExists)
        {
            ApplyCompletedStateInstant();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (fadeDuration < 0f)
        {
            fadeDuration = 0f;
        }

        if (playerMovement == null)
        {
            playerMovement = Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
        }
    }
#endif
}
