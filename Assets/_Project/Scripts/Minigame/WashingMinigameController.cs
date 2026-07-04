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
    [SerializeField] private bool startOnEnter = false;
    [SerializeField] private bool hideMinigameUntilStarted = false;
    [SerializeField] private bool lockGameStateWhilePlaying = true;
    [SerializeField, Min(0.05f)] private float tutorialTravelDuration = 3f;
    [SerializeField, Min(0.05f)] private float normalTravelDuration = 2f;
    [SerializeField, Range(0.1f, 1f)] private float speedMultiplier = 0.9f;
    [SerializeField, Min(0.05f)] private float minimumTravelDuration = 0.75f;
    [SerializeField, Min(0f)] private float failDistancePastCheck = 1.2f;
    [SerializeField, Min(0f)] private float timingWindowPadding = 0.35f;
    [SerializeField, Min(0f)] private float washLockoutDuration = 0.33f;

    [Header("Animation")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string washStateName = "Wash";

    [Header("Polish")]
    [SerializeField, Min(0f)] private float dishConsumeDuration = 0.18f;
    [SerializeField, Min(0f)] private float promptFadeDuration = 0.15f;
    [SerializeField, Min(0f)] private float promptPulseDuration = 0.55f;
    [SerializeField, Min(0f)] private float checkPulseDuration = 0.5f;
    [SerializeField] private Vector3 promptPunchScale = new(0.22f, 0.22f, 0f);
    [SerializeField] private Vector3 checkPulseScale = new(0.12f, 0.12f, 0f);

    private SpriteRenderer[] _movingDishRenderers;
    private Vector3 _movingDishStartPosition;
    private Vector3 _movingDishStartScale;
    private Vector3 _buttonPromptStartScale;
    private Vector3 _checkPulseStartScale;
    private Color _buttonPromptStartColor;
    private float _currentTravelDuration;
    private int _successCount;
    private bool _isPlaying;
    private bool _roundActive;
    private bool _isResolvingRound;
    private bool _failed;
    private bool _hasLockedGameState;
    private bool _hasCachedInitialState;
    private bool _waitingForEnterStart;
    private bool _warnedMissingUi;
    private bool _warnedMissingAnimatorState;
    private GameState _previousGameState = GameState.Playing;
    private Tween _moveTween;
    private Sequence _successSequence;
    private Tween _promptPulseTween;
    private Tween _checkPulseTween;

    private void Awake()
    {
        ResolveReferences();
        CacheInitialState();
        ResetVisualsForNextDish();
        HidePromptCue(false);
        PlayAnimatorState(idleStateName, false);
        if (startOnEnter && hideMinigameUntilStarted)
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

        _waitingForEnterStart = startOnEnter;
        if (_waitingForEnterStart && hideMinigameUntilStarted)
        {
            SetMinigameVisible(false);
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (_waitingForEnterStart)
        {
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                StartMinigame();
            }

            return;
        }

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
    }

    private void OnDestroy()
    {
        StopAllTweens();
    }

    public void StartMinigame()
    {
        ResolveReferences();
        CacheInitialState();
        StopAllTweens();

        _failed = false;
        _successCount = 0;
        _currentTravelDuration = tutorialTravelDuration;
        _isPlaying = true;
        _isResolvingRound = false;
        _waitingForEnterStart = false;
        SetMinigameVisible(true);
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
        _successCount++;

        PlayAnimatorState(washStateName, true);
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
            PlayAnimatorState(idleStateName, false);
            _currentTravelDuration = Mathf.Max(minimumTravelDuration, normalTravelDuration * Mathf.Pow(speedMultiplier, _successCount - 1));
            StartRound();
        });
    }

    private bool IsDishInCheckWindow()
    {
        if (movingDish == null || checkCollider == null)
        {
            return false;
        }

        Physics2D.SyncTransforms();
        if (!checkCollider.enabled || (movingDishCollider != null && !movingDishCollider.enabled))
        {
            return false;
        }

        float dishCenterX = ResolveMovingDishCenterLocalX();
        float checkCenterX = ResolveCheckLocalX();
        float timingHalfWidth = ResolveColliderHalfWidthLocal(checkCollider)
            + ResolveColliderHalfWidthLocal(movingDishCollider)
            + timingWindowPadding;

        return Mathf.Abs(dishCenterX - checkCenterX) <= timingHalfWidth;
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
        StopAllTweens();
        ReleaseGameStateLock();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameState.GameOver);
        }

        UIManager resolvedUiManager = ResolveUiManager();
        if (resolvedUiManager != null)
        {
            resolvedUiManager.OpenPanel(PanelId.Lose);
            return;
        }

        if (!_warnedMissingUi)
        {
            _warnedMissingUi = true;
            Debug.LogWarning($"WashingMinigameController: failed but no UIManager was found. Reason: {reason}", this);
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
            .DOPunchScale(promptPunchScale, 0.22f, 6, 0.65f)
            .SetTarget(this)
            .OnComplete(StartPromptPulse);

        if (buttonPromptRenderer != null)
        {
            buttonPromptRenderer.color = new Color(1f, 0.92f, 0.25f, _buttonPromptStartColor.a);
            buttonPromptRenderer
                .DOColor(_buttonPromptStartColor, 0.18f)
                .SetTarget(this);
        }
    }

    private void PlayAnimatorState(string stateName, bool restart)
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

        washingCatAnimator.Play(stateHash, 0, restart ? 0f : 0f);
        washingCatAnimator.Update(0f);
    }

    private float ResolveCheckLocalX()
    {
        if (checkCollider != null)
        {
            return transform.InverseTransformPoint(checkCollider.bounds.center).x;
        }

        return _movingDishStartPosition.x - 1f;
    }

    private Vector3 ResolveCheckStopPosition()
    {
        float stopX = ResolveCheckLocalX() - ResolveMovingDishColliderLocalOffsetX();
        return new Vector3(stopX, _movingDishStartPosition.y, _movingDishStartPosition.z);
    }

    private float ResolveMovingDishColliderLocalOffsetX()
    {
        if (movingDishCollider == null || movingDish == null)
        {
            return 0f;
        }

        Physics2D.SyncTransforms();
        return transform.InverseTransformPoint(movingDishCollider.bounds.center).x - movingDish.localPosition.x;
    }

    private float ResolveMovingDishCenterLocalX()
    {
        if (movingDishCollider != null)
        {
            return transform.InverseTransformPoint(movingDishCollider.bounds.center).x;
        }

        return movingDish.localPosition.x;
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

    private UIManager ResolveUiManager()
    {
        if (uiManager == null)
        {
            uiManager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        }

        return uiManager;
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
}
