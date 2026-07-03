using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StoryFlagAction
{
    public List<string> setFlags = new();
    public List<string> unsetFlags = new();

    public void Execute(FlagManager flagManager)
    {
        if (flagManager == null)
        {
            return;
        }

        for (int i = 0; i < setFlags.Count; i++)
        {
            flagManager.SetFlag(setFlags[i], true);
        }

        for (int i = 0; i < unsetFlags.Count; i++)
        {
            flagManager.SetFlag(unsetFlags[i], false);
        }
    }
}
