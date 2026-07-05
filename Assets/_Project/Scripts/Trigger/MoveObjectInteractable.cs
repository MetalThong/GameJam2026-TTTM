using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogueAfterMove;
    [SerializeField] private bool waitForCurrentDialogue = true;

    [Header("Post Move Spawn")]
    [SerializeField] private GameObject postMoveSpawnPrefab;
    [SerializeField] private bool usePostMoveSpawnPosition;
    [SerializeField] private Vector3 postMoveSpawnPosition;
    [SerializeField] private bool disablePostMoveSpawnFormObjects;
    [SerializeField] private bool disablePostMoveSpawnAnimators;
    [SerializeField] private string[] postMoveSpawnChildNamesToDisable = { "DontMissCat" };

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
        PlaySequenceAsync().Forget();
        return true;
    }

    private async UniTaskVoid PlaySequenceAsync()
    {
        _isMoving = true;
        CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();

        try
        {
            await MoveTargetAsync(destroyToken);
            SpawnPostMoveObject();
            await PlayDialogueAfterMoveAsync(destroyToken);
            CompleteMove();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _moveTween = null;
            _isMoving = false;
        }
    }

    private async UniTask MoveTargetAsync(CancellationToken cancellationToken)
    {
        Vector3 destination = moveTarget != null
            ? moveTarget.position
            : target.position + moveOffset;

        if (preserveCurrentZ)
        {
            destination.z = target.position.z;
        }

        _moveTween?.Kill();

        if (moveStartDelay > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(moveStartDelay), cancellationToken: cancellationToken);
        }

        if (duration <= 0f)
        {
            target.position = destination;
        }
        else
        {
            _moveTween = target.DOMove(destination, duration).SetEase(ease);

            using (cancellationToken.Register(() => _moveTween?.Kill()))
            {
                await _moveTween.AsyncWaitForCompletion();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void SpawnPostMoveObject()
    {
        if (postMoveSpawnPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = usePostMoveSpawnPosition ? postMoveSpawnPosition : transform.position;
        GameObject spawnedObject = InstantiatePostMovePrefab(spawnPosition);
        if (spawnedObject == null)
        {
            return;
        }

        spawnedObject.name = postMoveSpawnPrefab.name;
        DisablePostMoveSpawnFormObjects(spawnedObject);
        DisablePostMoveSpawnAnimators(spawnedObject);
        DisableSpawnedChildrenByName(spawnedObject);
    }

    private GameObject InstantiatePostMovePrefab(Vector3 spawnPosition)
    {
        try
        {
            UnityEngine.Object prefabObject = postMoveSpawnPrefab;
            UnityEngine.Object spawned = UnityEngine.Object.Instantiate(prefabObject, spawnPosition, Quaternion.identity);

            if (spawned is GameObject spawnedGameObject)
            {
                return spawnedGameObject;
            }

            if (spawned is Component spawnedComponent)
            {
                return spawnedComponent.gameObject;
            }

            Debug.LogWarning("MoveObjectInteractable: postMoveSpawnPrefab did not instantiate as a GameObject. Check the prefab reference in the scene.", this);
            if (spawned != null)
            {
                Destroy(spawned);
            }
        }
        catch (InvalidCastException exception)
        {
            Debug.LogWarning($"MoveObjectInteractable: failed to instantiate postMoveSpawnPrefab '{postMoveSpawnPrefab.name}'. Reassign the prefab reference to the prefab root GameObject. {exception.Message}", this);
        }

        return null;
    }

    private void CompleteMove()
    {
        _isDone = true;

        if (!string.IsNullOrWhiteSpace(completionFlagId) && FlagManager.Instance != null)
        {
            FlagManager.Instance.SetFlag(completionFlagId, true);
        }
    }

    private async UniTask PlayDialogueAfterMoveAsync(CancellationToken cancellationToken)
    {
        if (dialogueAfterMove == null)
        {
            return;
        }

        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            Debug.LogWarning("MoveObjectInteractable: dialogueAfterMove is assigned but no DialogueManager was found.", this);
            return;
        }

        if (waitForCurrentDialogue)
        {
            while (manager.IsPlaying)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        await manager.PlayDialogueAsync(dialogueAfterMove, cancellationToken);
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
            playerMovement = UnityEngine.Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
        }

        return playerMovement;
    }

    private DialogueManager ResolveDialogueManager()
    {
        if (dialogueManager == null)
        {
            dialogueManager = UnityEngine.Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }

        return dialogueManager;
    }

    private void DisablePostMoveSpawnFormObjects(GameObject spawnedObject)
    {
        if (!disablePostMoveSpawnFormObjects || spawnedObject == null)
        {
            return;
        }

        PlayerFormObject[] formObjects = spawnedObject.GetComponentsInChildren<PlayerFormObject>(true);
        for (int i = 0; i < formObjects.Length; i++)
        {
            if (formObjects[i] != null)
            {
                formObjects[i].enabled = false;
            }
        }
    }

    private void DisablePostMoveSpawnAnimators(GameObject spawnedObject)
    {
        if (!disablePostMoveSpawnAnimators || spawnedObject == null)
        {
            return;
        }

        Animator[] animators = spawnedObject.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].enabled = false;
            }
        }
    }

    private void DisableSpawnedChildrenByName(GameObject spawnedObject)
    {
        if (spawnedObject == null
            || postMoveSpawnChildNamesToDisable == null
            || postMoveSpawnChildNamesToDisable.Length == 0)
        {
            return;
        }

        Transform[] children = spawnedObject.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == spawnedObject.transform)
            {
                continue;
            }

            for (int j = 0; j < postMoveSpawnChildNamesToDisable.Length; j++)
            {
                string childName = postMoveSpawnChildNamesToDisable[j];
                if (!string.IsNullOrWhiteSpace(childName) && child.name == childName)
                {
                    child.gameObject.SetActive(false);
                    break;
                }
            }
        }
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
