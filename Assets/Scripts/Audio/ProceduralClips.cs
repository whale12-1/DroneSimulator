using UnityEngine;

// Процедурные короткие звуки без внешних ассетов. Используются для UI
// (клик по кнопкам) и для взаимодействий в бункере (сесть/встать).
public static class ProceduralClips
{
    private const int SampleRate = 44100;

    private static AudioClip click;
    private static AudioClip thud;

    public static AudioClip Click()
    {
        if (click != null) return click;

        int count = Mathf.RoundToInt(SampleRate * 0.05f);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = Mathf.Exp(-t * 90f);
            float value = Mathf.Sin(2f * Mathf.PI * 1450f * t) * 0.6f
                        + Mathf.Sin(2f * Mathf.PI * 2100f * t) * 0.3f;

            data[i] = value * envelope * 0.5f;
        }

        click = AudioClip.Create("UI_Click", count, 1, SampleRate, false);
        click.SetData(data, 0);
        return click;
    }

    public static AudioClip Thud()
    {
        if (thud != null) return thud;

        int count = Mathf.RoundToInt(SampleRate * 0.18f);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float envelope = Mathf.Exp(-t * 26f);
            float value = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.7f
                        + Mathf.Sin(2f * Mathf.PI * 160f * t) * 0.25f;

            data[i] = value * envelope * 0.6f;
        }

        thud = AudioClip.Create("Interaction_Thud", count, 1, SampleRate, false);
        thud.SetData(data, 0);
        return thud;
    }
}
