using System;
using System.Collections.Generic;

[Serializable]
public class StoryFlagCondition
{
    public List<string> requiredFlags = new();
    public List<string> blockedFlags = new();

    public bool IsMet(StoryFlagStore flagStore)
    {
        for (int i = 0; i < requiredFlags.Count; i++)
        {
            string flag = requiredFlags[i];
            if (!flagStore.Has(flag))
            {
                return false;
            }
        }

        for (int i = 0; i < blockedFlags.Count; i++)
        {
            string flag = blockedFlags[i];
            if (flagStore.Has(flag))
            {
                return false;
            }
        }

        return true;
    }
}
