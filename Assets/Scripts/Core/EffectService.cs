using UnityEngine;

/// <summary>
/// Единая точка спавна визуальных и звуковых эффектов.
/// Готова к замене на пулинг без изменения вызывающего кода.
/// </summary>
public static class EffectService
{
    public static void Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return;
        Object.Instantiate(prefab, position, rotation);
    }

    public static void Spawn(GameObject prefab, Vector3 position)
        => Spawn(prefab, position, Quaternion.identity);

    public static void PlaySound(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}