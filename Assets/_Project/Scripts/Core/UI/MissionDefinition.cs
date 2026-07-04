using System;
using System.Collections.Generic;
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
    [SerializeField] private bool showProgress;
    [SerializeField] private List<string> progressFlags = new();
    [SerializeField] private bool completeWhenProgressFull = true;

    public string MissionId => !string.IsNullOrWhiteSpace(missionId) ? missionId : ResolveFallbackId();
    public bool ShouldRestoreWhenAssignedOnLoad => restoreWhenAssignedOnLoad;
    public bool ShouldShowProgress => showProgress && ProgressTotal > 0;
    public int ProgressTotal => CountValidProgressFlags();

    public bool MatchesAssignedFlag(string flagId)
    {
        return !string.IsNullOrWhiteSpace(assignedFlag) && assignedFlag == flagId;
    }

    public bool MatchesCompletedFlag(string flagId)
    {
        return !string.IsNullOrWhiteSpace(completedFlag) && completedFlag == flagId;
    }

    public bool MatchesProgressFlag(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId) || progressFlags == null)
        {
            return false;
        }

        for (int i = 0; i < progressFlags.Count; i++)
        {
            if (progressFlags[i] == flagId)
            {
                return true;
            }
        }

        return false;
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

    public string GetDisplayTitle(LocalizationManager localizationManager, FlagManager flagManager)
    {
        string title = GetTitle(localizationManager);
        if (!ShouldShowProgress)
        {
            return title;
        }

        return $"{title} ({GetProgressCount(flagManager)}/{ProgressTotal})";
    }

    public int GetProgressCount(FlagManager flagManager)
    {
        if (flagManager == null || progressFlags == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < progressFlags.Count; i++)
        {
            string progressFlag = progressFlags[i];
            if (!string.IsNullOrWhiteSpace(progressFlag) && flagManager.HasFlag(progressFlag))
            {
                count++;
            }
        }

        return count;
    }

    public bool IsProgressFull(FlagManager flagManager)
    {
        int total = ProgressTotal;
        return total > 0 && GetProgressCount(flagManager) >= total;
    }

    public bool TryCompleteFromProgress(FlagManager flagManager)
    {
        if (!completeWhenProgressFull
            || string.IsNullOrWhiteSpace(completedFlag)
            || IsCompleted(flagManager)
            || !IsProgressFull(flagManager))
        {
            return false;
        }

        flagManager.SetFlag(completedFlag, true);
        return true;
    }

    public bool IsSameMission(MissionDefinition other)
    {
        return other != null && MissionId == other.MissionId;
    }

    private int CountValidProgressFlags()
    {
        if (progressFlags == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < progressFlags.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(progressFlags[i]))
            {
                count++;
            }
        }

        return count;
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
