using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public sealed class LivingRoomChatTransformSequence : MonoBehaviour
{
    [Header("Flags")]
    [SerializeField] private string startFlag = "chat_completed_go_living_room";
    [SerializeField] private string completionFlag = "post_chat_go_living_room_complete";
    [SerializeField] private string missionAssignedFlag = "mission_provoke_owner";

    [Header("Form Change")]
    [SerializeField] private Movement playerMovement;
    [SerializeField] private bool changeForm = true;
    [SerializeField] private MovementForm targetForm = MovementForm.Cat;
    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField, Min(0f)] private float formFadeOutDuration = 0.12f;
    [SerializeField, Min(0f)] private float formHoldDuration = 0.08f;
    [SerializeField, Min(0f)] private float formFadeInDuration = 0.18f;
    [SerializeField] private Vector3 formPunchScale = new(0.12f, 0.12f, 0f);
    [SerializeField, Min(0f)] private float formPunchDuration = 0.28f;

    [Header("Form VFX")]
    [SerializeField] private bool playBuiltInVfx = true;
    [SerializeField] private Color vfxColor = new(1f, 0.84f, 0.35f, 0.85f);
    [SerializeField, Min(1)] private int vfxRingCount = 2;
    [SerializeField, Min(0.01f)] private float vfxDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float vfxRadius = 0.45f;
    [SerializeField, Min(0.001f)] private float vfxLineWidth = 0.035f;
    [SerializeField] private Vector3 vfxLocalOffset = new(0f, 0.05f, 0f);
    [SerializeField] private int vfxSortingOrderOffset = 12;

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO dialogue;
    [SerializeField, Min(0f)] private float dialogueDelay = 0.2f;

    private const int VfxPointCount = 48;

    private CancellationTokenSource _sequenceCts;
    private Tween _formTween;
    private bool _isPlaying;
    private bool _hasLockedGameState;
    private GameState _previousGameState = GameState.Playing;

    private void OnEnable()
    {
        _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        TryStartFromCurrentFlagsAsync(_sequenceCts.Token).Forget();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        CancelSequence();
    }

    private void OnDestroy()
    {
        CancelSequence();
    }

    private void OnFlagChanged(FlagChangedEvent eventData)
    {
        if (eventData.Value && eventData.FlagId == startFlag)
        {
            TryStartSequence();
        }
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        TryStartFromCurrentFlagsAsync(_sequenceCts.Token).Forget();
    }

    private async UniTaskVoid TryStartFromCurrentFlagsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            TryStartSequence();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void TryStartSequence()
    {
        if (_isPlaying || !ShouldRunSequence())
        {
            return;
        }

        PlaySequenceAsync(_sequenceCts.Token).Forget();
    }

    private bool ShouldRunSequence()
    {
        if (FlagManager.Instance == null || string.IsNullOrWhiteSpace(startFlag))
        {
            return false;
        }

        if (!FlagManager.Instance.HasFlag(startFlag))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(completionFlag) || !FlagManager.Instance.HasFlag(completionFlag);
    }

    private async UniTaskVoid PlaySequenceAsync(CancellationToken cancellationToken)
    {
        _isPlaying = true;

        try
        {
            LockGameState();
            if (dialogueDelay > 0f)
            {
                await DelayWithMovementLockAsync(dialogueDelay, cancellationToken);
            }

            EnsureGameStateLocked();
            await PlayDialogueAsync(cancellationToken);
            EnsureGameStateLocked();
            await PlayFormChangeAsync(cancellationToken);
            SetFlag(missionAssignedFlag);
            SetFlag(completionFlag);
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

    private async UniTask PlayFormChangeAsync(CancellationToken cancellationToken)
    {
        ResolvePlayerReferences();

        if (playerMovement == null)
        {
            return;
        }

        Transform playerTransform = playerMovement.transform;
        Vector3 originalScale = playerTransform.localScale;
        float originalAlpha = playerSprite != null ? playerSprite.color.a : 1f;

        PlayFormVfx(playerTransform);

        if (!changeForm)
        {
            return;
        }

        try
        {
            if (playerSprite != null && formFadeOutDuration > 0f)
            {
                await FadeSpriteAsync(playerSprite, 0f, formFadeOutDuration, cancellationToken);
            }

            playerMovement.SetForm(targetForm);

            if (formHoldDuration > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(formHoldDuration), cancellationToken: cancellationToken);
            }

            await RevealPlayerAsync(playerSprite, playerTransform, originalAlpha, cancellationToken);
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                SetSpriteAlpha(playerSprite, originalAlpha);
                playerTransform.localScale = originalScale;
            }
        }
    }

    private async UniTask RevealPlayerAsync(
        SpriteRenderer spriteRenderer,
        Transform playerTransform,
        float targetAlpha,
        CancellationToken cancellationToken)
    {
        _formTween?.Kill();
        Sequence sequence = DOTween.Sequence()
            .SetTarget(gameObject)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        bool hasTween = false;
        if (spriteRenderer != null)
        {
            sequence.Join(spriteRenderer.DOFade(targetAlpha, formFadeInDuration));
            hasTween = true;
        }

        if (playerTransform != null && formPunchDuration > 0f && formPunchScale.sqrMagnitude > 0.0001f)
        {
            DOTween.Kill(playerTransform);
            sequence.Join(playerTransform.DOPunchScale(formPunchScale, formPunchDuration, 8, 0.65f));
            hasTween = true;
        }

        if (!hasTween || !sequence.IsActive() || sequence.Duration() <= 0f)
        {
            SetSpriteAlpha(spriteRenderer, targetAlpha);
            sequence.Kill();
            return;
        }

        _formTween = sequence;
        using (cancellationToken.Register(() => sequence.Kill()))
        {
            await sequence.AsyncWaitForCompletion();
        }

        if (_formTween == sequence)
        {
            _formTween = null;
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async UniTask FadeSpriteAsync(
        SpriteRenderer spriteRenderer,
        float targetAlpha,
        float duration,
        CancellationToken cancellationToken)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (duration <= 0f)
        {
            SetSpriteAlpha(spriteRenderer, targetAlpha);
            return;
        }

        DOTween.Kill(spriteRenderer);
        Tween tween = spriteRenderer.DOFade(targetAlpha, duration)
            .SetTarget(spriteRenderer)
            .SetLink(spriteRenderer.gameObject, LinkBehaviour.KillOnDestroy);

        _formTween = tween;
        using (cancellationToken.Register(() => tween.Kill()))
        {
            await tween.AsyncWaitForCompletion();
        }

        if (_formTween == tween)
        {
            _formTween = null;
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void PlayFormVfx(Transform playerTransform)
    {
        if (!playBuiltInVfx || playerTransform == null || vfxRingCount <= 0)
        {
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");
        for (int i = 0; i < vfxRingCount; i++)
        {
            CreateVfxRing(playerTransform, shader, i);
        }
    }

    private void CreateVfxRing(Transform playerTransform, Shader shader, int ringIndex)
    {
        GameObject ring = new($"FormChangeVfxRing_{ringIndex}");
        ring.transform.SetParent(playerTransform, false);
        ring.transform.localPosition = vfxLocalOffset;
        ring.transform.localScale = Vector3.one * 0.2f;

        LineRenderer line = ring.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = VfxPointCount;
        line.widthMultiplier = vfxLineWidth;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;

        if (playerSprite != null)
        {
            line.sortingLayerID = playerSprite.sortingLayerID;
            line.sortingOrder = playerSprite.sortingOrder + vfxSortingOrderOffset + ringIndex;
        }

        Material material = null;
        if (shader != null)
        {
            material = new Material(shader);
            line.sharedMaterial = material;
        }

        Color ringColor = vfxColor;
        line.startColor = ringColor;
        line.endColor = ringColor;
        if (material != null)
        {
            material.color = ringColor;
        }

        for (int i = 0; i < VfxPointCount; i++)
        {
            float angle = i / (float)VfxPointCount * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * vfxRadius, Mathf.Sin(angle) * vfxRadius, 0f));
        }

        float delay = ringIndex * 0.08f;
        Sequence sequence = DOTween.Sequence()
            .SetTarget(ring)
            .SetLink(ring, LinkBehaviour.KillOnDestroy);

        sequence.AppendInterval(delay);
        sequence.Append(ring.transform.DOScale(1.55f + ringIndex * 0.25f, vfxDuration).SetEase(Ease.OutQuad));
        sequence.Join(DOTween.To(
            () => ringColor.a,
            alpha =>
            {
                ringColor.a = alpha;
                line.startColor = ringColor;
                line.endColor = ringColor;
                if (material != null)
                {
                    material.color = ringColor;
                }
            },
            0f,
            vfxDuration
        ).SetEase(Ease.OutQuad));
        sequence.OnKill(() =>
        {
            if (ring != null)
            {
                Destroy(ring);
            }

            if (material != null)
            {
                Destroy(material);
            }
        });
    }

    private async UniTask PlayDialogueAsync(CancellationToken cancellationToken)
    {
        if (dialogue == null)
        {
            return;
        }

        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            Debug.LogWarning("LivingRoomChatTransformSequence: dialogue assigned but no DialogueManager was found.", this);
            return;
        }

        await manager.PlayDialogueAsync(dialogue, cancellationToken);
    }

    private void LockGameState()
    {
        if (_hasLockedGameState || GameManager.Instance == null)
        {
            return;
        }

        _previousGameState = GameManager.Instance.CurrentState;
        _hasLockedGameState = true;

        if (_previousGameState != GameState.OnDialog)
        {
            GameManager.Instance.SetState(GameState.OnDialog);
        }
    }

    private async UniTask DelayWithMovementLockAsync(float duration, CancellationToken cancellationToken)
    {
        float remaining = Mathf.Max(0f, duration);
        while (remaining > 0f)
        {
            EnsureGameStateLocked();
            float frameStart = Time.time;
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            remaining -= Mathf.Max(0f, Time.time - frameStart);
        }

        EnsureGameStateLocked();
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

    private void ResolvePlayerReferences()
    {
        if (playerMovement == null)
        {
            playerMovement = UnityEngine.Object.FindFirstObjectByType<Movement>(FindObjectsInactive.Exclude);
        }

        if (playerSprite == null && playerMovement != null)
        {
            playerSprite = playerMovement.GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private DialogueManager ResolveDialogueManager()
    {
        if (dialogueManager == null)
        {
            dialogueManager = UnityEngine.Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }

        return dialogueManager;
    }

    private static void SetSpriteAlpha(SpriteRenderer spriteRenderer, float alpha)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
    }

    private static void SetFlag(string flagId)
    {
        if (!string.IsNullOrWhiteSpace(flagId) && FlagManager.Instance != null)
        {
            FlagManager.Instance.SetFlag(flagId, true);
        }
    }

    private void CancelSequence()
    {
        if (_sequenceCts != null)
        {
            _sequenceCts.Cancel();
            _sequenceCts.Dispose();
            _sequenceCts = null;
        }

        _formTween?.Kill();
        _formTween = null;
        RestoreGameStateIfNeeded();
    }
}
