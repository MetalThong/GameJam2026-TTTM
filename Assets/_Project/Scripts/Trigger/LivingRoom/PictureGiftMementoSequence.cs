using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PictureGiftMementoSequence : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private SceneId sceneId = SceneId.LivingRoomPart4;
    [SerializeField] private string carryId = "gift";
    [SerializeField] private string completionFlagId = "owner_memento_found";
    [SerializeField] private string nextMissionFlagId = "mission_go_to_bedroom";
    [SerializeField, Min(0f)] private float startDelay = 0.2f;

    [Header("BellCat")]
    [SerializeField] private GameObject bellCatPrefab;
    [SerializeField] private Vector3 bellCatGroundOffset;
    [SerializeField] private Vector3 bellCatSpawnOffset = new(0f, 1.2f, 0f);
    [SerializeField] private Vector3 bellCatSpawnScale = Vector3.one;
    [SerializeField, Min(0f)] private float bellCatFallDuration = 0.45f;
    [SerializeField] private Ease bellCatFallEase = Ease.OutBounce;

    [Header("Owner Spawn")]
    [SerializeField] private GameObject ownerPrefab;
    [SerializeField] private string ownerResourcePath = "Main/thang chu di";
    [SerializeField] private bool spawnOwnerAtDropPosition = true;
    [SerializeField] private Vector3 ownerDropSpawnOffset = new(1.2f, 0f, 0f);
    [SerializeField] private Transform ownerSpawnPoint;
    [SerializeField] private Vector3 ownerSpawnOffset;
    [SerializeField] private Vector3 ownerSpawnScale = Vector3.one;
    [SerializeField] private bool flipOwnerOnSpawn;
    [SerializeField] private bool disableOwnerAnimatorOnSpawn = true;
    [SerializeField, Min(0f)] private float ownerSpawnFadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float ownerDespawnFadeDuration = 0.35f;

    [Header("Dropped Gift")]
    [SerializeField] private bool hideDroppedGiftOnBellCatSpawn = true;
    [SerializeField, Min(0f)] private float droppedGiftFadeDuration = 0.2f;

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO ownerFoundDialogue;

    private bool _isPlaying;
    private GameObject _spawnedOwner;
    private GameState _previousGameState = GameState.Playing;
    private bool _hasLockedGameState;

    private void OnEnable()
    {
        CarryManager.CarriedObjectDropped += OnCarriedObjectDropped;
    }

    private void OnDisable()
    {
        CarryManager.CarriedObjectDropped -= OnCarriedObjectDropped;
    }

    private void OnDestroy()
    {
        RestoreGameStateIfNeeded();
        DespawnOwner();
    }

    private void OnCarriedObjectDropped(CarryManager.CarryDropInfo dropInfo)
    {
        if (_isPlaying || IsCompleted() || !IsMatchingDrop(dropInfo))
        {
            return;
        }

        PlaySequenceAsync(dropInfo, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private bool IsMatchingDrop(CarryManager.CarryDropInfo dropInfo)
    {
        return dropInfo.DropScene.IsValid()
            && string.Equals(dropInfo.DropScene.name, sceneId.ToString(), StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(carryId)
            && string.Equals(dropInfo.CarryId, carryId, StringComparison.Ordinal);
    }

    private async UniTaskVoid PlaySequenceAsync(
        CarryManager.CarryDropInfo dropInfo,
        CancellationToken cancellationToken)
    {
        _isPlaying = true;

        try
        {
            LockGameState();

            if (startDelay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: cancellationToken);
            }

            GameObject bellCat = SpawnBellCat(dropInfo.DropPosition);
            await UniTask.WhenAll(
                DropBellCatAsync(bellCat, dropInfo.DropPosition, cancellationToken),
                HideDroppedGiftAsync(dropInfo.DroppedObject, cancellationToken));

            _spawnedOwner = SpawnOwner(dropInfo.DropPosition);
            await FadeOwnerAsync(_spawnedOwner, 1f, ownerSpawnFadeDuration, cancellationToken);
            await PlayDialogueIfAssignedAsync(cancellationToken);
            EnsureGameStateLocked();

            await FadeOwnerAsync(_spawnedOwner, 0f, ownerDespawnFadeDuration, cancellationToken);
            DespawnOwner();
            SetCompletionFlags();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            RestoreGameStateIfNeeded();
            _isPlaying = false;
        }
    }

    private GameObject SpawnBellCat(Vector3 dropPosition)
    {
        if (bellCatPrefab == null)
        {
            Debug.LogWarning("PictureGiftMementoSequence: BellCat prefab is not assigned.", this);
            return null;
        }

        Vector3 groundPosition = dropPosition + bellCatGroundOffset;
        GameObject bellCat = Instantiate(bellCatPrefab, groundPosition + bellCatSpawnOffset, Quaternion.identity);
        bellCat.name = bellCatPrefab.name;
        bellCat.SetActive(true);

        if (IsValidScale(bellCatSpawnScale))
        {
            bellCat.transform.localScale = bellCatSpawnScale;
        }

        return bellCat;
    }

    private async UniTask DropBellCatAsync(
        GameObject bellCat,
        Vector3 dropPosition,
        CancellationToken cancellationToken)
    {
        if (bellCat == null)
        {
            return;
        }

        Vector3 targetPosition = dropPosition + bellCatGroundOffset;
        if (bellCatFallDuration <= 0f)
        {
            bellCat.transform.position = targetPosition;
            return;
        }

        Tween fallTween = bellCat.transform.DOMove(targetPosition, bellCatFallDuration).SetEase(bellCatFallEase);
        using (cancellationToken.Register(() => fallTween.Kill()))
        {
            await fallTween.AsyncWaitForCompletion();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private GameObject SpawnOwner(Vector3 dropPosition)
    {
        GameObject prefab = ownerPrefab;
        if (prefab == null && !string.IsNullOrWhiteSpace(ownerResourcePath))
        {
            prefab = Resources.Load<GameObject>(ownerResourcePath);
        }

        if (prefab == null)
        {
            Debug.LogWarning($"PictureGiftMementoSequence: owner prefab could not be resolved from Resources path '{ownerResourcePath}'.", this);
            return null;
        }

        Vector3 spawnPosition = ResolveOwnerSpawnPosition(dropPosition) + ownerSpawnOffset;
        GameObject owner = Instantiate(prefab, spawnPosition, Quaternion.identity);
        owner.name = prefab.name;
        owner.SetActive(true);
        owner.transform.localScale = ResolveOwnerSpawnScale(prefab);
        SetOwnerAnimatorsEnabled(owner, !disableOwnerAnimatorOnSpawn);
        SetOwnerAlpha(owner, 0f);
        return owner;
    }

    private Vector3 ResolveOwnerSpawnPosition(Vector3 dropPosition)
    {
        if (spawnOwnerAtDropPosition)
        {
            return dropPosition + ownerDropSpawnOffset;
        }

        return ownerSpawnPoint != null ? ownerSpawnPoint.position : transform.position;
    }

    private Vector3 ResolveOwnerSpawnScale(GameObject prefab)
    {
        Vector3 scale = IsValidScale(ownerSpawnScale) ? ownerSpawnScale : prefab.transform.localScale;
        if (flipOwnerOnSpawn)
        {
            float xScale = Mathf.Approximately(scale.x, 0f) ? 1f : Mathf.Abs(scale.x);
            scale.x = -xScale;
        }

        return scale;
    }

    private async UniTask HideDroppedGiftAsync(GameObject droppedGift, CancellationToken cancellationToken)
    {
        if (!hideDroppedGiftOnBellCatSpawn || droppedGift == null)
        {
            return;
        }

        PrepareDroppedGiftForHide(droppedGift);
        await FadeDroppedGiftAsync(droppedGift, 0f, droppedGiftFadeDuration, cancellationToken);

        if (droppedGift != null)
        {
            Destroy(droppedGift);
        }
    }

    private static void PrepareDroppedGiftForHide(GameObject droppedGift)
    {
        if (droppedGift == null)
        {
            return;
        }

        foreach (Collider2D collider in droppedGift.GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody2D rigidbody in droppedGift.GetComponentsInChildren<Rigidbody2D>(true))
        {
            rigidbody.simulated = false;
        }

        foreach (MonoBehaviour behaviour in droppedGift.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is IInteractable)
            {
                behaviour.enabled = false;
            }
        }
    }

    private async UniTask FadeDroppedGiftAsync(
        GameObject droppedGift,
        float targetAlpha,
        float duration,
        CancellationToken cancellationToken)
    {
        if (droppedGift == null)
        {
            return;
        }

        SpriteRenderer[] renderers = droppedGift.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length <= 0)
        {
            droppedGift.SetActive(false);
            return;
        }

        if (duration <= 0f)
        {
            SetSpriteRenderersAlpha(renderers, targetAlpha);
            return;
        }

        Sequence sequence = DOTween.Sequence();
        bool hasTween = false;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            DOTween.Kill(renderer);
            sequence.Join(renderer.DOFade(targetAlpha, duration));
            hasTween = true;
        }

        if (!hasTween)
        {
            sequence.Kill();
            return;
        }

        using (cancellationToken.Register(() => sequence.Kill()))
        {
            await sequence.AsyncWaitForCompletion();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async UniTask PlayDialogueIfAssignedAsync(CancellationToken cancellationToken)
    {
        if (ownerFoundDialogue == null)
        {
            return;
        }

        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            Debug.LogWarning("PictureGiftMementoSequence: dialogue assigned but no DialogueManager was found.", this);
            return;
        }

        await manager.PlayDialogueAsync(ownerFoundDialogue, cancellationToken);
    }

    private DialogueManager ResolveDialogueManager()
    {
        if (dialogueManager == null)
        {
            dialogueManager = UnityEngine.Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }

        return dialogueManager;
    }

    private async UniTask FadeOwnerAsync(GameObject owner, float targetAlpha, float duration, CancellationToken cancellationToken)
    {
        if (owner == null)
        {
            return;
        }

        SpriteRenderer[] renderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length <= 0)
        {
            return;
        }

        if (duration <= 0f)
        {
            SetOwnerAlpha(owner, targetAlpha);
            return;
        }

        Sequence sequence = DOTween.Sequence();
        bool hasTween = false;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            DOTween.Kill(renderer);
            sequence.Join(renderer.DOFade(targetAlpha, duration));
            hasTween = true;
        }

        if (!hasTween)
        {
            sequence.Kill();
            return;
        }

        using (cancellationToken.Register(() => sequence.Kill()))
        {
            await sequence.AsyncWaitForCompletion();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void DespawnOwner()
    {
        if (_spawnedOwner == null)
        {
            return;
        }

        Destroy(_spawnedOwner);
        _spawnedOwner = null;
    }

    private void LockGameState()
    {
        if (_hasLockedGameState || GameManager.Instance == null)
        {
            return;
        }

        _previousGameState = GameManager.Instance.CurrentState;
        _hasLockedGameState = true;
        GameManager.Instance.SetState(GameState.OnDialog);
    }

    private void EnsureGameStateLocked()
    {
        if (!_hasLockedGameState || GameManager.Instance == null)
        {
            return;
        }

        if (GameManager.Instance.CurrentState != GameState.OnDialog)
        {
            GameManager.Instance.SetState(GameState.OnDialog);
        }
    }

    private void RestoreGameStateIfNeeded()
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

    private void SetCompletionFlags()
    {
        if (FlagManager.Instance == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(completionFlagId))
        {
            FlagManager.Instance.SetFlag(completionFlagId, true);
        }

        if (!string.IsNullOrWhiteSpace(nextMissionFlagId))
        {
            FlagManager.Instance.SetFlag(nextMissionFlagId, true);
        }
    }

    private bool IsCompleted()
    {
        return !string.IsNullOrWhiteSpace(completionFlagId)
            && FlagManager.Instance != null
            && FlagManager.Instance.HasFlag(completionFlagId);
    }

    private static void SetOwnerAlpha(GameObject owner, float alpha)
    {
        if (owner == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in owner.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    private static void SetSpriteRenderersAlpha(SpriteRenderer[] renderers, float alpha)
    {
        if (renderers == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    private static void SetOwnerAnimatorsEnabled(GameObject owner, bool enabled)
    {
        if (owner == null)
        {
            return;
        }

        foreach (Animator animator in owner.GetComponentsInChildren<Animator>(true))
        {
            if (animator != null)
            {
                animator.enabled = enabled;
            }
        }
    }

    private static bool IsValidScale(Vector3 scale)
    {
        return !Mathf.Approximately(scale.x, 0f)
            && !Mathf.Approximately(scale.y, 0f)
            && !Mathf.Approximately(scale.z, 0f);
    }
}
