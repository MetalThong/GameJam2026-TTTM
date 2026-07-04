using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StoreRoomCatMeowTransformAbility : MonoBehaviour
{
    private enum AbilityState
    {
        Ready,
        Dialogue,
        Active,
        Cooldown
    }

    [Header("References")]
    [SerializeField] private Movement movement;
    [SerializeField] private CatInteractor catInteractor;
    [SerializeField] private RandomDialogueSpawner randomDialogueSpawner;
    [SerializeField] private RadialTimerUI timerUI;
    [SerializeField] private InteractionPromptView interactionPromptView;

    [Header("Scene")]
    [SerializeField] private string requiredSceneName = "StoreRoom";

    [Header("Forms")]
    [SerializeField] private MovementForm requiredStartForm = MovementForm.Ghost;
    [SerializeField] private MovementForm activeForm = MovementForm.Cat;
    [SerializeField] private MovementForm inactiveForm = MovementForm.Ghost;
    [SerializeField] private bool requireStartForm = true;
    [SerializeField] private bool revertToInactiveFormOnCancel = true;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float activeDuration = 5f;
    [SerializeField, Min(0f)] private float cooldownDuration = 3f;
    [SerializeField] private bool useUnscaledTime;

    [Header("Timer Visual")]
    [SerializeField] private Color activeTimerColor = Color.white;
    [SerializeField] private Color cooldownTimerColor = new(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private bool hideTimerWhenReady = true;

    [Header("Audio")]
    [SerializeField] private bool playMeowAudio = true;

    [Header("Prompt")]
    [SerializeField] private bool showFallbackPrompt = true;
    [SerializeField] private string fallbackPromptKey = "prompt.store_meow";

    private AbilityState _state = AbilityState.Ready;
    private CancellationTokenSource _sequenceCts;
    private bool _fallbackPromptVisible;
    private bool _isDestroying;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (catInteractor != null)
        {
            catInteractor.FallbackInteractRequested += TryStartFromFallback;
        }

        if (movement != null)
        {
            movement.FormChanged += OnFormChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged += OnGameStateChanged;
        }

        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        if (hideTimerWhenReady && _state == AbilityState.Ready)
        {
            SetTimerVisible(false);
        }

        RefreshFallbackPrompt();
    }

    private void OnDisable()
    {
        if (catInteractor != null)
        {
            catInteractor.FallbackInteractRequested -= TryStartFromFallback;
        }

        if (movement != null)
        {
            movement.FormChanged -= OnFormChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged -= OnGameStateChanged;
        }

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        HideFallbackPrompt();
        CancelSequence(revertToInactiveFormOnCancel);
    }

    private void OnDestroy()
    {
        _isDestroying = true;
        HideFallbackPrompt();
        CancelSequence(revertToInactiveFormOnCancel, false);
    }

    private bool TryStartFromFallback()
    {
        if (!CanStart())
        {
            return false;
        }

        _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        RunSequenceAsync(_sequenceCts.Token).Forget();
        return true;
    }

    private async UniTaskVoid RunSequenceAsync(CancellationToken cancellationToken)
    {
        bool activeFormApplied = false;
        bool completed = false;

        try
        {
            _state = AbilityState.Dialogue;
            SetTimerVisible(false);
            RefreshFallbackPrompt();

            if (playMeowAudio)
            {
                AudioFeedback.PlayCatMeow();
            }

            if (randomDialogueSpawner != null)
            {
                await randomDialogueSpawner.PlayRandomAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            movement.SetForm(activeForm);
            activeFormApplied = true;
            RefreshFallbackPrompt();

            _state = AbilityState.Active;
            await RunTimerAsync(activeDuration, activeTimerColor, cancellationToken);

            movement.SetForm(inactiveForm);
            activeFormApplied = false;

            _state = AbilityState.Cooldown;
            RefreshFallbackPrompt();
            await RunTimerAsync(cooldownDuration, cooldownTimerColor, cancellationToken);

            completed = true;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!completed && activeFormApplied && revertToInactiveFormOnCancel && movement != null)
            {
                movement.SetForm(inactiveForm);
            }

            _state = AbilityState.Ready;

            if (hideTimerWhenReady)
            {
                SetTimerVisible(false);
            }

            RefreshFallbackPrompt();
            DisposeSequenceCts();
        }
    }

    private async UniTask RunTimerAsync(float duration, Color color, CancellationToken cancellationToken)
    {
        if (duration <= 0f)
        {
            return;
        }

        if (timerUI == null)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: cancellationToken);
            return;
        }

        SetTimerVisible(true);
        timerUI.Pause();
        timerUI.SetFillColor(color);
        timerUI.SetNormalizedProgress(0f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            timerUI.SetNormalizedProgress(elapsed / duration);
        }

        timerUI.SetNormalizedProgress(1f);
        timerUI.Pause();
    }

    private bool CanStart()
    {
        ResolveReferences();

        if (_state != AbilityState.Ready
            || movement == null
            || !IsInRequiredScene()
            || IsGameplayBlocked())
        {
            return false;
        }

        return !requireStartForm || movement.CurrentForm == requiredStartForm;
    }

    private bool IsInRequiredScene()
    {
        if (string.IsNullOrWhiteSpace(requiredSceneName))
        {
            return true;
        }

        return SceneManager.GetActiveScene().name == requiredSceneName;
    }

    private static bool IsGameplayBlocked()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing;
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene currentScene)
    {
        if (!IsInRequiredScene())
        {
            HideFallbackPrompt();
            CancelSequence(revertToInactiveFormOnCancel);
            return;
        }

        if (hideTimerWhenReady && _state == AbilityState.Ready)
        {
            SetTimerVisible(false);
        }

        RefreshFallbackPrompt();
    }

    private void OnFormChanged(MovementForm previousForm, MovementForm currentForm)
    {
        RefreshFallbackPrompt();
    }

    private void OnGameStateChanged(GameState previousState, GameState currentState)
    {
        RefreshFallbackPrompt();
    }

    private void CancelSequence(bool revertForm, bool refreshPrompt = true)
    {
        if (_sequenceCts != null)
        {
            _sequenceCts.Cancel();
            DisposeSequenceCts();
        }

        if (revertForm && movement != null && _state == AbilityState.Active)
        {
            movement.SetForm(inactiveForm);
        }

        _state = AbilityState.Ready;

        if (hideTimerWhenReady)
        {
            SetTimerVisible(false);
        }

        if (refreshPrompt && !_isDestroying)
        {
            RefreshFallbackPrompt();
        }
    }

    private void DisposeSequenceCts()
    {
        if (_sequenceCts == null)
        {
            return;
        }

        _sequenceCts.Dispose();
        _sequenceCts = null;
    }

    private void SetTimerVisible(bool isVisible)
    {
        if (timerUI != null)
        {
            timerUI.gameObject.SetActive(isVisible);
        }
    }

    private void ResolveReferences()
    {
        if (movement == null)
        {
            movement = GetComponent<Movement>();
        }

        if (catInteractor == null)
        {
            catInteractor = GetComponent<CatInteractor>();
        }

        if (interactionPromptView == null)
        {
            interactionPromptView = UnityEngine.Object.FindFirstObjectByType<InteractionPromptView>(FindObjectsInactive.Include);
        }
    }

    private void RefreshFallbackPrompt()
    {
        if (_isDestroying || this == null)
        {
            return;
        }

        if (!isActiveAndEnabled)
        {
            HideFallbackPrompt();
            return;
        }

        bool shouldShow = showFallbackPrompt && CanStart();
        if (!shouldShow)
        {
            HideFallbackPrompt();
            return;
        }

        InteractionPromptView promptView = ResolvePromptView();
        if (promptView == null)
        {
            _fallbackPromptVisible = false;
            return;
        }

        promptView.ShowFallback(this, fallbackPromptKey);
        _fallbackPromptVisible = true;
    }

    private void HideFallbackPrompt()
    {
        if (!_fallbackPromptVisible && interactionPromptView == null)
        {
            return;
        }

        if (interactionPromptView != null)
        {
            interactionPromptView.HideFallback(this);
        }

        _fallbackPromptVisible = false;
    }

    private InteractionPromptView ResolvePromptView()
    {
        if (interactionPromptView != null)
        {
            return interactionPromptView;
        }

        interactionPromptView = UnityEngine.Object.FindFirstObjectByType<InteractionPromptView>(FindObjectsInactive.Include);
        return interactionPromptView;
    }
}
