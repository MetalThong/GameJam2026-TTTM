using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class PlayerAnimatedSceneLoadInteractable : SceneLoadInteractable
{
    [Header("Player Animation")]
    [SerializeField] private Movement playerMovement;
    [SerializeField] private string animationTrigger = "IsPicture";
    [SerializeField] private string animationStateName = "GoToPicture";
    [SerializeField] private bool requireCatForm;
    [SerializeField] private bool lockMovementDuringAnimation = true;
    [SerializeField] private bool hidePlayerAfterAnimation = true;
    [SerializeField, Min(0f)] private float fallbackAnimationDuration = 0.72f;

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

            if (waitDuration > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(waitDuration), cancellationToken: destroyToken);
            }

            if (hidePlayerAfterAnimation)
            {
                SetPlayerAlpha(movement, 0f);
            }
        }
        finally
        {
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

    private Movement ResolvePlayerMovement()
    {
        if (playerMovement == null)
        {
            playerMovement = UnityEngine.Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
        }

        return playerMovement;
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
        if (playerMovement == null)
        {
            playerMovement = UnityEngine.Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
        }
    }
#endif
}
