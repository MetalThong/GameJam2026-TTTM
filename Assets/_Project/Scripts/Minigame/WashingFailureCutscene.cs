using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WashingFailureCutscene : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private string stateName = "Dish";
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private bool hideWhenFinished = true;
    [SerializeField] private bool useUnscaledTime;
    [SerializeField, Min(0f)] private float fallbackDuration = 0.45f;
    [SerializeField, Min(0f)] private float postHoldDuration = 0.35f;

    private void Awake()
    {
        ResolveReferences();

        if (hideOnAwake)
        {
            SetVisible(false);
        }
    }

    public async UniTask PlayAsync(CancellationToken cancellationToken)
    {
        ResolveReferences();
        SetVisible(true);
        PlayAnimationFromStart();

        float duration = ResolveAnimationDuration();
        await DelayAsync(duration + postHoldDuration, cancellationToken);

        if (hideWhenFinished)
        {
            SetVisible(false);
        }
    }

    private void PlayAnimationFromStart()
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        animator.enabled = true;
        int stateHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, stateHash))
        {
            animator.Play(stateHash, 0, 0f);
            animator.Update(0f);
        }
    }

    private float ResolveAnimationDuration()
    {
        if (animator == null)
        {
            return fallbackDuration;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.length > 0f ? stateInfo.length : fallbackDuration;
    }

    private async UniTask DelayAsync(float seconds, CancellationToken cancellationToken)
    {
        if (seconds <= 0f)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            return;
        }

        TimeSpan delay = TimeSpan.FromSeconds(seconds);
        await UniTask.Delay(delay, ignoreTimeScale: useUnscaledTime, cancellationToken: cancellationToken);
    }

    private void SetVisible(bool visible)
    {
        if (animator != null)
        {
            animator.enabled = visible;
        }

        if (spriteRenderers == null)
        {
            return;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].enabled = visible;
            }
        }
    }

    private void ResolveReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }
}
