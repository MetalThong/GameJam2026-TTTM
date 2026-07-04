using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SaveData
{
    public int Coin;
    public Vector3 PlayerPosition;
    public string PlayerSceneName;
    public bool HasPlayerState;
    public int PlayerForm = -1;
    public bool PlayerFacingRight = true;
    public List<PlayerScenePositionSaveEntry> PlayerScenePositions = new();
    public List<FlagSaveEntry> Flags = new();

    public void SetPlayerScenePosition(string sceneName, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        if (PlayerScenePositions == null)
        {
            PlayerScenePositions = new List<PlayerScenePositionSaveEntry>();
        }

        for (int i = 0; i < PlayerScenePositions.Count; i++)
        {
            PlayerScenePositionSaveEntry entry = PlayerScenePositions[i];
            if (entry != null && entry.SceneName == sceneName)
            {
                entry.Position = position;
                return;
            }
        }

        PlayerScenePositions.Add(new PlayerScenePositionSaveEntry
        {
            SceneName = sceneName,
            Position = position
        });
    }

    public bool TryGetPlayerScenePosition(string sceneName, out Vector3 position)
    {
        position = default;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        if (PlayerScenePositions != null)
        {
            for (int i = 0; i < PlayerScenePositions.Count; i++)
            {
                PlayerScenePositionSaveEntry entry = PlayerScenePositions[i];
                if (entry != null && entry.SceneName == sceneName)
                {
                    position = entry.Position;
                    return true;
                }
            }
        }

        if (PlayerSceneName == sceneName)
        {
            position = PlayerPosition;
            return true;
        }

        return false;
    }
}

[Serializable]
public sealed class PlayerScenePositionSaveEntry
{
    public string SceneName;
    public Vector3 Position;
}
