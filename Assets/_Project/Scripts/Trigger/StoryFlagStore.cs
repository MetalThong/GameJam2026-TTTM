using System.Collections.Generic;
using UnityEngine;

public class StoryFlagStore
{
    private readonly Dictionary<string, bool> _flags = new();

    public bool Has(string flagId)
    {
        return _flags.TryGetValue(flagId, out bool value) && value;
    }

    public void Set(string flagId, bool value = true)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            return;
        }

        _flags[flagId] = value;
    }

    public void Remove(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            return;
        }

        _flags.Remove(flagId);
    }

    public Dictionary<string, bool> ToDictionary()
    {
        return new Dictionary<string, bool>(_flags);
    }

    public List<FlagSaveEntry> ToSaveEntries()
    {
        List<FlagSaveEntry> entries = new(_flags.Count);

        foreach (KeyValuePair<string, bool> flag in _flags)
        {
            entries.Add(new FlagSaveEntry
            {
                Id = flag.Key,
                Value = flag.Value
            });
        }

        return entries;
    }

    public void LoadFrom(List<FlagSaveEntry> flags)
    {
        _flags.Clear();

        if (flags == null)
        {
            return;
        }

        foreach (FlagSaveEntry entry in flags)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
            {
                continue;
            }

            _flags[entry.Id] = entry.Value;
        }
    }
}
