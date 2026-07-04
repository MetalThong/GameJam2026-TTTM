using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TemporaryChat : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spr;
    [SerializeField, Min(0f)] private float showDelay;
    [SerializeField] private float exsitsTime = 2f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private bool lockMovementWhileVisible;
    [SerializeField] private bool deactivateAfterFade;
    [SerializeField] private string completionFlag;
    [SerializeField] private GameObject deactivateTarget;

    private Tween _fadeTween;
    private CancellationTokenSource _animationCts;
    private SpriteRenderer[] _spriteRenderers;
    private Graphic[] _graphics;
    private GameState _previousGameState = GameState.Playing;
    private bool _hasLockedGameState;

    private void OnEnable()
    {
        CancelAnimation();
        ResolveVisualTargets();
        SetAlpha(0f);
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
        bool completed = false;

        try
        {
            float delay = Mathf.Max(0f, showDelay);
            if (delay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);
            }

            LockMovement();
            await FadeToAsync(1f, cancellationToken);
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, exsitsTime)), cancellationToken: cancellationToken);
            await FadeToAsync(0f, cancellationToken);
            completed = true;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            ReleaseMovementLock();

            if (completed)
            {
                SetCompletionFlag();
                DeactivateAfterFadeIfNeeded();
            }
        }
    }

    private async UniTask FadeToAsync(float targetAlpha, CancellationToken cancellationToken)
    {
        ResolveVisualTargets();
        KillFadeTween();

        float duration = Mathf.Max(0f, fadeDuration);
        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            return;
        }

        Sequence tween = DOTween.Sequence()
            .SetTarget(gameObject)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = _spriteRenderers[i];
            if (spriteRenderer != null)
            {
                tween.Join(spriteRenderer.DOFade(targetAlpha, duration));
            }
        }

        for (int i = 0; i < _graphics.Length; i++)
        {
            Graphic graphic = _graphics[i];
            if (graphic != null)
            {
                tween.Join(graphic.DOFade(targetAlpha, duration));
            }
        }

        if (!tween.IsActive() || tween.Duration() <= 0f)
        {
            SetAlpha(targetAlpha);
            return;
        }

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

    private void ResolveVisualTargets()
    {
        if (spr == null)
        {
            spr = GetComponentInChildren<SpriteRenderer>(true);
        }

        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _graphics = GetComponentsInChildren<Graphic>(true);
    }

    private void CancelAnimation()
    {
        if (_animationCts != null)
        {
            _animationCts.Cancel();
            _animationCts.Dispose();
            _animationCts = null;
        }

        ReleaseMovementLock();
        KillFadeTween();
    }

    private void KillFadeTween()
    {
        if (_fadeTween != null)
        {
            _fadeTween.Kill();
            _fadeTween = null;
        }

        DOTween.Kill(gameObject);

        if (_spriteRenderers != null)
        {
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] != null)
                {
                    DOTween.Kill(_spriteRenderers[i]);
                }
            }
        }

        if (_graphics != null)
        {
            for (int i = 0; i < _graphics.Length; i++)
            {
                if (_graphics[i] != null)
                {
                    DOTween.Kill(_graphics[i]);
                }
            }
        }
    }

    private void LockMovement()
    {
        if (!lockMovementWhileVisible || _hasLockedGameState || GameManager.Instance == null)
        {
            return;
        }

        _previousGameState = GameManager.Instance.CurrentState;
        if (_previousGameState == GameState.OnDialog)
        {
            return;
        }

        _hasLockedGameState = true;
        GameManager.Instance.SetState(GameState.OnDialog);
    }

    private void ReleaseMovementLock()
    {
        if (!_hasLockedGameState)
        {
            return;
        }

        _hasLockedGameState = false;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OnDialog)
        {
            GameManager.Instance.SetState(_previousGameState == GameState.OnDialog
                ? GameState.Playing
                : _previousGameState);
        }
    }

    private void SetCompletionFlag()
    {
        if (!string.IsNullOrWhiteSpace(completionFlag) && FlagManager.Instance != null)
        {
            FlagManager.Instance.SetFlag(completionFlag, true);
        }
    }

    private void DeactivateAfterFadeIfNeeded()
    {
        if (!deactivateAfterFade)
        {
            return;
        }

        GameObject target = deactivateTarget != null ? deactivateTarget : gameObject;
        if (target != null)
        {
            target.SetActive(false);
        }
    }
}
