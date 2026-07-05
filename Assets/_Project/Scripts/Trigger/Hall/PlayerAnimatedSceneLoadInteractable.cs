using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class PlayerAnimatedSceneLoadInteractable : SceneLoadInteractable
{
    [Header("Player Animation")]
    [SerializeField] private string animationTrigger = "IsPicture";
    [SerializeField] private string animationStateName = "GoToPicture";
    [SerializeField] private bool requireCatForm;
    [SerializeField] private bool lockMovementDuringAnimation = true;
    [SerializeField] private bool hidePlayerAfterAnimation = true;
    [SerializeField, Min(0f)] private float fallbackAnimationDuration = 0.72f;
    [SerializeField] private bool freezeFinalFrameBeforeLoad = true;
    [SerializeField, Range(0.8f, 1f)] private float finalFrameNormalizedTime = 0.98f;
    [SerializeField, Min(0f)] private float animationStateWaitTimeout = 0.25f;
    [SerializeField] private bool setFormBeforeLoad;
    [SerializeField] private MovementForm formBeforeLoad = MovementForm.Ghost;

    protected override async UniTask BeforeLoadAsync()
    {
        Movement movement = ResolvePlayerMovement();
        if (movement == null)
        {
            return;
        }

        CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();
        GameState previousState = GameManager.Instance != null
            ? GameManager.Instance.CurrentState
            : GameState.Playing;
        bool lockedMovement = false;
        Animator animator = movement.GetComponentInChildren<Animator>(true);
        float originalAnimatorSpeed = animator != null ? animator.speed : 1f;
        bool frozeAnimator = false;

        try
        {
            if (lockMovementDuringAnimation && GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.OnDialog);
                lockedMovement = true;
            }

            bool played = movement.TryPlayAnimationTrigger(animationTrigger, requireCatForm);
            if (!played)
            {
                return;
            }

            float waitDuration = ResolveAnimationDuration(movement);
            if (waitDuration <= 0f)
            {
                waitDuration = fallbackAnimationDuration;
            }

            await WaitForAnimationEndAsync(animator, waitDuration, destroyToken);

            if (freezeFinalFrameBeforeLoad)
            {
                frozeAnimator = FreezeAnimationAtFinalFrame(animator);
            }

            if (hidePlayerAfterAnimation)
            {
                SetPlayerAlpha(movement, 0f);
            }

            if (setFormBeforeLoad)
            {
                movement.SetForm(formBeforeLoad);
            }
        }
        finally
        {
            if (frozeAnimator && animator != null)
            {
                animator.speed = originalAnimatorSpeed;
            }

            if (lockedMovement
                && GameManager.Instance != null
                && GameManager.Instance.CurrentState == GameState.OnDialog)
            {
                GameManager.Instance.SetState(previousState == GameState.OnDialog
                    ? GameState.Playing
                    : previousState);
            }
        }
    }

    private float ResolveAnimationDuration(Movement movement)
    {
        if (movement == null || string.IsNullOrWhiteSpace(animationStateName))
        {
            return 0f;
        }

        Animator animator = movement.GetComponentInChildren<Animator>(true);
        RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
        if (controller == null)
        {
            return 0f;
        }

        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip == null)
            {
                continue;
            }

            if (!MatchesAnimationName(clip.name, animationStateName))
            {
                continue;
            }

            float speed = animator != null ? Mathf.Abs(animator.speed) : 1f;
            if (Mathf.Approximately(speed, 0f))
            {
                speed = 1f;
            }

            return clip.length / speed;
        }

        return 0f;
    }

    private async UniTask WaitForAnimationEndAsync(
        Animator animator,
        float fallbackDuration,
        CancellationToken cancellationToken)
    {
        if (animator == null || string.IsNullOrWhiteSpace(animationStateName))
        {
            await DelayIfNeededAsync(fallbackDuration, cancellationToken);
            return;
        }

        bool enteredState = await WaitForAnimationStateAsync(animator, cancellationToken);
        if (!enteredState)
        {
            await DelayIfNeededAsync(fallbackDuration, cancellationToken);
            return;
        }

        float targetNormalizedTime = freezeFinalFrameBeforeLoad
            ? finalFrameNormalizedTime
            : 1f;
        float startTime = Time.time;
        float maxWaitDuration = Mathf.Max(fallbackDuration + animationStateWaitTimeout, fallbackAnimationDuration);

        while (Time.time - startTime <= maxWaitDuration)
        {
            if (TryGetAnimationNormalizedTime(animator, out float normalizedTime)
                && normalizedTime >= targetNormalizedTime)
            {
                return;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
    }

    private async UniTask<bool> WaitForAnimationStateAsync(
        Animator animator,
        CancellationToken cancellationToken)
    {
        float startTime = Time.time;
        while (Time.time - startTime <= animationStateWaitTimeout)
        {
            if (TryGetAnimationNormalizedTime(animator, out _))
            {
                return true;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }

        return false;
    }

    private static async UniTask DelayIfNeededAsync(float duration, CancellationToken cancellationToken)
    {
        if (duration > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: cancellationToken);
        }
    }

    private bool FreezeAnimationAtFinalFrame(Animator animator)
    {
        if (animator == null || string.IsNullOrWhiteSpace(animationStateName))
        {
            return false;
        }

        int stateHash = Animator.StringToHash(animationStateName);
        if (animator.HasState(0, stateHash))
        {
            animator.Play(stateHash, 0, finalFrameNormalizedTime);
            animator.Update(0f);
        }

        animator.speed = 0f;
        return true;
    }

    private bool TryGetAnimationNormalizedTime(Animator animator, out float normalizedTime)
    {
        normalizedTime = 0f;
        if (animator == null || string.IsNullOrWhiteSpace(animationStateName))
        {
            return false;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (MatchesState(currentState))
        {
            normalizedTime = currentState.normalizedTime;
            return true;
        }

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
            if (MatchesState(nextState))
            {
                normalizedTime = nextState.normalizedTime;
                return true;
            }
        }

        return false;
    }

    private bool MatchesState(AnimatorStateInfo stateInfo)
    {
        int shortNameHash = Animator.StringToHash(animationStateName);
        int fullPathHash = Animator.StringToHash($"Base Layer.{animationStateName}");
        return stateInfo.shortNameHash == shortNameHash || stateInfo.fullPathHash == fullPathHash;
    }

    private static void SetPlayerAlpha(Movement movement, float alpha)
    {
        if (movement == null)
        {
            return;
        }

        SpriteRenderer[] renderers = movement.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }

    private static bool MatchesAnimationName(string clipName, string stateName)
    {
        return string.Equals(clipName, stateName, StringComparison.Ordinal)
            || clipName.Contains(stateName, StringComparison.Ordinal)
            || stateName.Contains(clipName, StringComparison.Ordinal);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolvePlayerMovement();
    }
#endif
}
