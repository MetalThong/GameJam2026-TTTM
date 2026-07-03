using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SaveData
{
    public int Coin;
    public Vector3 PlayerPosition;
    public List<FlagSaveEntry> Flags = new();
}
