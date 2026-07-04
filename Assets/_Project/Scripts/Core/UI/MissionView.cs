using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class MissionView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Content")]
    [SerializeField] private string prefix = "(!)";
    [SerializeField] private List<MissionDefinition> missions = new();

    [Header("Animation")]
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;
    [SerializeField] private Vector2 assignStartOffset = new(0f, 42f);
    [SerializeField] private Vector2 completeExitOffset = new(0f, 36f);
    [SerializeField, Min(0f)] private float assignFadeDuration = 0.45f;
    [SerializeField, Min(0f)] private float completeHoldDuration = 0.45f;
    [SerializeField, Min(0f)] private float completeFadeDuration = 0.65f;
    [SerializeField] private Ease assignEase = Ease.OutCubic;
    [SerializeField] private Ease completeEase = Ease.InCubic;

    private Vector2 _restPosition;
    private MissionDefinition _currentMission;
    private Sequence _sequence;
    private bool _isCompleting;

    private void Awake()
    {
        ResolveReferences();
        _restPosition = panel != null ? panel.anchoredPosition : Vector2.zero;
        SetHiddenInstant();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Subscribe<FlagsLoadedEvent>(OnFlagsLoaded);

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        }

        RefreshFromFlags();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FlagChangedEvent>(OnFlagChanged);
        EventBus.Unsubscribe<FlagsLoadedEvent>(OnFlagsLoaded);

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
        }
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
    }

    private void OnFlagChanged(FlagChangedEvent eventData)
    {
        if (ShouldSuppressMissionUi())
        {
            SetHiddenInstant();
            return;
        }

        if (!eventData.Value)
        {
            return;
        }

        MissionDefinition completedMission = FindMissionByCompletedFlag(eventData.FlagId);
        if (completedMission != null && IsCurrentMission(completedMission))
        {
            CompleteMission(completedMission);
            return;
        }

        MissionDefinition assignedMission = FindMissionByAssignedFlag(eventData.FlagId);
        if (assignedMission != null && !assignedMission.IsCompleted(FlagManager.Instance))
        {
            ShowMission(assignedMission, true);
        }
    }

    private void OnFlagsLoaded(FlagsLoadedEvent eventData)
    {
        RefreshFromFlags();
    }

    private void OnLanguageChanged(Language language)
    {
        RefreshCurrentText();
    }

    private void RefreshFromFlags()
    {
        if (ShouldSuppressMissionUi())
        {
            SetHiddenInstant();
            return;
        }

        MissionDefinition activeMission = FindActiveMissionFromFlags();
        if (activeMission != null)
        {
            ShowMission(activeMission, false);
            return;
        }

        SetHiddenInstant();
    }

    private static bool ShouldSuppressMissionUi()
    {
        return GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.MainMenu;
    }

    private void ShowMission(MissionDefinition mission, bool animate)
    {
        ResolveReferences();

        if (mission == null || panel == null || canvasGroup == null || titleText == null)
        {
            return;
        }

        _sequence?.Kill();
        _currentMission = mission;
        _isCompleting = false;
        RefreshCurrentText();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (!animate || assignFadeDuration <= 0f)
        {
            panel.anchoredPosition = _restPosition;
            canvasGroup.alpha = visibleAlpha;
            return;
        }

        panel.anchoredPosition = _restPosition + assignStartOffset;
        canvasGroup.alpha = 0f;

        _sequence = DOTween.Sequence()
            .SetUpdate(true)
            .Join(canvasGroup.DOFade(visibleAlpha, assignFadeDuration))
            .Join(panel.DOAnchorPos(_restPosition, assignFadeDuration).SetEase(assignEase))
            .OnComplete(() => _sequence = null);
    }

    private void CompleteMission(MissionDefinition mission)
    {
        ResolveReferences();

        if (mission == null || panel == null || canvasGroup == null || titleText == null)
        {
            return;
        }

        _sequence?.Kill();
        _currentMission = mission;
        _isCompleting = true;
        RefreshCurrentText();

        canvasGroup.alpha = visibleAlpha;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        _sequence = DOTween.Sequence().SetUpdate(true);

        if (completeHoldDuration > 0f)
        {
            _sequence.AppendInterval(completeHoldDuration);
        }

        _sequence
            .Append(canvasGroup.DOFade(0f, completeFadeDuration))
            .Join(panel.DOAnchorPos(_restPosition + completeExitOffset, completeFadeDuration).SetEase(completeEase))
            .OnComplete(SetHiddenInstant);
    }

    private void RefreshCurrentText()
    {
        if (_currentMission == null || titleText == null)
        {
            return;
        }

        string title = _currentMission.GetTitle(LocalizationManager.Instance);
        string missionText = string.IsNullOrWhiteSpace(prefix) ? title : $"{prefix} {title}";
        titleText.text = _isCompleting ? $"<s>{missionText}</s>" : missionText;
    }

    private void SetHiddenInstant()
    {
        ResolveReferences();
        _sequence?.Kill();
        _sequence = null;
        _currentMission = null;
        _isCompleting = false;

        if (panel != null)
        {
            panel.anchoredPosition = _restPosition;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private MissionDefinition FindActiveMissionFromFlags()
    {
        FlagManager flagManager = FlagManager.Instance;
        if (flagManager == null)
        {
            return null;
        }

        for (int i = missions.Count - 1; i >= 0; i--)
        {
            MissionDefinition mission = missions[i];
            if (mission != null
                && mission.ShouldRestoreWhenAssignedOnLoad
                && mission.IsAssigned(flagManager)
                && !mission.IsCompleted(flagManager))
            {
                return mission;
            }
        }

        return null;
    }

    private MissionDefinition FindMissionByAssignedFlag(string flagId)
    {
        for (int i = missions.Count - 1; i >= 0; i--)
        {
            MissionDefinition mission = missions[i];
            if (mission != null && mission.MatchesAssignedFlag(flagId))
            {
                return mission;
            }
        }

        return null;
    }

    private MissionDefinition FindMissionByCompletedFlag(string flagId)
    {
        for (int i = missions.Count - 1; i >= 0; i--)
        {
            MissionDefinition mission = missions[i];
            if (mission != null && mission.MatchesCompletedFlag(flagId))
            {
                return mission;
            }
        }

        return null;
    }

    private bool IsCurrentMission(MissionDefinition mission)
    {
        return _currentMission != null && _currentMission.IsSameMission(mission);
    }

    private void ResolveReferences()
    {
        if (panel == null)
        {
            panel = GetComponent<RectTransform>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (titleText == null)
        {
            titleText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
