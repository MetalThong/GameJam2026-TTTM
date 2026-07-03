using UnityEngine;

public readonly struct FlagChangedEvent
{
    public readonly string FlagId;
    public readonly bool Value;

    public FlagChangedEvent(string flagId, bool value)
    {
        FlagId = flagId;
        Value = value;
    }
}

public readonly struct FlagsLoadedEvent
{
    public readonly int Count;

    public FlagsLoadedEvent(int count)
    {
        Count = count;
    }
}
