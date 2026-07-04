using System;
using UnityEngine;

[Serializable]
public sealed class MissionDefinition
{
    [SerializeField] private string missionId;
    [SerializeField] private string assignedFlag;
    [SerializeField] private string completedFlag;
    [SerializeField, TextArea] private string vietnameseTitle;
    [SerializeField, TextArea] private string englishTitle;
    [SerializeField] private bool restoreWhenAssignedOnLoad = true;

    public string MissionId => !string.IsNullOrWhiteSpace(missionId) ? missionId : ResolveFallbackId();
    public bool ShouldRestoreWhenAssignedOnLoad => restoreWhenAssignedOnLoad;

    public bool MatchesAssignedFlag(string flagId)
    {
        return !string.IsNullOrWhiteSpace(assignedFlag) && assignedFlag == flagId;
    }

    public bool MatchesCompletedFlag(string flagId)
    {
        return !string.IsNullOrWhiteSpace(completedFlag) && completedFlag == flagId;
    }

    public bool IsAssigned(FlagManager flagManager)
    {
        if (flagManager == null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(assignedFlag) && flagManager.HasFlag(assignedFlag);
    }

    public bool IsCompleted(FlagManager flagManager)
    {
        return flagManager != null
            && !string.IsNullOrWhiteSpace(completedFlag)
            && flagManager.HasFlag(completedFlag);
    }

    public string GetTitle(LocalizationManager localizationManager)
    {
        if (localizationManager == null)
        {
            return !string.IsNullOrWhiteSpace(vietnameseTitle) ? vietnameseTitle : MissionId;
        }

        return localizationManager.GetDialogueText(vietnameseTitle, englishTitle);
    }

    public bool IsSameMission(MissionDefinition other)
    {
        return other != null && MissionId == other.MissionId;
    }

    private string ResolveFallbackId()
    {
        if (!string.IsNullOrWhiteSpace(assignedFlag))
        {
            return assignedFlag;
        }

        if (!string.IsNullOrWhiteSpace(completedFlag))
        {
            return completedFlag;
        }

        return !string.IsNullOrWhiteSpace(vietnameseTitle) ? vietnameseTitle : nameof(MissionDefinition);
    }
}
