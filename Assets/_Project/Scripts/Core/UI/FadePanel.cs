using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class FadePanel : UIPanelView
{
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private bool blockRaycastsWhileVisible = true;

    private CanvasGroup _canvasGroup;
    private Tween _fadeTween;
    private bool _isTransitioning;

    private void Awake()
    {
        ResolveCanvasGroup();

        if (hideOnAwake && !_isTransitioning)
        {
            SetVisibleInstant(false);
        }
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
    }

    public override void Show()
    {
        FadeInAsync().Forget();
    }

    public override void Hide()
    {
        FadeOutAsync().Forget();
    }

    public async UniTask FadeInAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            this.GetCancellationTokenOnDestroy());

        await FadeToAsync(visibleAlpha, fadeInDuration, true, true, linkedTokenSource.Token);
    }

    public async UniTask FadeOutAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            this.GetCancellationTokenOnDestroy());

        await FadeToAsync(0f, fadeOutDuration, false, true, linkedTokenSource.Token);
    }

    public void SetVisibleInstant(bool isVisible)
    {
        ResolveCanvasGroup();
        _fadeTween?.Kill();

        _canvasGroup.alpha = isVisible ? visibleAlpha : 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = isVisible && blockRaycastsWhileVisible;
        gameObject.SetActive(isVisible);
    }

    private async UniTask FadeToAsync(
        float targetAlpha,
        float duration,
        bool activeWhenComplete,
        bool blockRaycastsDuringFade,
        CancellationToken cancellationToken)
    {
        ResolveCanvasGroup();
        _fadeTween?.Kill();

        _isTransitioning = true;
        try
        {
            gameObject.SetActive(true);
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = blockRaycastsDuringFade && blockRaycastsWhileVisible;

            if (duration <= 0f)
            {
                _canvasGroup.alpha = targetAlpha;
                CompleteFade(activeWhenComplete);
                return;
            }

            Tween tween = _canvasGroup.DOFade(targetAlpha, duration).SetUpdate(true);
            _fadeTween = tween;

            using (cancellationToken.Register(() => tween.Kill()))
            {
                await tween.AsyncWaitForCompletion();
            }

            if (cancellationToken.IsCancellationRequested || _fadeTween != tween)
            {
                return;
            }

            _fadeTween = null;
            CompleteFade(activeWhenComplete);
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    private void CompleteFade(bool activeWhenComplete)
    {
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = activeWhenComplete && blockRaycastsWhileVisible;
        gameObject.SetActive(activeWhenComplete);
    }

    private void ResolveCanvasGroup()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}
