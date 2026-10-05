// Core/GameStateMachine.cs
using System;
using System.Collections.Generic;
using UnityEngine;
public enum GameState
{
    Boot,
    MainMenu,
    MissionSelect,
    Loading,
    Gameplay,
    Paused,
    Debrief
}

public static class GameStateMachine
{
    public static GameState Current { get; private set; } = GameState.Boot;
    public static event Action<GameState, GameState> StateChanged;

    private static readonly Dictionary<GameState, GameState[]> allowed = new()
    {
        { GameState.Boot,          new[] { GameState.MainMenu } },
        { GameState.MainMenu,      new[] { GameState.MissionSelect, GameState.Loading, GameState.Boot } },
        { GameState.MissionSelect, new[] { GameState.MainMenu, GameState.Loading } },
        { GameState.Loading,       new[] { GameState.Gameplay, GameState.MainMenu } },
        { GameState.Gameplay,      new[] { GameState.Paused, GameState.Debrief, GameState.MainMenu } },
        { GameState.Paused,        new[] { GameState.Gameplay, GameState.MainMenu } },
        { GameState.Debrief,       new[] { GameState.MainMenu, GameState.Loading } },
    };

    public static bool CanTransitionTo(GameState next)
        => allowed.TryGetValue(Current, out var list) && System.Array.IndexOf(list, next) >= 0;

    public static void TransitionTo(GameState next)
    {
        if (!CanTransitionTo(next))
        {
            Debug.LogWarning($"[FSM] Недопустимый переход {Current} → {next}");
            return;
        }
        var prev = Current;
        Current = next;
        StateChanged?.Invoke(prev, next);
    }
}