using UnityEngine;
using System.Collections;

public class HimarsStrike : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private VehiclePatrol himarsPatrol;
    [SerializeField] private VehicleHealth himarsHealth; // <--- ДОБАВИЛИ

    [Header("Стрельба")]
    [SerializeField] private GameObject rocketVFX;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private int rocketsCount = 4;
    [SerializeField] private float delayBetweenRockets = 1.5f;
    [SerializeField] private float waitBeforeFiring = 3f;

    [Header("Отступление")]
    [SerializeField] private float himarsRetreatSpeed = 15f;

    private bool isFiring = false;

    private void Update()
    {
        // Если патруль закончен и мы еще не стреляем
        if (himarsPatrol != null && himarsPatrol.IsFinished && !isFiring)
        {
            StartCoroutine(FireAndRetreatSequence());
        }
    }

    private IEnumerator FireAndRetreatSequence()
    {
        isFiring = true;
        yield return new WaitForSeconds(waitBeforeFiring);

        for (int i = 0; i < rocketsCount; i++)
        {
            // ЗАЩИТА: Если Хаймерс мертв, прекращаем корутину
            if (himarsHealth != null && himarsHealth.currentHealth <= 0) yield break;

            if (rocketVFX != null && spawnPoint != null)
            {
                GameObject rocket = Instantiate(rocketVFX, spawnPoint.position, spawnPoint.rotation);
                Destroy(rocket, 5f);
            }
            yield return new WaitForSeconds(delayBetweenRockets);
        }

        // ЗАЩИТА: Если он выжил после залпа, только тогда он уезжает
        if (himarsHealth != null && himarsHealth.currentHealth > 0)
        {
            if (himarsPatrol != null) himarsPatrol.StartRetreat(himarsRetreatSpeed);
        }
    }
}