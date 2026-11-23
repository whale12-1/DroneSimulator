using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Клики интерфейса: звук на 2D-AudioSource, маршрутизированный в шину UI,
// чтобы ползунок "Интерфейс" реально влиял на громкость.
public static class UISoundService
{
    private const string HostName = "UISoundHost";
    private const float ClickVolume = 0.45f;

    private static AudioSource source;
    private static readonly List<Button> boundButtons = new List<Button>();

    // Идемпотентно подписывает все Button в иерархии root на клик.
    // Безопасно вызывать повторно (например, после пересборки строк настроек).
    public static void Attach(GameObject root)
    {
        if (root == null) return;

        PruneDestroyed();

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null) continue;
            if (boundButtons.Contains(button)) continue;

            boundButtons.Add(button);
            button.onClick.AddListener(PlayClick);
        }
    }

    public static void PlayClick()
    {
        AudioSource audio = EnsureSource();
        if (audio == null) return;

        audio.PlayOneShot(ProceduralClips.Click(), ClickVolume);
    }

    private static AudioSource EnsureSource()
    {
        if (source != null) return source;

        GameObject host = new GameObject(HostName);
        Object.DontDestroyOnLoad(host);

        source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 1f;
        source.outputAudioMixerGroup = AudioBus.UI;

        // Клики UI должны быть слышны и при AudioListener.pause = true (пауза игры).
        source.ignoreListenerPause = true;

        return source;
    }

    // Кнопки, пересозданные вместе со сценой/строками настроек, ссылаются на
    // уничтоженные объекты — вычищаем их, чтобы список не рос бесконечно.
    private static void PruneDestroyed()
    {
        for (int i = boundButtons.Count - 1; i >= 0; i--)
        {
            if (boundButtons[i] == null) boundButtons.RemoveAt(i);
        }
    }
}
