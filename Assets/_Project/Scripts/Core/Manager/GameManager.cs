using System;
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Booting;
    public event Action<GameState, GameState> StateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Initialize()
    {
        SetState(GameState.Booting);
    }

    public void SetState(GameState gameState)
    {
        if (CurrentState == gameState)
        {
            return;
        }

        GameState previousState = CurrentState;
        CurrentState = gameState;
        StateChanged?.Invoke(previousState, CurrentState);
    }

    public void Pause()
    {
        if (CurrentState == GameState.Playing)
        {
            SetState(GameState.Paused);
        }
    }

    public void Resume()
    {
        if (CurrentState == GameState.Paused)
        {
            SetState(GameState.Playing);
        }
    }

    public void RestartRun()
    {
        SetState(GameState.Booting);
    }

    public void QuitToMenu()
    {
        SetState(GameState.MainMenu);
    }
}
