using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps localization keys to translated strings for every supported language.
/// </summary>
[CreateAssetMenu(fileName = "SO_LocalizationTable", menuName = "GameJam/Localization/Localization Table")]
public sealed class LocalizationTable : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private string key;
        [SerializeField] private string vietnamese;
        [SerializeField] private string english;
        [SerializeField] private string cat;

        public string Key => key;
        public string Vietnamese => vietnamese;
        public string English => english;
        public string Cat => cat;

        public string Get(Language language)
        {
            return language switch
            {
                Language.English => !string.IsNullOrWhiteSpace(english) ? english : vietnamese,
                Language.Cat => !string.IsNullOrWhiteSpace(cat) ? cat : vietnamese,
                _ => vietnamese
            };
        }
    }

    [SerializeField] private List<Entry> entries = new();

    private Dictionary<string, Entry> _keyToEntry;

    public bool TryGet(string key, Language language, out string value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        _keyToEntry ??= BuildLookup();

        if (!_keyToEntry.TryGetValue(key, out Entry entry))
        {
            return false;
        }

        value = entry.Get(language);
        return !string.IsNullOrEmpty(value);
    }

    private Dictionary<string, Entry> BuildLookup()
    {
        Dictionary<string, Entry> lookup = new();

        foreach (Entry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
            {
                continue;
            }

            lookup[entry.Key] = entry;
        }

        return lookup;
    }
}
