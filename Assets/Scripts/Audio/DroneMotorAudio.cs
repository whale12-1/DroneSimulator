using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DroneMotorAudio : MonoBehaviour
{
    [SerializeField] private DroneMotors motors;
    [SerializeField] private DroneFlightController flightController;
    [SerializeField] private AudioClip motorLoop;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.35f;
    [SerializeField] private float minPitch = 0.75f;
    [SerializeField] private float maxPitch = 1.6f;
    [SerializeField] private float idleFactor = 0.25f;
    [SerializeField] private float smoothing = 3f;

    private static AudioClip cachedLoop;

    private AudioSource source;
    private float smoothedThrust;

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        if (motors == null) motors = GetComponentInParent<DroneMotors>();
        if (flightController == null) flightController = GetComponentInParent<DroneFlightController>();

        source.clip = motorLoop != null ? motorLoop : BuildLoop();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 3f;
        source.maxDistance = 150f;
        source.outputAudioMixerGroup = AudioBus.Motors;
        source.volume = 0f;
        source.Play();
    }

    private void Update()
    {
        float target = 0f;

        if (flightController == null || flightController.isMotorStarted)
        {
            float thrust = motors != null ? Mathf.Clamp01(motors.CurrentThrust01) : 0f;
            target = idleFactor + (1f - idleFactor) * thrust;
        }

        smoothedThrust = Mathf.Lerp(smoothedThrust, target, Time.deltaTime * smoothing);

        source.volume = maxVolume * smoothedThrust;
        source.pitch = Mathf.Lerp(minPitch, maxPitch, smoothedThrust);
    }

    private static AudioClip BuildLoop()
    {
        if (cachedLoop != null) return cachedLoop;

        const int sampleRate = 44100;
        const float fundamental = 96f;

        float[] harmonics = { 1f, 2f, 3f, 5f };
        float[] amplitudes = { 0.5f, 0.3f, 0.15f, 0.07f };

        int sampleCount = sampleRate;
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            float value = 0f;

            for (int h = 0; h < harmonics.Length; h++)
                value += Mathf.Sin(2f * Mathf.PI * fundamental * harmonics[h] * time) * amplitudes[h];

            float wobble = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 3f * time);
            data[i] = value * wobble * 0.5f;
        }

        cachedLoop = AudioClip.Create("DroneMotorLoop", sampleCount, 1, sampleRate, false);
        cachedLoop.SetData(data, 0);
        return cachedLoop;
    }
}
