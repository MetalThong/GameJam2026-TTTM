using UnityEngine;
using DG.Tweening;

public class FadeFlagObject : FlagBasedObject
{
    [SerializeField] private float fadeDuration = 0.3f;

    private SpriteRenderer _targetSpr;
    private Tween _fadeTween;

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
            return;
        }

        _fadeTween?.Kill();

        target.SetActive(true);

        float targetAlpha = shouldBeActive ? 1f : 0f;
        _fadeTween = _targetSpr.DOFade(targetAlpha, fadeDuration).OnComplete(() =>
        {
            if (!shouldBeActive)
            {
                target.SetActive(false);
            }
        });
    }
}
