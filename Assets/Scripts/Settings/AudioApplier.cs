using UnityEngine;

public static class AudioApplier
{
    private static bool warningLogged;

    public static void Apply(DroneSimulator.Settings.AudioSettings audio)
    {
        if (audio == null) return;

        if (!AudioBus.Ready)
        {
            AudioListener.volume = Mathf.Clamp01(audio.masterVolume);

            if (!warningLogged)
            {
                warningLogged = true;
                Debug.LogWarning("[AudioApplier] AudioMixer не найден: работает только общая громкость через AudioListener.volume.");
            }

            return;
        }

        warningLogged = false;
        // Мастер применяется через параметр микшера; AudioListener не дублирует его,
        // иначе громкость возводилась бы в квадрат.
        AudioListener.volume = 1f;

        AudioBus.SetVolume(AudioBus.MasterParameter, audio.masterVolume);
        AudioBus.SetVolume(AudioBus.MusicParameter, audio.musicVolume * MusicDuckFactor(audio));
        AudioBus.SetVolume(AudioBus.SfxParameter, audio.sfxVolume);
        AudioBus.SetVolume(AudioBus.MotorsParameter, audio.motorsVolume);
        AudioBus.SetVolume(AudioBus.EwParameter, audio.ewVolume);
        AudioBus.SetVolume(AudioBus.UiParameter, audio.uiVolume);
        AudioBus.SetVolume(AudioBus.VoiceParameter, audio.voiceVolume);
    }

    // duckOnMenus: приглушаем музыку вне геймплея (главное меню, пауза, дебриф).
    // Мастер-громкость и остальные шины не затрагиваются.
    private const float MenuMusicDuck = 0.35f;

    private static float MusicDuckFactor(DroneSimulator.Settings.AudioSettings audio)
    {
        if (audio == null || !audio.duckOnMenus) return 1f;
        return GameStateMachine.Current == GameState.Gameplay ? 1f : MenuMusicDuck;
    }
}
