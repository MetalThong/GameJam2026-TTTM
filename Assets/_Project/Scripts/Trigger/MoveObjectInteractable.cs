using DG.Tweening;
using UnityEngine;

public sealed class MoveObjectInteractable : MonoBehaviour, IInteractable
{
    [Header("Move")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform moveTarget;
    [SerializeField] private Vector3 moveOffset;
    [SerializeField, Min(0f)] private float moveStartDelay = 0.5f;
    [SerializeField, Min(0f)] private float duration = 0.5f;
    [SerializeField] private Ease ease = Ease.OutQuad;
    [SerializeField] private bool preserveCurrentZ = true;
    [SerializeField] private bool interactOnce = true;

    [Header("Player Pull Animation")]
    [SerializeField] private bool playPullAnimation = true;
    [SerializeField] private Movement playerMovement;
    [SerializeField] private string pullAnimationTrigger = "IsPull";
    [SerializeField] private bool requireCatFormForPullAnimation = true;

    [Header("Flag")]
    [SerializeField] private string completionFlagId;

    private bool _isMoving;
    private bool _isDone;
    private Tween _moveTween;

    private void Awake()
    {
        if (target == null)
        {
            target = transform;
        }

        RefreshDoneStateFromFlag();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        RefreshDoneStateFromFlag();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);
    }

    private void OnDestroy()
    {
        _moveTween?.Kill();
    }

    public bool TryInteract()
    {
        if (_isMoving || (interactOnce && _isDone) || target == null)
        {
            return false;
        }

        PlayPullAnimation();
        MoveTarget();
        return true;
    }

    private void MoveTarget()
    {
        Vector3 destination = moveTarget != null
            ? moveTarget.position
            : target.position + moveOffset;

        if (preserveCurrentZ)
        {
            destination.z = target.position.z;
        }

        _isMoving = true;
        _moveTween?.Kill();

        Sequence sequence = DOTween.Sequence();
        if (moveStartDelay > 0f)
        {
            sequence.AppendInterval(moveStartDelay);
        }

        if (duration <= 0f)
        {
            sequence.AppendCallback(() => target.position = destination);
        }
        else
        {
            sequence.Append(target.DOMove(destination, duration).SetEase(ease));
        }

        _moveTween = sequence
            .OnComplete(CompleteMove);
    }

    private void CompleteMove()
    {
        _moveTween = null;
        _isMoving = false;
        _isDone = true;

        if (!string.IsNullOrWhiteSpace(completionFlagId) && FlagManager.Instance != null)
        {
            FlagManager.Instance.SetFlag(completionFlagId, true);
        }
    }

    private void PlayPullAnimation()
    {
        if (!playPullAnimation || string.IsNullOrWhiteSpace(pullAnimationTrigger))
        {
            return;
        }

        Movement movement = ResolvePlayerMovement();
        movement?.TryPlayAnimationTrigger(pullAnimationTrigger, requireCatFormForPullAnimation);
    }

    private Movement ResolvePlayerMovement()
    {
        if (playerMovement == null)
        {
            playerMovement = Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
        }

        return playerMovement;
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        RefreshDoneStateFromFlag();
    }

    private void RefreshDoneStateFromFlag()
    {
        if (string.IsNullOrWhiteSpace(completionFlagId) || FlagManager.Instance == null)
        {
            return;
        }

        _isDone = FlagManager.Instance.HasFlag(completionFlagId);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (target == null)
        {
            target = transform;
        }
    }
#endif
}
