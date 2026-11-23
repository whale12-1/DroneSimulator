using UnityEngine;

/// <summary>
/// Приводит машину состояний к Gameplay для сцен, которые запускают напрямую
/// из редактора (например PreparingDemo), минуя Boot и MainMenu.
/// Переходы идут строго по матрице GameStateMachine: Boot → MainMenu → Loading → Gameplay.
/// </summary>
public static class GameStateBootstrap
{
    /// <summary>
    /// Доводит FSM до Gameplay (если это возможно по матрице) и поднимает
    /// SettingsService, без которого не применяются настройки графики/звука/HUD.
    /// </summary>
    public static void EnsureGameplayState()
    {
        // При прямом старте сцены Boot не выполнялся — сервиса настроек нет.
        // В обычном цикле (через Boot) это no-op.
        SettingsService.EnsureInstance();

        GameState state = GameStateMachine.Current;

        if (state == GameState.Boot && GameStateMachine.CanTransitionTo(GameState.MainMenu))
        {
            GameStateMachine.TransitionTo(GameState.MainMenu);
            state = GameStateMachine.Current;
        }

        if (state == GameState.MainMenu && GameStateMachine.CanTransitionTo(GameState.Loading))
        {
            GameStateMachine.TransitionTo(GameState.Loading);
            state = GameStateMachine.Current;
        }

        if (state == GameState.Loading && GameStateMachine.CanTransitionTo(GameState.Gameplay))
            GameStateMachine.TransitionTo(GameState.Gameplay);
    }
}
