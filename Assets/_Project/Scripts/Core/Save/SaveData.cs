using System;
using UnityEngine;

[Serializable]
public sealed class SaveData
{
    public int coin;
    public Vector3 playerPosition;

    public SaveData()
    {
        coin = 0;
        playerPosition = Vector3.zero;
    }
}
