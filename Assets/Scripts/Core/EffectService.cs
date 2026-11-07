using UnityEngine;

public static class EffectService
{
    private const int PoolSize = 16;
    private const string PoolName = "EffectAudioPool";

    private static AudioSource[] pool;
    private static Transform poolRoot;
    private static int cursor;

    public static void Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return;
        Object.Instantiate(prefab, position, rotation);
    }

    public static void Spawn(GameObject prefab, Vector3 position)
    {
        Spawn(prefab, position, Quaternion.identity);
    }

    public static void PlaySound(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        EnsurePool();

        if (pool == null)
        {
            AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volume));
            return;
        }

        AudioSource source = NextSource();
        source.transform.position = position;
        source.clip = clip;
        source.pitch = 1f;
        source.volume = Mathf.Clamp01(volume);
        source.Play();
    }

    private static void EnsurePool()
    {
        if (pool != null && pool.Length > 0 && pool[0] != null) return;

        GameObject root = new GameObject(PoolName);
        Object.DontDestroyOnLoad(root);

        poolRoot = root.transform;
        pool = new AudioSource[PoolSize];
        cursor = 0;

        for (int i = 0; i < PoolSize; i++)
        {
            GameObject voice = new GameObject("Voice" + i);
            voice.transform.SetParent(poolRoot, false);

            AudioSource source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 5f;
            source.maxDistance = 300f;
            source.outputAudioMixerGroup = AudioBus.Sfx;

            pool[i] = source;
        }
    }

    private static AudioSource NextSource()
    {
        for (int i = 0; i < pool.Length; i++)
        {
            int index = (cursor + i) % pool.Length;
            if (pool[index].isPlaying) continue;

            cursor = (index + 1) % pool.Length;
            return pool[index];
        }

        AudioSource fallback = pool[cursor];
        cursor = (cursor + 1) % pool.Length;
        return fallback;
    }
}
