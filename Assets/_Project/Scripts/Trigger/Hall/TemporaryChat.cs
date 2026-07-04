using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class TemporaryChat : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spr;
    [SerializeField] private float exsitsTime = 2f;
    [SerializeField] private float fadeDuration = 0.5f;

    private Tween _fadeTween;
    private CancellationTokenSource _animationCts;

    private void OnEnable()
    {
        CancelAnimation();
        _animationCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        ChatAnimationAsync(_animationCts.Token).Forget();
    }

    private void OnDisable()
    {
        CancelAnimation();
    }

    private void OnDestroy()
    {
        CancelAnimation();
    }

    private async UniTaskVoid ChatAnimationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await FadeToAsync(1f, cancellationToken);
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, exsitsTime)), cancellationToken: cancellationToken);
            await FadeToAsync(0f, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async UniTask FadeToAsync(float targetAlpha, CancellationToken cancellationToken)
    {
        if (spr == null)
        {
            return;
        }

        KillFadeTween();

        float duration = Mathf.Max(0f, fadeDuration);
        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            return;
        }

        Tween tween = spr.DOFade(targetAlpha, duration)
            .SetTarget(spr)
            .SetLink(spr.gameObject, LinkBehaviour.KillOnDestroy);

        _fadeTween = tween;

        using (cancellationToken.Register(() => tween.Kill()))
        {
            await tween.AsyncWaitForCompletion();
        }

        if (_fadeTween == tween)
        {
            _fadeTween = null;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (spr == null)
        {
            return;
        }

        Color color = spr.color;
        color.a = alpha;
        spr.color = color;
    }

    private void CancelAnimation()
    {
        if (_animationCts != null)
        {
            _animationCts.Cancel();
            _animationCts.Dispose();
            _animationCts = null;
        }

        KillFadeTween();
    }

    private void KillFadeTween()
    {
        if (_fadeTween != null)
        {
            _fadeTween.Kill();
            _fadeTween = null;
        }

        if (spr != null)
        {
            DOTween.Kill(spr);
        }
    }
}
