using DG.Tweening;
using UnityEngine;

// Visual feedback for a MashStoryInteractable: fills a progress bar (color goes toward red as it
// fills), punches an emote transform on every press, and can hide the whole group when progress is empty.
[RequireComponent(typeof(MashStoryInteractable))]
public class MashProgressView : MonoBehaviour
{
    [Header("Bindings")]
    [Tooltip("Root object used by the mash feedback. It can either stay visible for the whole phase or only appear while progress > 0.")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private bool hideWhenEmpty = true;

    [Header("Progress Bar")]
    [Tooltip("Fill sprite scaled along X from 0..1. Its pivot should be on the left edge.")]
    [SerializeField] private SpriteRenderer fillRenderer;
    [SerializeField] private Color emptyColor = new(1f, 0.8f, 0.3f, 1f);
    [SerializeField] private Color fullColor = new(0.9f, 0.15f, 0.15f, 1f);
    [SerializeField] private float fillLerpSpeed = 12f;

    [Header("Emote")]
    [Tooltip("Transform that shakes on each press (e.g. TriggerEmote).")]
    [SerializeField] private Transform emoteTransform;
    [SerializeField] private float punchScale = 0.25f;
    [SerializeField] private float punchDuration = 0.2f;

    private MashStoryInteractable _mash;
    private float _displayedProgress;
    private float _targetProgress;
    private Vector3 _fillBaseScale = Vector3.one;
    private Vector3 _emoteBaseScale = Vector3.one;
    private Tween _emoteTween;

    private void Awake()
    {
        _mash = GetComponent<MashStoryInteractable>();

        if (visualRoot == null)
        {
            visualRoot = gameObject;
        }

        ResolveMissingBindings();

        if (fillRenderer != null)
        {
            _fillBaseScale = fillRenderer.transform.localScale;
        }

        if (emoteTransform != null)
        {
            _emoteBaseScale = emoteTransform.localScale;
        }

        ApplyFill(0f);
        if (hideWhenEmpty)
        {
            SetVisible(false);
        }
    }

    private void OnEnable()
    {
        _mash.Pressed += OnPressed;
        _mash.ProgressChanged += OnProgressChanged;
        _mash.Succeeded += OnSucceeded;
    }

    private void OnDisable()
    {
        _mash.Pressed -= OnPressed;
        _mash.ProgressChanged -= OnProgressChanged;
        _mash.Succeeded -= OnSucceeded;
        _emoteTween?.Kill();
    }

    private void Update()
    {
        if (Mathf.Approximately(_displayedProgress, _targetProgress))
        {
            return;
        }

        // Smoothly chase the target so both filling and decaying animate instead of snapping.
        _displayedProgress = Mathf.Lerp(_displayedProgress, _targetProgress, fillLerpSpeed * Time.deltaTime);

        if (Mathf.Abs(_displayedProgress - _targetProgress) < 0.001f)
        {
            _displayedProgress = _targetProgress;
        }

        ApplyFill(_displayedProgress);

        if (hideWhenEmpty && _targetProgress <= 0f && _displayedProgress <= 0.001f)
        {
            SetVisible(false);
        }
    }

    private void OnPressed()
    {
        SetVisible(true);
        PunchEmote(1f);
    }

    private void OnProgressChanged(float normalized)
    {
        _targetProgress = Mathf.Clamp01(normalized);

        if (_targetProgress > 0f || !hideWhenEmpty)
        {
            SetVisible(true);
        }
    }

    private void OnSucceeded()
    {
        SetVisible(true);
        _displayedProgress = 1f;
        _targetProgress = 1f;
        ApplyFill(1f);
        PunchEmote(1.6f);
    }

    private void ApplyFill(float normalized)
    {
        if (fillRenderer != null)
        {
            Vector3 scale = fillRenderer.transform.localScale;
            scale.x = _fillBaseScale.x * normalized;
            scale.y = _fillBaseScale.y;
            scale.z = _fillBaseScale.z;
            fillRenderer.transform.localScale = scale;
            fillRenderer.color = Color.Lerp(emptyColor, fullColor, normalized);
        }
    }

    private void PunchEmote(float multiplier)
    {
        if (emoteTransform == null)
        {
            return;
        }

        _emoteTween?.Kill();
        emoteTransform.localScale = _emoteBaseScale;
        _emoteTween = emoteTransform
            .DOPunchScale(_emoteBaseScale * punchScale * multiplier, punchDuration, 8, 1f);
    }

    private void SetVisible(bool visible)
    {
        if (visualRoot != null && visualRoot.activeSelf != visible)
        {
            visualRoot.SetActive(visible);
        }
    }

    private void ResolveMissingBindings()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (fillRenderer == null)
        {
            fillRenderer = FindFillRenderer();
        }

        if (emoteTransform == null)
        {
            emoteTransform = visualRoot.transform;
        }
    }

    private SpriteRenderer FindFillRenderer()
    {
        SpriteRenderer[] renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0)
        {
            return null;
        }

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.name.ToLowerInvariant().Contains("full"))
            {
                return renderer;
            }
        }

        return renderers[renderers.Length - 1];
    }
}
