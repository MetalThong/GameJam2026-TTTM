using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class WashingMinigameController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform movingDish;
    [SerializeField] private Collider2D movingDishCollider;
    [SerializeField] private Collider2D checkCollider;
    [SerializeField] private Transform checkPulseTarget;
    [SerializeField] private Transform buttonPrompt;
    [SerializeField] private SpriteRenderer buttonPromptRenderer;
    [SerializeField] private Animator washingCatAnimator;
    [SerializeField] private Transform trackRoot;
    [SerializeField] private UIManager uiManager;

    [Header("Flow")]
    [SerializeField] private bool autoStartOnEnable = true;
    [SerializeField] private bool hideMinigameUntilStarted = false;
    [SerializeField] private bool lockGameStateWhilePlaying = true;
    [SerializeField, Min(0.05f)] private float tutorialTravelDuration = 3f;
    [SerializeField, Min(0.05f)] private float normalTravelDuration = 2f;
    [SerializeField, Range(0.1f, 1f)] private float speedMultiplier = 0.9f;
    [SerializeField, Min(0.05f)] private float minimumTravelDuration = 0.75f;
    [SerializeField, Min(0f)] private float failDistancePastCheck = 1.2f;
    [SerializeField, Min(0f)] private float timingWindowPadding = 0.35f;
    [SerializeField, Min(0f)] private float washLockoutDuration;

    [Header("Completion")]
    [SerializeField, Min(1)] private int requiredSuccessfulHits = 5;
    [SerializeField] private string completionFlagId = "dishes_washed";
    [SerializeField] private bool deactivateOnComplete = true;

    [Header("Failure Cutscene")]
    [SerializeField] private bool playUntilFail = true;
    [SerializeField] private WashingFailureCutscene failureCutscene;
    [SerializeField] private bool completeAfterFailureCutscene = true;
    [SerializeField] private bool deactivateAfterFailureCutscene = true;

    [Header("Post Failure Story")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueSO postFailureDialogue;
    [SerializeField] private string postFailureMissionAssignedFlag = "mission_find_owner_memento";

    [Header("Animation")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string washStateName = "Wash";
    [SerializeField, Min(0f)] private float washCrossFadeDuration = 0.04f;
    [SerializeField, Min(0f)] private float idleReturnDelay = 0.18f;

    [Header("Polish")]
    [SerializeField, Min(0f)] private float dishConsumeDuration = 0.07f;
    [SerializeField, Min(0f)] private float promptFadeDuration = 0.15f;
    [SerializeField, Min(0f)] private float promptPulseDuration = 0.55f;
    [SerializeField, Min(0f)] private float checkPulseDuration = 0.5f;
    [SerializeField, Min(0f)] private float promptPunchDuration = 0.12f;
    [SerializeField] private Vector3 promptPunchScale = new(0.14f, 0.14f, 0f);
    [SerializeField] private Vector3 checkPulseScale = new(0.12f, 0.12f, 0f);

    private SpriteRenderer[] _movingDishRenderers;
    private Vector3 _movingDishStartPosition;
    private Vector3 _movingDishStartScale;
    private Vector3 _buttonPromptStartScale;
    private Vector3 _checkPulseStartScale;
    private Color _buttonPromptStartColor;
    private float _currentTravelDuration;
    private float _cachedCheckLocalX;
    private float _cachedMovingDishColliderOffsetX;
    private float _cachedCheckHalfWidthLocal;
    private float _cachedMovingDishHalfWidthLocal;
    private int _successCount;
    private int _hitVersion;
    private bool _isPlaying;
    private bool _roundActive;
    private bool _isResolvingRound;
    private bool _failed;
    private bool _hasLockedGameState;
    private bool _hasCachedInitialState;
    private bool _hasCachedTimingGeometry;
    private bool _isPlayingFailureCutscene;
    private bool _warnedMissingUi;
    private bool _warnedMissingFailureCutscene;
    private bool _warnedMissingAnimatorState;
    private GameState _previousGameState = GameState.Playing;
    private Tween _moveTween;
    private Sequence _successSequence;
    private Tween _promptPulseTween;
    private Tween _checkPulseTween;
    private Tween _idleReturnTween;

    public bool IsPlaying => _isPlaying || _isPlayingFailureCutscene;

    private void Awake()
    {
        ResolveReferences();
        CacheInitialState();
        ResetVisualsForNextDish();
        HidePromptCue(false);
        PlayAnimatorState(idleStateName, false);
        if (hideMinigameUntilStarted)
        {
            SetMinigameVisible(false);
        }
    }

    private void OnEnable()
    {
        if (autoStartOnEnable)
        {
            StartMinigame();
            return;
        }

        if (hideMinigameUntilStarted)
        {
            SetMinigameVisible(false);
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (!_roundActive || _isResolvingRound || _failed)
        {
            return;
        }

        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            TryHit();
        }
    }

    private void OnDisable()
    {
        StopAllTweens();
        ReleaseGameStateLock();
        _isPlaying = false;
        _roundActive = false;
        _isResolvingRound = false;
        _isPlayingFailureCutscene = false;
    }

    private void OnDestroy()
    {
        StopAllTweens();
    }

    public void StartFromInteraction()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (!_isPlaying)
        {
            StartMinigame();
        }
    }

    public void StartMinigame()
    {
        ResolveReferences();
        CacheInitialState();
        StopAllTweens();

        _failed = false;
        _isPlayingFailureCutscene = false;
        _successCount = 0;
        _hitVersion = 0;
        _hasCachedTimingGeometry = false;
        _currentTravelDuration = tutorialTravelDuration;
        _isPlaying = true;
        _isResolvingRound = false;
        SetMinigameVisible(true);
        ResetVisualsForNextDish();
        CacheTimingGeometry();
        LockGameState();
        HidePromptCue(false);
        StartCheckPulse();
        StartRound();
    }

    private void StartRound()
    {
        if (!_isPlaying || _failed || movingDish == null)
        {
            return;
        }

        _roundActive = true;
        _isResolvingRound = false;
        ResetVisualsForNextDish();

        if (_successCount == 0)
        {
            StartTutorialRound();
            return;
        }

        StartNormalRound();
    }

    private void StartTutorialRound()
    {
        _roundActive = false;
        HidePromptCue(false);
        StartCheckPulse();

        Vector3 stopPosition = ResolveCheckStopPosition();
        _moveTween = movingDish
            .DOLocalMove(stopPosition, Mathf.Max(0.05f, tutorialTravelDuration))
            .SetEase(Ease.OutQuart)
            .SetTarget(this)
            .OnComplete(() =>
            {
                _moveTween = null;
                _roundActive = true;
                ShowPromptCue(true);
            });
    }

    private void StartNormalRound()
    {
        _roundActive = true;
        ShowPromptCue(true);
        StartCheckPulse();

        float failX = ResolveCheckLocalX() - ResolveMovingDishColliderLocalOffsetX() - failDistancePastCheck;
        Vector3 failPosition = new(failX, _movingDishStartPosition.y, _movingDishStartPosition.z);

        _moveTween = movingDish
            .DOLocalMove(failPosition, Mathf.Max(minimumTravelDuration, _currentTravelDuration))
            .SetEase(Ease.Linear)
            .SetTarget(this)
            .OnComplete(() => FailMinigame("Dish passed the check window."));
    }

    private void TryHit()
    {
        if (IsDishInCheckWindow())
        {
            ResolveSuccessfulHit();
            return;
        }

        FailMinigame("Pressed E outside the check window.");
    }

    private void ResolveSuccessfulHit()
    {
        if (_isResolvingRound)
        {
            return;
        }

        _isResolvingRound = true;
        _roundActive = false;
        _moveTween?.Kill();
        _moveTween = null;
        _idleReturnTween?.Kill();
        _idleReturnTween = null;
        _successCount++;

        int hitVersion = ++_hitVersion;
        PlayAnimatorState(washStateName, true, washCrossFadeDuration);
        PunchPrompt();

        _successSequence?.Kill();
        _successSequence = DOTween.Sequence()
            .SetTarget(this)
            .Join(FadeDishTo(0f, dishConsumeDuration))
            .Join(movingDish.DOScale(_movingDishStartScale * 0.18f, dishConsumeDuration).SetEase(Ease.InBack));

        if (washLockoutDuration > 0f)
        {
            _successSequence.AppendInterval(washLockoutDuration);
        }

        _successSequence.OnComplete(() =>
        {
            if (!playUntilFail && _successCount >= Mathf.Max(1, requiredSuccessfulHits))
            {
                CompleteMinigame();
                return;
            }

            _currentTravelDuration = Mathf.Max(minimumTravelDuration, normalTravelDuration * Mathf.Pow(speedMultiplier, _successCount - 1));
            StartRound();
            ScheduleIdleReturn(hitVersion);
        });
    }

    private void CompleteMinigame()
    {
        _successSequence = null;
        _roundActive = false;
        _isResolvingRound = false;
        _isPlaying = false;
        _failed = false;

        HidePromptCue(false);
        _checkPulseTween?.Kill();
        _checkPulseTween = null;
        ReleaseGameStateLock();
        SetCompletionFlag();

        if (deactivateOnComplete)
        {
            gameObject.SetActive(false);
        }
    }

    private bool IsDishInCheckWindow()
    {
        if (movingDish == null || checkCollider == null)
        {
            return false;
        }

        if (!checkCollider.enabled || (movingDishCollider != null && !movingDishCollider.enabled))
        {
            return false;
        }

        if (!_hasCachedTimingGeometry)
        {
            CacheTimingGeometry();
        }

        float dishCenterX = movingDish.localPosition.x + _cachedMovingDishColliderOffsetX;
        float timingHalfWidth = _cachedCheckHalfWidthLocal
            + _cachedMovingDishHalfWidthLocal
            + timingWindowPadding;

        return Mathf.Abs(dishCenterX - _cachedCheckLocalX) <= timingHalfWidth;
    }

    private void FailMinigame(string reason)
    {
        if (_failed)
        {
            return;
        }

        _failed = true;
        _roundActive = false;
        _isResolvingRound = false;
        _isPlaying = false;
        _isPlayingFailureCutscene = true;
        StopAllTweens();
        HidePromptCue(false);
        _checkPulseTween?.Kill();
        _checkPulseTween = null;
        SetMinigameVisible(false);

        RunFailureCutsceneAsync(reason, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid RunFailureCutsceneAsync(string reason, CancellationToken cancellationToken)
    {
        bool shouldFinalize = false;

        try
        {
            WashingFailureCutscene resolvedCutscene = ResolveFailureCutscene();
            if (resolvedCutscene != null)
            {
                await resolvedCutscene.PlayAsync(cancellationToken);
            }
            else if (!_warnedMissingFailureCutscene)
            {
                _warnedMissingFailureCutscene = true;
                Debug.LogWarning($"WashingMinigameController: failed but no failure cutscene was found. Reason: {reason}", this);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (completeAfterFailureCutscene)
            {
                SetCompletionFlag();
            }

            await PlayPostFailureDialogueAsync(cancellationToken);
            SetPostFailureMissionAssignedFlag();
            shouldFinalize = true;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (shouldFinalize && this != null)
            {
                _isPlayingFailureCutscene = false;
                ReleaseGameStateLock();

                if (deactivateAfterFailureCutscene)
                {
                    gameObject.SetActive(false);
                }
            }
        }
    }

    private void ResetVisualsForNextDish()
    {
        if (movingDish != null)
        {
            movingDish.localPosition = _movingDishStartPosition;
            movingDish.localScale = _movingDishStartScale;
        }

        SetDishAlpha(1f);
    }

    private Tween FadeDishTo(float alpha, float duration)
    {
        if (_movingDishRenderers == null || _movingDishRenderers.Length == 0)
        {
            return DOVirtual.DelayedCall(0f, () => { });
        }

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        float resolvedDuration = Mathf.Max(0f, duration);
        for (int i = 0; i < _movingDishRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = _movingDishRenderers[i];
            if (spriteRenderer != null)
            {
                sequence.Join(spriteRenderer.DOFade(alpha, resolvedDuration));
            }
        }

        return sequence;
    }

    private void SetDishAlpha(float alpha)
    {
        if (_movingDishRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _movingDishRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = _movingDishRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }

    private void StartPromptPulse()
    {
        if (buttonPrompt == null)
        {
            return;
        }

        _promptPulseTween?.Kill();
        buttonPrompt.localScale = _buttonPromptStartScale;
        _promptPulseTween = buttonPrompt
            .DOScale(_buttonPromptStartScale * 1.08f, Mathf.Max(0.01f, promptPulseDuration))
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetTarget(this);
    }

    private void ShowPromptCue(bool animate)
    {
        SetPromptVisible(true, animate);
        StartPromptPulse();
    }

    private void HidePromptCue(bool animate)
    {
        _promptPulseTween?.Kill();
        _promptPulseTween = null;

        if (buttonPrompt != null)
        {
            buttonPrompt.DOKill();
            buttonPrompt.localScale = _buttonPromptStartScale;
        }

        SetPromptVisible(false, animate);
    }

    private void SetPromptVisible(bool visible, bool animate)
    {
        if (buttonPromptRenderer == null)
        {
            return;
        }

        buttonPromptRenderer.DOKill();
        buttonPromptRenderer.enabled = true;

        Color targetColor = _buttonPromptStartColor;
        targetColor.a = visible ? _buttonPromptStartColor.a : 0f;

        if (!animate || promptFadeDuration <= 0f)
        {
            buttonPromptRenderer.color = targetColor;
            buttonPromptRenderer.enabled = visible;
            return;
        }

        buttonPromptRenderer
            .DOFade(targetColor.a, promptFadeDuration)
            .SetTarget(this)
            .OnComplete(() => buttonPromptRenderer.enabled = visible);
    }

    private void SetMinigameVisible(bool visible)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(visible);
        }
    }

    private void StartCheckPulse()
    {
        if (checkPulseTarget == null)
        {
            return;
        }

        _checkPulseTween?.Kill();
        checkPulseTarget.localScale = _checkPulseStartScale;
        _checkPulseTween = checkPulseTarget
            .DOScale(_checkPulseStartScale + checkPulseScale, Mathf.Max(0.01f, checkPulseDuration))
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetTarget(this);
    }

    private void PunchPrompt()
    {
        if (buttonPrompt == null)
        {
            return;
        }

        _promptPulseTween?.Kill();
        _promptPulseTween = null;
        buttonPrompt.DOKill();
        buttonPrompt.localScale = _buttonPromptStartScale;
        buttonPrompt
            .DOPunchScale(promptPunchScale, Mathf.Max(0.01f, promptPunchDuration), 4, 0.45f)
            .SetTarget(this)
            .OnComplete(() =>
            {
                if (_roundActive && !_failed)
                {
                    StartPromptPulse();
                }
            });

        if (buttonPromptRenderer != null)
        {
            buttonPromptRenderer.color = new Color(1f, 0.92f, 0.25f, _buttonPromptStartColor.a);
            buttonPromptRenderer
                .DOColor(_buttonPromptStartColor, Mathf.Max(0.01f, promptPunchDuration))
                .SetTarget(this);
        }
    }

    private void PlayAnimatorState(string stateName, bool restart, float crossFadeDuration = 0f)
    {
        if (washingCatAnimator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!washingCatAnimator.HasState(0, stateHash))
        {
            if (!_warnedMissingAnimatorState && restart)
            {
                _warnedMissingAnimatorState = true;
                Debug.LogWarning($"WashingMinigameController: Animator has no state named '{stateName}'.", washingCatAnimator);
            }

            return;
        }

        if (crossFadeDuration > 0f && washingCatAnimator.isActiveAndEnabled)
        {
            washingCatAnimator.CrossFadeInFixedTime(stateHash, crossFadeDuration, 0, 0f);
            return;
        }

        washingCatAnimator.Play(stateHash, 0, restart ? 0f : 0f);
        if (!restart)
        {
            washingCatAnimator.Update(0f);
        }
    }

    private float ResolveCheckLocalX()
    {
        return _hasCachedTimingGeometry ? _cachedCheckLocalX : CalculateCheckLocalX();
    }

    private Vector3 ResolveCheckStopPosition()
    {
        float stopX = ResolveCheckLocalX() - ResolveMovingDishColliderLocalOffsetX();
        return new Vector3(stopX, _movingDishStartPosition.y, _movingDishStartPosition.z);
    }

    private float ResolveMovingDishColliderLocalOffsetX()
    {
        return _hasCachedTimingGeometry ? _cachedMovingDishColliderOffsetX : CalculateMovingDishColliderLocalOffsetX();
    }

    private void CacheTimingGeometry()
    {
        Physics2D.SyncTransforms();
        _cachedCheckLocalX = CalculateCheckLocalX();
        _cachedMovingDishColliderOffsetX = CalculateMovingDishColliderLocalOffsetX();
        _cachedCheckHalfWidthLocal = ResolveColliderHalfWidthLocal(checkCollider);
        _cachedMovingDishHalfWidthLocal = ResolveColliderHalfWidthLocal(movingDishCollider);
        _hasCachedTimingGeometry = true;
    }

    private float CalculateCheckLocalX()
    {
        if (checkCollider != null)
        {
            return transform.InverseTransformPoint(checkCollider.bounds.center).x;
        }

        return _movingDishStartPosition.x - 1f;
    }

    private float CalculateMovingDishColliderLocalOffsetX()
    {
        if (movingDishCollider == null || movingDish == null)
        {
            return 0f;
        }

        return transform.InverseTransformPoint(movingDishCollider.bounds.center).x - movingDish.localPosition.x;
    }

    private void ScheduleIdleReturn(int hitVersion)
    {
        _idleReturnTween?.Kill();
        _idleReturnTween = null;

        if (idleReturnDelay <= 0f)
        {
            TryReturnToIdle(hitVersion);
            return;
        }

        _idleReturnTween = DOVirtual
            .DelayedCall(idleReturnDelay, () => TryReturnToIdle(hitVersion))
            .SetTarget(this);
    }

    private void TryReturnToIdle(int hitVersion)
    {
        _idleReturnTween = null;
        if (hitVersion != _hitVersion || !_isPlaying || _failed || _isResolvingRound)
        {
            return;
        }

        PlayAnimatorState(idleStateName, false, washCrossFadeDuration);
    }

    private float ResolveColliderHalfWidthLocal(Collider2D collider)
    {
        if (collider == null)
        {
            return 0f;
        }

        Bounds bounds = collider.bounds;
        Vector3 localMin = transform.InverseTransformPoint(new Vector3(bounds.min.x, bounds.center.y, bounds.center.z));
        Vector3 localMax = transform.InverseTransformPoint(new Vector3(bounds.max.x, bounds.center.y, bounds.center.z));
        return Mathf.Abs(localMax.x - localMin.x) * 0.5f;
    }

    private void StopAllTweens()
    {
        _moveTween?.Kill();
        _successSequence?.Kill();
        _promptPulseTween?.Kill();
        _checkPulseTween?.Kill();
        _idleReturnTween?.Kill();
        DOTween.Kill(this);

        if (movingDish != null)
        {
            movingDish.DOKill();
        }

        if (buttonPrompt != null)
        {
            buttonPrompt.DOKill();
        }

        if (checkPulseTarget != null)
        {
            checkPulseTarget.DOKill();
        }
    }

    private void LockGameState()
    {
        if (!lockGameStateWhilePlaying || _hasLockedGameState || GameManager.Instance == null)
        {
            return;
        }

        _previousGameState = GameManager.Instance.CurrentState;
        if (_previousGameState == GameState.OnDialog)
        {
            return;
        }

        _hasLockedGameState = true;
        GameManager.Instance.SetState(GameState.OnDialog);
    }

    private void ReleaseGameStateLock()
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

    private UIManager ResolveUiManager()
    {
        if (uiManager == null)
        {
            uiManager = UnityEngine.Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        }

        return uiManager;
    }

    private WashingFailureCutscene ResolveFailureCutscene()
    {
        if (failureCutscene == null)
        {
            failureCutscene = UnityEngine.Object.FindFirstObjectByType<WashingFailureCutscene>(FindObjectsInactive.Include);
        }

        return failureCutscene;
    }

    private void ResolveReferences()
    {
        if (movingDish == null)
        {
            movingDish = FindDeepChild(transform, "DirtyDish (1)");
        }

        if (checkCollider == null)
        {
            Transform check = FindDeepChild(transform, "GameObject");
            if (check != null)
            {
                checkCollider = check.GetComponent<Collider2D>();
            }
        }

        if (movingDishCollider == null && movingDish != null)
        {
            movingDishCollider = movingDish.GetComponentInChildren<Collider2D>(true);
        }

        if (checkPulseTarget == null)
        {
            checkPulseTarget = FindDeepChild(transform, "CheckPulse");
        }

        if (buttonPrompt == null)
        {
            buttonPrompt = FindDeepChild(transform, "ButtonE");
        }

        if (buttonPromptRenderer == null && buttonPrompt != null)
        {
            buttonPromptRenderer = buttonPrompt.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (washingCatAnimator == null)
        {
            Transform washingCat = FindDeepChild(transform, "WashingCat");
            if (washingCat != null)
            {
                washingCatAnimator = washingCat.GetComponent<Animator>();
            }
        }

        if (trackRoot == null)
        {
            trackRoot = FindDeepChild(transform, "Square");
        }

        ResolveFailureCutscene();

        _movingDishRenderers = movingDish != null
            ? movingDish.GetComponentsInChildren<SpriteRenderer>(true)
            : new SpriteRenderer[0];
    }

    private void CacheInitialState()
    {
        if (_hasCachedInitialState)
        {
            return;
        }

        if (movingDish != null)
        {
            _movingDishStartPosition = movingDish.localPosition;
            _movingDishStartScale = movingDish.localScale;
        }

        if (buttonPrompt != null)
        {
            _buttonPromptStartScale = buttonPrompt.localScale;
        }

        if (checkPulseTarget != null)
        {
            _checkPulseStartScale = checkPulseTarget.localScale;
        }

        if (buttonPromptRenderer != null)
        {
            _buttonPromptStartColor = buttonPromptRenderer.color;
        }

        _hasCachedInitialState = true;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Transform match = FindDeepChild(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private async UniTask PlayPostFailureDialogueAsync(CancellationToken cancellationToken)
    {
        if (postFailureDialogue == null)
        {
            return;
        }

        DialogueManager manager = ResolveDialogueManager();
        if (manager == null)
        {
            Debug.LogWarning("WashingMinigameController: post-failure dialogue is assigned but no DialogueManager was found.", this);
            return;
        }

        await manager.PlayDialogueAsync(postFailureDialogue, cancellationToken);
    }

    private void SetPostFailureMissionAssignedFlag()
    {
        if (!string.IsNullOrWhiteSpace(postFailureMissionAssignedFlag) && FlagManager.Instance != null)
        {
            FlagManager.Instance.SetFlag(postFailureMissionAssignedFlag, true);
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
}
