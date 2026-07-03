using System.Collections.Generic;
using UnityEngine;

public class FlagManager : MonoBehaviour, ISaveable
{
    public static FlagManager Instance { get; private set; }

    public StoryFlagStore Flags { get; private set; } = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool HasFlag(string flagId)
    {
        return Flags.Has(flagId);
    }

    public void SetFlag(string flagId, bool value = true)
    {
        if (string.IsNullOrWhiteSpace(flagId))
        {
            return;
        }

        bool oldValue = Flags.Has(flagId);
        Flags.Set(flagId, value);

        if (oldValue != value)
        {
            EventBus.Publish(new FlagChangedEvent(flagId, value));
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame();
        }
    }

    public void Load(List<FlagSaveEntry> flags)
    {
        Flags.LoadFrom(flags);
    }

    public void Save(SaveData data)
    {
        if (data == null)
        {
            return;
        }

        data.Flags = Flags.ToSaveEntries();
    }

    public void Load(SaveData data)
    {
        Load(data?.Flags);
    }
}
