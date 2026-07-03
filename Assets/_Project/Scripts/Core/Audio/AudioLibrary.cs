using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_AudioLibrary", menuName = "GameJam/Audio/Audio Library")]
public sealed class AudioLibrary : ScriptableObject
{
    [SerializeField] private List<AudioEntry> entries = new();

    private Dictionary<string, AudioClip> _idToClip;

    public bool TryGetClip(string id, out AudioClip clip)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            clip = null;
            return false;
        }

        _idToClip ??= BuildLookup();
        return _idToClip.TryGetValue(id, out clip);
    }

    private Dictionary<string, AudioClip> BuildLookup()
    {
        Dictionary<string, AudioClip> lookup = new();

        foreach (AudioEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.Clip == null)
            {
                continue;
            }

            lookup[entry.Id] = entry.Clip;
        }

        return lookup;
    }
}
