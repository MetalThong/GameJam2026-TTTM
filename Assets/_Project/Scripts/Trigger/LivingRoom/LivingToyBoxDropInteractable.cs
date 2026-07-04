using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LivingToyBoxDropInteractable : StoryInteractable
{
    [Header("Toy Box Move")]
    [SerializeField] private Transform toyBoxTransform;
    [SerializeField] private Transform moveTarget;
    [SerializeField] private Vector3 moveOffset;
    [SerializeField, Min(0f)] private float moveDuration = 0.5f;
    [SerializeField] private Ease moveEase = Ease.OutQuad;
    [SerializeField] private bool preserveCurrentZ = true;

    [Header("SFX")]
    [SerializeField] private string sfxId = "drop_box";
    [SerializeField] private bool playSpatialSfx;
    [SerializeField] private bool playSfxAfterMove = true;

    [Header("Cut Scene")]
    [SerializeField] private GameObject cutSceneObject;
    [SerializeField] private CutSceneDialoguePlayer cutScenePlayer;
    [SerializeField] private bool hideCutSceneObjectOnComplete = true;
    [SerializeField, Min(0f)] private float cutSceneFadeInDuration = 0.18f;
    [SerializeField, Min(0f)] private float cutSceneHoldAfterPlayback = 0.2f;
    [SerializeField, Min(0f)] private float cutSceneFadeOutDuration = 0.28f;
    [SerializeField] private Ease cutSceneFadeEase = Ease.OutQuad;

    [Header("Post Dialogue Form")]
    [SerializeField] private Movement playerMovement;
    [SerializeField] private bool changePlayerFormAfterDialogue = true;
    [SerializeField] private MovementForm postDialogueForm = MovementForm.Ghost;

    [Header("Flags")]
    [SerializeField] private string completionFlagId = "dropped_box";

    [Header("Visibility")]
    [SerializeField] private GameObject[] hideOnStart;
    [SerializeField] private GameObject[] hideOnComplete;
    [SerializeField] private bool hideToyBoxOnComplete = true;
    [SerializeField] private GameObject[] showOnComplete;
    [SerializeField] private bool deactivateOnComplete;

    private bool _isPlaying;
    private Tween _moveTween;
    private Tween _cutSceneFadeTween;

    private void OnDestroy()
    {
        _moveTween?.Kill();
        _cutSceneFadeTween?.Kill();
    }

    protected override bool CanInteract()
    {
        return !_isPlaying;
    }

    protected override void OnInteractSucceeded()
    {
        if (_isPlaying)
        {
            return;
        }

        PlaySequenceAsync().Forget();
    }

    private async UniTaskVoid PlaySequenceAsync()
    {
        _isPlaying = true;
        CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();

        try
        {
            SetTargetsActive(hideOnStart, false);

            if (!playSfxAfterMove)
            {
                PlaySfx();
            }

            await MoveToyBoxAsync(destroyToken);

            if (playSfxAfterMove)
            {
                PlaySfx();
            }

            await PlayCutSceneAsync(destroyToken);

            HideCompletionTargets();
            ExecuteAction();
            SetCompletionFlag();
            SetTargetsActive(showOnComplete, true);

            if (deactivateOnComplete)
            {
                gameObject.SetActive(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _moveTween = null;
            _isPlaying = false;
        }
    }

    private async UniTask MoveToyBoxAsync(CancellationToken cancellationToken)
    {
        if (toyBoxTransform == null)
        {
            Debug.LogWarning("LivingToyBoxDropInteractable: toyBoxTransform is not assigned.", this);
            return;
        }

        Vector3 targetPosition = moveTarget != null
            ? moveTarget.position
            : toyBoxTransform.position + moveOffset;

        if (preserveCurrentZ)
        {
            targetPosition.z = toyBoxTransform.position.z;
        }

        if (moveDuration <= 0f)
        {
            toyBoxTransform.position = targetPosition;
            return;
        }

        _moveTween?.Kill();
        _moveTween = toyBoxTransform.DOMove(targetPosition, moveDuration).SetEase(moveEase);

        using (cancellationToken.Register(() => _moveTween?.Kill()))
        {
            await _moveTween.AsyncWaitForCompletion();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void PlaySfx()
    {
        if (AudioManager.Instance == null || string.IsNullOrWhiteSpace(sfxId))
        {
            return;
        }

        if (playSpatialSfx)
        {
            Vector3 position = toyBoxTransform != null ? toyBoxTransform.position : transform.position;
            AudioManager.Instance.PlaySfx(sfxId, position);
            return;
        }

        AudioManager.Instance.PlaySfx(sfxId);
    }

    private async UniTask PlayCutSceneAsync(CancellationToken cancellationToken)
    {
        CutSceneDialoguePlayer player = ResolveCutScenePlayer();
        if (player == null)
        {
            return;
        }

        SpriteRenderer[] cutSceneRenderers = cutSceneObject != null
            ? cutSceneObject.GetComponentsInChildren<SpriteRenderer>(true)
            : player.GetComponentsInChildren<SpriteRenderer>(true);
        float[] originalAlphas = CaptureRendererAlphas(cutSceneRenderers);

        if (cutSceneObject != null)
        {
            SetRendererAlphas(cutSceneRenderers, 0f);
            cutSceneObject.SetActive(true);
            player.PrepareForManualPlayback();
            await FadeRenderersAsync(cutSceneRenderers, originalAlphas, cutSceneFadeInDuration, cancellationToken);
        }

        if (!player.gameObject.activeSelf)
        {
            player.gameObject.SetActive(true);
        }

        await player.PlayAsync(cancellationToken);
        ChangePlayerFormAfterDialogue();

        if (cutSceneHoldAfterPlayback > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(cutSceneHoldAfterPlayback), cancellationToken: cancellationToken);
        }

        if (hideCutSceneObjectOnComplete && cutSceneObject != null && !cancellationToken.IsCancellationRequested)
        {
            await FadeRenderersToAlphaAsync(cutSceneRenderers, 0f, cutSceneFadeOutDuration, cancellationToken);
            cutSceneObject.SetActive(false);
            RestoreRendererAlphas(cutSceneRenderers, originalAlphas);
        }
    }

    private async UniTask FadeRenderersToAlphaAsync(
        SpriteRenderer[] renderers,
        float alpha,
        float duration,
        CancellationToken cancellationToken)
    {
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        float[] targetAlphas = new float[renderers.Length];
        for (int i = 0; i < targetAlphas.Length; i++)
        {
            targetAlphas[i] = alpha;
        }

        await FadeRenderersAsync(renderers, targetAlphas, duration, cancellationToken);
    }

    private async UniTask FadeRenderersAsync(
        SpriteRenderer[] renderers,
        float[] targetAlphas,
        float duration,
        CancellationToken cancellationToken)
    {
        if (renderers == null || renderers.Length == 0 || targetAlphas == null)
        {
            return;
        }

        if (duration <= 0f)
        {
            RestoreRendererAlphas(renderers, targetAlphas);
            return;
        }

        _cutSceneFadeTween?.Kill();
        Sequence sequence = DOTween.Sequence();
        bool hasTween = false;

        for (int i = 0; i < renderers.Length && i < targetAlphas.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            sequence.Join(renderer.DOFade(targetAlphas[i], duration).SetEase(cutSceneFadeEase));
            hasTween = true;
        }

        if (!hasTween)
        {
            sequence.Kill();
            return;
        }

        _cutSceneFadeTween = sequence;
        using (cancellationToken.Register(() => sequence.Kill()))
        {
            await sequence.AsyncWaitForCompletion();
        }

        if (_cutSceneFadeTween == sequence)
        {
            _cutSceneFadeTween = null;
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static float[] CaptureRendererAlphas(SpriteRenderer[] renderers)
    {
        if (renderers == null)
        {
            return Array.Empty<float>();
        }

        float[] alphas = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            alphas[i] = renderers[i] != null ? renderers[i].color.a : 1f;
        }

        return alphas;
    }

    private static void SetRendererAlphas(SpriteRenderer[] renderers, float alpha)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    private static void RestoreRendererAlphas(SpriteRenderer[] renderers, float[] alphas)
    {
        if (renderers == null || alphas == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length && i < alphas.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = alphas[i];
            renderer.color = color;
        }
    }

    private CutSceneDialoguePlayer ResolveCutScenePlayer()
    {
        if (cutScenePlayer != null)
        {
            return cutScenePlayer;
        }

        if (cutSceneObject != null)
        {
            cutScenePlayer = cutSceneObject.GetComponentInChildren<CutSceneDialoguePlayer>(true);
        }

        if (cutScenePlayer == null)
        {
            cutScenePlayer = GetComponentInChildren<CutSceneDialoguePlayer>(true);
        }

        return cutScenePlayer;
    }

    private void ChangePlayerFormAfterDialogue()
    {
        if (!changePlayerFormAfterDialogue)
        {
            return;
        }

        Movement movement = ResolvePlayerMovement();
        if (movement != null)
        {
            movement.SetForm(postDialogueForm);
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

    private void SetCompletionFlag()
    {
        if (string.IsNullOrWhiteSpace(completionFlagId) || FlagManager.Instance == null)
        {
            return;
        }

        FlagManager.Instance.SetFlag(completionFlagId, true);
    }

    private void HideCompletionTargets()
    {
        SetTargetsActive(hideOnComplete, false);

        if (hideToyBoxOnComplete && toyBoxTransform != null)
        {
            toyBoxTransform.gameObject.SetActive(false);
        }
    }

    private static void SetTargetsActive(GameObject[] targets, bool active)
    {
        if (targets == null)
        {
            return;
        }

        foreach (GameObject target in targets)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (cutScenePlayer == null && cutSceneObject != null)
        {
            cutScenePlayer = cutSceneObject.GetComponentInChildren<CutSceneDialoguePlayer>(true);
        }

        if (playerMovement == null)
        {
            playerMovement = UnityEngine.Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
        }
    }
#endif
}
