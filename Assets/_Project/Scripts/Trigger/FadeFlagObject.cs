using UnityEngine;
using DG.Tweening;

public class FadeFlagObject : FlagBasedObject
{
    [SerializeField] private float fadeDuration = 0.3f;

    private SpriteRenderer _targetSpr;
    private Tween _fadeTween;
    private bool _hasCompletedInitialRefresh;

    private void Awake()
    {
        if (target != null)
        {
            target.TryGetComponent(out _targetSpr);
        }
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
    }

    protected override void Refresh()
    {
        if (!TryGetTargetActiveState(out bool shouldBeActive))
        {
            return;
        }

        if (_targetSpr == null)
        {
            SetTargetActive();
            _hasCompletedInitialRefresh = true;
            return;
        }

        _fadeTween?.Kill();

        bool shouldAnimate = _hasCompletedInitialRefresh && Application.isPlaying && fadeDuration > 0f;
        if (!shouldAnimate)
        {
            SetTargetInstant(shouldBeActive);
            _hasCompletedInitialRefresh = true;
            return;
        }

        target.SetActive(true);

        float targetAlpha = shouldBeActive ? 1f : 0f;
        _fadeTween = _targetSpr.DOFade(targetAlpha, fadeDuration).OnComplete(() =>
        {
            if (!shouldBeActive)
            {
                target.SetActive(false);
            }
        });

        _hasCompletedInitialRefresh = true;
    }

    private void SetTargetInstant(bool shouldBeActive)
    {
        target.SetActive(shouldBeActive);

        Color color = _targetSpr.color;
        color.a = shouldBeActive ? 1f : 0f;
        _targetSpr.color = color;
    }
}
