using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class EWAudio : MonoBehaviour
{
    [SerializeField] private EWReceiver receiver;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.3f;
    [SerializeField] private float smoothing = 6f;
    [SerializeField] private float lowPitch = 0.85f;
    [SerializeField] private float highPitch = 1.15f;

    private static AudioClip cachedNoise;

    private AudioSource source;
    private float smoothedJam;

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        if (receiver == null) receiver = GetComponent<EWReceiver>();
        if (receiver == null) receiver = GetComponentInParent<EWReceiver>();
        if (receiver == null) receiver = GetComponentInChildren<EWReceiver>();

        source.clip = BuildNoise();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.outputAudioMixerGroup = AudioBus.EW;
        source.volume = 0f;
        source.Play();
    }

    private void Update()
    {
        float target = receiver != null ? Mathf.Clamp01(receiver.jamIntensity) : 0f;
        smoothedJam = Mathf.Lerp(smoothedJam, target, Time.deltaTime * smoothing);

        source.volume = maxVolume * smoothedJam;
        source.pitch = Mathf.Lerp(highPitch, lowPitch, smoothedJam);
    }

    private static AudioClip BuildNoise()
    {
        if (cachedNoise != null) return cachedNoise;

        const int sampleRate = 22050;

        System.Random random = new System.Random(12345);
        float[] data = new float[sampleRate];
        float filtered = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            filtered = Mathf.Lerp(filtered, white, 0.35f);
            data[i] = filtered * 0.6f;
        }

        cachedNoise = AudioClip.Create("EWNoiseLoop", data.Length, 1, sampleRate, false);
        cachedNoise.SetData(data, 0);
        return cachedNoise;
    }
}
