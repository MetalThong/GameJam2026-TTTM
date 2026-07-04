using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class KitchenDroppedBowlSequence : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private SceneId sceneId = SceneId.Kitchen;
    [SerializeField] private string carryId = "cat_bowl";
    [SerializeField] private string completionFlagId = "kitchen_bowl_taken";
    [SerializeField, Min(0f)] private float startDelay = 0.35f;

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO ownerHearDialogue;
    [SerializeField] private DialogueSO ownerFoundDialogue;
    [SerializeField] private DialogueSO catReactionDialogue;

    [Header("Owner Spawn")]
    [SerializeField] private GameObject ownerPrefab;
    [SerializeField] private string ownerResourcePath = "Main/thang chu di";
    [SerializeField] private bool spawnAtDropPosition = true;
    [SerializeField] private Vector3 ownerDropSpawnOffset;
    [SerializeField] private Transform ownerSpawnPoint;
    [SerializeField] private bool useOwnerSpawnPosition;
    [SerializeField] private Vector3 ownerSpawnPosition = new(4.85f, -2.87f, 0f);
    [SerializeField] private Vector3 ownerSpawnOffset;
    [SerializeField] private Vector3 ownerSpawnScale = Vector3.one;
    [SerializeField] private bool flipOwnerOnSpawn;
    [SerializeField] private bool disableAnimatorOnSpawn = true;
    [SerializeField, Min(0f)] private float ownerSpawnFadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float ownerDespawnFadeDuration = 0.35f;

    [Header("Dropped Bowl")]
    [SerializeField] private bool hideDroppedBowlOnOwnerSpawn = true;
    [SerializeField, Min(0f)] private float droppedBowlFadeDuration = 0.25f;

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

        if (_spawnedOwner != null)
        {
            Destroy(_spawnedOwner);
            _spawnedOwner = null;
        }
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
            if (startDelay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: cancellationToken);
            }

            LockGameState();

            _spawnedOwner = SpawnOwner(dropInfo.DropPosition);
            await UniTask.WhenAll(
                FadeOwnerAsync(_spawnedOwner, 1f, ownerSpawnFadeDuration, cancellationToken),
                HideDroppedBowlAsync(dropInfo.DroppedObject, cancellationToken));

            await PlayDialogueIfAssignedAsync(ownerHearDialogue, cancellationToken);
            EnsureGameStateLocked();
            await PlayDialogueIfAssignedAsync(ownerFoundDialogue, cancellationToken);
            EnsureGameStateLocked();

            await FadeOwnerAsync(_spawnedOwner, 0f, ownerDespawnFadeDuration, cancellationToken);
            DespawnOwner();

            await PlayDialogueIfAssignedAsync(catReactionDialogue, cancellationToken);
            SetCompletionFlag();
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

    private GameObject SpawnOwner(Vector3 dropPosition)
    {
        GameObject prefab = ownerPrefab;
        if (prefab == null && !string.IsNullOrWhiteSpace(ownerResourcePath))
        {
            prefab = Resources.Load<GameObject>(ownerResourcePath);
        }

        if (prefab == null)
        {
            Debug.LogWarning($"KitchenDroppedBowlSequence: owner prefab could not be resolved from Resources path '{ownerResourcePath}'.", this);
            return null;
        }

        Vector3 spawnPosition = ResolveOwnerSpawnPosition(dropPosition) + ownerSpawnOffset;
        GameObject owner = Instantiate(prefab, spawnPosition, Quaternion.identity);
        owner.name = prefab.name;
        owner.transform.localScale = ResolveSpawnScale(prefab);
        owner.SetActive(true);
        SetOwnerAnimatorsEnabled(owner, !disableAnimatorOnSpawn);
        SetOwnerAlpha(owner, 0f);
        return owner;
    }

    private Vector3 ResolveOwnerSpawnPosition(Vector3 dropPosition)
    {
        if (spawnAtDropPosition)
        {
            return dropPosition + ownerDropSpawnOffset;
        }

        if (useOwnerSpawnPosition)
        {
            return ownerSpawnPosition;
        }

        return ownerSpawnPoint != null ? ownerSpawnPoint.position : transform.position;
    }

    private Vector3 ResolveSpawnScale(GameObject prefab)
    {
        Vector3 scale = IsValidScale(ownerSpawnScale) ? ownerSpawnScale : prefab.transform.localScale;
        if (flipOwnerOnSpawn)
        {
            float xScale = Mathf.Approximately(scale.x, 0f) ? 1f : Mathf.Abs(scale.x);
            scale.x = -xScale;
        }

        return scale;
    }

    private async UniTask HideDroppedBowlAsync(GameObject droppedBowl, CancellationToken cancellationToken)
    {
        if (!hideDroppedBowlOnOwnerSpawn || droppedBowl == null)
        {
            return;
        }

        PrepareDroppedBowlForHide(droppedBowl);
        await FadeDroppedBowlAsync(droppedBowl, 0f, droppedBowlFadeDuration, cancellationToken);

        if (droppedBowl != null)
        {
            Destroy(droppedBowl);
        }
    }

    private static void PrepareDroppedBowlForHide(GameObject bowl)
    {
        if (bowl == null)
        {
            return;
        }

        foreach (Collider2D collider in bowl.GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody2D rigidbody in bowl.GetComponentsInChildren<Rigidbody2D>(true))
        {
            rigidbody.simulated = false;
        }

        foreach (MonoBehaviour behaviour in bowl.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is IInteractable)
            {
                behaviour.enabled = false;
            }
        }
    }

    private async UniTask FadeDroppedBowlAsync(
        GameObject bowl,
        float targetAlpha,
        float duration,
        CancellationToken cancellationToken)
    {
        if (bowl == null)
        {
            return;
        }

        SpriteRenderer[] renderers = bowl.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length <= 0)
        {
            bowl.SetActive(false);
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

    private async UniTask PlayDialogueIfAssignedAsync(DialogueSO dialogueToPlay, CancellationToken cancellationToken)
    {
        if (dialogueToPlay == null)
        {
            return;
        }

        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            Debug.LogWarning("KitchenDroppedBowlSequence: dialogue assigned but no DialogueManager was found.", this);
            return;
        }

        await manager.PlayDialogueAsync(dialogueToPlay, cancellationToken);
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

    private void SetCompletionFlag()
    {
        if (!string.IsNullOrWhiteSpace(completionFlagId) && FlagManager.Instance != null)
        {
            FlagManager.Instance.SetFlag(completionFlagId, true);
        }
    }

    private bool IsCompleted()
    {
        return !string.IsNullOrWhiteSpace(completionFlagId)
            && FlagManager.Instance != null
            && FlagManager.Instance.HasFlag(completionFlagId);
    }

    private static bool IsValidScale(Vector3 scale)
    {
        return !Mathf.Approximately(scale.x, 0f)
            && !Mathf.Approximately(scale.y, 0f)
            && !Mathf.Approximately(scale.z, 0f);
    }

}
