using UnityEngine;

public class VehicleDust : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private ParticleSystem dustParticles;

    [Header("Настройки")]
    [SerializeField] private float minSpeedForDust = 0.5f;
    [SerializeField] private float maxEmissionRate = 30f;

    private Vector3 lastPosition;
    private ParticleSystem.EmissionModule emissionModule;
    private bool isDustPlaying = false;

    private void Start()
    {
        if (dustParticles != null)
        {
            emissionModule = dustParticles.emission;
            lastPosition = transform.position;

            // Отключаем Play On Awake и глушим пыль на старте
            var main = dustParticles.main;
            main.playOnAwake = false;
            dustParticles.Stop();
        }
    }

    private void Update()
    {
        if (dustParticles == null) return;

        // Вычисляем скорость
        float currentSpeed = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
        lastPosition = transform.position;

        // Если машина едет
        if (currentSpeed > minSpeedForDust)
        {
            if (!isDustPlaying)
            {
                dustParticles.Play();
                isDustPlaying = true;
            }

            // Плавно наращиваем плотность пыли
            float normalizedSpeed = Mathf.Clamp01(currentSpeed / 15f);
            var rate = emissionModule.rateOverTime;
            rate.constant = Mathf.Lerp(5f, maxEmissionRate, normalizedSpeed);
            emissionModule.rateOverTime = rate;
        }
        // Если машина остановилась
        else
        {
            if (isDustPlaying)
            {
                dustParticles.Stop(); // Пыль догорит и исчезнет
                isDustPlaying = false;
            }
        }
    }
}