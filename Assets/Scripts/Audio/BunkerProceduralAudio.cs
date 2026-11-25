using UnityEngine;

// Процедурные звуки сюжетного бункера: вызов рации, радиошум и щелчок
// переключения волны. Нужны как безопасная заглушка, когда в Inspector
// не назначены внешние AudioClip — игра не пишет ошибок в консоль и
// радио/рация всё равно звучат для проверки механик.
public static class BunkerProceduralAudio
{
    private const int SampleRate = 44100;

    private static AudioClip ring;
    private static AudioClip radioStatic;
    private static AudioClip click;

    // Двухтоновый вызов рации: 8 коротких сигналов, затем пауза до конца
    // цикла. Закольцованный клип начинается и заканчивается тишиной, поэтому
    // стык петли не слышен.
    public static AudioClip Ring()
    {
        if (ring != null) return ring;

        const float loopSeconds = 2.6f;
        int count = Mathf.RoundToInt(SampleRate * loopSeconds);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float phase = t % 0.32f;
            float value = 0f;

            // Первые 1.6 с — трель, дальше тишина.
            if (t < 1.6f && phase < 0.2f)
            {
                int index = Mathf.FloorToInt(t / 0.32f);
                float frequency = index % 2 == 0 ? 880f : 1180f;
                float envelope = Mathf.Sin(Mathf.PI * (phase / 0.2f));
                value = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.45f;
            }

            data[i] = value;
        }

        ring = AudioClip.Create("Bunker_WalkieRing", count, 1, SampleRate, false);
        ring.SetData(data, 0);
        return ring;
    }

    // Радиошум для радио без треков: тихий низкочастотный шум, редкие
    // щелчки помех и слабый сетевой гул.
    public static AudioClip RadioStatic()
    {
        if (radioStatic != null) return radioStatic;

        const float loopSeconds = 3f;
        int count = Mathf.RoundToInt(SampleRate * loopSeconds);
        float[] data = new float[count];

        System.Random rng = new System.Random(1337);
        float smooth = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);

            // Однополюсный низкочастотный фильтр: шум становится «эфирным»,
            // а не резким белым.
            smooth += (white - smooth) * 0.22f;

            float crackle = rng.NextDouble() < 0.0025 ? white * 0.7f : 0f;
            float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.03f;

            data[i] = (smooth * 0.32f + crackle + hum) * 0.55f;
        }

        radioStatic = AudioClip.Create("Bunker_RadioStatic", count, 1, SampleRate, false);
        radioStatic.SetData(data, 0);
        return radioStatic;
    }

    // Короткий металлический щелчок ручки настройки.
    public static AudioClip Click()
    {
        if (click != null) return click;

        int count = Mathf.RoundToInt(SampleRate * 0.06f);
        float[] data = new float[count];

        System.Random rng = new System.Random(9001);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = Mathf.Exp(-t * 120f);
            float tone = Mathf.Sin(2f * Mathf.PI * 1900f * t) * 0.5f
                       + Mathf.Sin(2f * Mathf.PI * 2650f * t) * 0.3f;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.25f;

            data[i] = (tone + noise) * envelope * 0.5f;
        }

        click = AudioClip.Create("Bunker_RadioClick", count, 1, SampleRate, false);
        click.SetData(data, 0);
        return click;
    }
}
