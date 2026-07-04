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

    [Header("Flags")]
    [SerializeField] private string completionFlagId = "dropped_box";

    [Header("Visibility")]
    [SerializeField] private GameObject[] hideOnStart;
    [SerializeField] private GameObject[] showOnComplete;
    [SerializeField] private bool deactivateOnComplete;

    private bool _isPlaying;
    private Tween _moveTween;

    private void OnDestroy()
    {
        _moveTween?.Kill();
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
        if (cutSceneObject != null)
        {
            cutSceneObject.SetActive(true);
        }

        CutSceneDialoguePlayer player = ResolveCutScenePlayer();
        if (player == null)
        {
            return;
        }

        if (!player.gameObject.activeSelf)
        {
            player.gameObject.SetActive(true);
        }

        await player.PlayAsync(cancellationToken);

        if (hideCutSceneObjectOnComplete && cutSceneObject != null && !cancellationToken.IsCancellationRequested)
        {
            cutSceneObject.SetActive(false);
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

    private void SetCompletionFlag()
    {
        if (string.IsNullOrWhiteSpace(completionFlagId) || FlagManager.Instance == null)
        {
            return;
        }

        FlagManager.Instance.SetFlag(completionFlagId, true);
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
    }
#endif
}
