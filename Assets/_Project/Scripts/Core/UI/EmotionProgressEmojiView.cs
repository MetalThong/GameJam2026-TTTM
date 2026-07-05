using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class EmotionProgressEmojiView : MonoBehaviour
{
    [Serializable]
    private sealed class EmotionStage
    {
        [SerializeField] private string unlockFlag;
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform rectTransform;

        public string UnlockFlag => unlockFlag;
        public GameObject Root => root;
        public CanvasGroup CanvasGroup => canvasGroup;
        public RectTransform RectTransform => rectTransform;

        public void Bind(GameObject resolvedRoot, CanvasGroup resolvedCanvasGroup, RectTransform resolvedRectTransform)
        {
            root = resolvedRoot;
            canvasGroup = resolvedCanvasGroup;
            rectTransform = resolvedRectTransform;
        }
    }

    private sealed class RuntimeStage
    {
        public EmotionStage Definition;
        public GameObject Root;
        public CanvasGroup CanvasGroup;
        public RectTransform RectTransform;
        public Vector3 VisibleScale;
        public bool IsUnlocked;
        public Tween Tween;
    }

    [Header("Stages")]
    [SerializeField] private List<EmotionStage> stages = new();
    [SerializeField] private bool autoBindChildrenByIndex = true;

    [Header("Visibility")]
    [SerializeField] private CanvasGroup containerGroup;
    [SerializeField] private bool requireGameplayState = true;
    [SerializeField] private bool visibleDuringDialogue = true;
    [SerializeField] private bool visibleWhilePaused;
    [SerializeField] private string hiddenSceneName = "MainMenu";

    [Header("Animation")]
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;
    [SerializeField, Min(0.01f)] private float hiddenScale = 0.45f;
    [SerializeField, Min(0f)] private float revealDuration = 0.45f;
    [SerializeField, Min(0f)] private float punchDuration = 0.18f;
    [SerializeField] private Vector3 punchScale = new(0.12f, 0.12f, 0f);
    [SerializeField] private Ease revealEase = Ease.OutBack;

    private readonly List<RuntimeStage> _runtimeStages = new();
    private bool _hasBuiltStages;
    private bool _isSubscribedToGameState;

    private void Awake()
    {
        ResolveContainerGroup();
        BuildStages();
        RefreshFromFlags(false);
        RefreshContainerVisibility();
    }

    private void OnEnable()
    {
        TrySubscribeGameState();
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);

        BuildStages();
        RefreshFromFlags(false);
        RefreshContainerVisibility();
    }

    private void OnDisable()
    {
        UnsubscribeGameState();
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);
        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    private void OnFlagChanged(FlagChangedEvent eventData)
    {
        BuildStages();
        RefreshContainerVisibility();

        if (!eventData.Value)
        {
            RefreshFromFlags(false);
            return;
        }

        if (!ShouldShowContainer())
        {
            RefreshFromFlags(false);
            return;
        }

        bool handled = false;
        for (int i = 0; i < _runtimeStages.Count; i++)
        {
            RuntimeStage stage = _runtimeStages[i];
            if (stage?.Definition == null || stage.Definition.UnlockFlag != eventData.FlagId)
            {
                continue;
            }

            ApplyStageState(stage, true, true);
            handled = true;
        }

        if (!handled)
        {
            RefreshFromFlags(false);
        }
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        RefreshFromFlags(false);
        RefreshContainerVisibility();
    }

    private void RefreshFromFlags(bool animateNewUnlocks)
    {
        BuildStages();
        RefreshContainerVisibility();
        bool canAnimate = animateNewUnlocks && ShouldShowContainer();

        for (int i = 0; i < _runtimeStages.Count; i++)
        {
            RuntimeStage stage = _runtimeStages[i];
            if (stage?.Definition == null)
            {
                continue;
            }

            ApplyStageState(stage, HasFlag(stage.Definition.UnlockFlag), canAnimate);
        }
    }

    private void BuildStages()
    {
        if (_hasBuiltStages)
        {
            return;
        }

        _hasBuiltStages = true;
        _runtimeStages.Clear();

        for (int i = 0; i < stages.Count; i++)
        {
            EmotionStage definition = stages[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.UnlockFlag))
            {
                continue;
            }

            RuntimeStage stage = BuildRuntimeStage(definition, i);
            if (stage != null)
            {
                _runtimeStages.Add(stage);
            }
        }
    }

    private RuntimeStage BuildRuntimeStage(EmotionStage definition, int index)
    {
        GameObject root = definition.Root;
        if (root == null && autoBindChildrenByIndex)
        {
            Transform child = transform.Find(index.ToString());
            root = child != null ? child.gameObject : null;
        }

        if (root == null)
        {
            return null;
        }

        RectTransform rectTransform = definition.RectTransform;
        if (rectTransform == null)
        {
            rectTransform = root.GetComponent<RectTransform>();
        }

        CanvasGroup canvasGroup = definition.CanvasGroup;
        if (canvasGroup == null)
        {
            canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = root.AddComponent<CanvasGroup>();
            }
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        definition.Bind(root, canvasGroup, rectTransform);

        Vector3 visibleScale = rectTransform != null ? rectTransform.localScale : root.transform.localScale;
        return new RuntimeStage
        {
            Definition = definition,
            Root = root,
            CanvasGroup = canvasGroup,
            RectTransform = rectTransform,
            VisibleScale = visibleScale == Vector3.zero ? Vector3.one : visibleScale
        };
    }

    private void ApplyStageState(RuntimeStage stage, bool unlocked, bool animate)
    {
        if (stage == null)
        {
            return;
        }

        if (unlocked && animate && !stage.IsUnlocked)
        {
            Reveal(stage);
            return;
        }

        stage.Tween?.Kill();
        stage.Tween = null;
        stage.IsUnlocked = unlocked;

        if (stage.Root != null)
        {
            stage.Root.SetActive(unlocked);
        }

        if (stage.CanvasGroup != null)
        {
            stage.CanvasGroup.alpha = unlocked ? visibleAlpha : 0f;
            stage.CanvasGroup.blocksRaycasts = false;
            stage.CanvasGroup.interactable = false;
        }

        ApplyScale(stage, unlocked ? stage.VisibleScale : stage.VisibleScale * hiddenScale);
    }

    private void Reveal(RuntimeStage stage)
    {
        stage.Tween?.Kill();
        stage.IsUnlocked = true;

        if (stage.Root != null)
        {
            stage.Root.SetActive(true);
        }

        if (stage.CanvasGroup != null)
        {
            stage.CanvasGroup.alpha = 0f;
            stage.CanvasGroup.blocksRaycasts = false;
            stage.CanvasGroup.interactable = false;
        }

        ApplyScale(stage, stage.VisibleScale * hiddenScale);

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(stage.Root != null ? stage.Root : gameObject);

        if (stage.CanvasGroup != null)
        {
            sequence.Join(stage.CanvasGroup.DOFade(visibleAlpha, revealDuration));
        }

        if (stage.RectTransform != null)
        {
            sequence.Join(stage.RectTransform.DOScale(stage.VisibleScale, revealDuration).SetEase(revealEase));
            if (punchDuration > 0f)
            {
                sequence.Append(stage.RectTransform.DOPunchScale(punchScale, punchDuration, 6, 0.75f));
            }
        }

        stage.Tween = sequence.OnComplete(() =>
        {
            stage.Tween = null;
            ApplyStageState(stage, true, false);
        });
    }

    private static void ApplyScale(RuntimeStage stage, Vector3 scale)
    {
        if (stage.RectTransform != null)
        {
            stage.RectTransform.localScale = scale;
        }
        else if (stage.Root != null)
        {
            stage.Root.transform.localScale = scale;
        }
    }

    private static bool HasFlag(string flagId)
    {
        return !string.IsNullOrWhiteSpace(flagId)
            && FlagManager.Instance != null
            && FlagManager.Instance.HasFlag(flagId);
    }

    private void OnGameStateChanged(GameState previousState, GameState currentState)
    {
        RefreshContainerVisibility();
        RefreshFromFlags(false);
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene currentScene)
    {
        RefreshContainerVisibility();
        RefreshFromFlags(false);
    }

    private void RefreshContainerVisibility()
    {
        ResolveContainerGroup();
        bool visible = ShouldShowContainer();

        if (containerGroup == null)
        {
            return;
        }

        containerGroup.alpha = visible ? 1f : 0f;
        containerGroup.blocksRaycasts = false;
        containerGroup.interactable = false;
    }

    private bool ShouldShowContainer()
    {
        if (!requireGameplayState)
        {
            return true;
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(hiddenSceneName)
            && SceneManager.GetActiveScene().name == hiddenSceneName)
        {
            return false;
        }

        return gameManager.CurrentState == GameState.Playing
            || (visibleDuringDialogue && gameManager.CurrentState == GameState.OnDialog)
            || (visibleWhilePaused && gameManager.CurrentState == GameState.Paused);
    }

    private void TrySubscribeGameState()
    {
        if (_isSubscribedToGameState || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.StateChanged += OnGameStateChanged;
        _isSubscribedToGameState = true;
    }

    private void UnsubscribeGameState()
    {
        if (!_isSubscribedToGameState || GameManager.Instance == null)
        {
            _isSubscribedToGameState = false;
            return;
        }

        GameManager.Instance.StateChanged -= OnGameStateChanged;
        _isSubscribedToGameState = false;
    }

    private void ResolveContainerGroup()
    {
        if (containerGroup == null)
        {
            containerGroup = GetComponent<CanvasGroup>();
        }

        if (containerGroup == null)
        {
            containerGroup = gameObject.AddComponent<CanvasGroup>();
        }

        containerGroup.blocksRaycasts = false;
        containerGroup.interactable = false;
    }

    private void KillTweens()
    {
        for (int i = 0; i < _runtimeStages.Count; i++)
        {
            _runtimeStages[i]?.Tween?.Kill();
        }
    }
}
