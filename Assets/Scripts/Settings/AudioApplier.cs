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
        AudioListener.volume = 1f;

        AudioBus.SetVolume(AudioBus.MasterParameter, audio.masterVolume);
        AudioBus.SetVolume(AudioBus.MusicParameter, audio.musicVolume);
        AudioBus.SetVolume(AudioBus.SfxParameter, audio.sfxVolume);
        AudioBus.SetVolume(AudioBus.MotorsParameter, audio.motorsVolume);
        AudioBus.SetVolume(AudioBus.EwParameter, audio.ewVolume);
        AudioBus.SetVolume(AudioBus.UiParameter, audio.uiVolume);
        AudioBus.SetVolume(AudioBus.VoiceParameter, audio.voiceVolume);
    }
}
