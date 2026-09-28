using System.Collections.Generic;
using UnityEngine;

public class BulletParticleCollision : MonoBehaviour
{
    [Header("Эффекты")]
    [SerializeField] private GameObject explosionEffectPrefab; // Префаб взрыва дрона в воздухе (опционально)

    private ParticleSystem partSystem;
    private List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();

    private void Awake()
    {
        partSystem = GetComponent<ParticleSystem>();
    }

    private void OnParticleCollision(GameObject other)
    {
        // ТЕСТОВЫЙ ЛОГ: сработает при ЛЮБОМ касании частицы с коллайдером
        //Debug.Log($"<color=yellow>[PHYSICS HIT]</color> Частица коснулась: {other.name} | Tag: {other.tag} | Layer: {LayerMask.LayerToName(other.layer)}");//

        int numCollisionEvents = partSystem.GetCollisionEvents(other, collisionEvents);

        for (int i = 0; i < numCollisionEvents; i++)
        {
            Transform droneRoot = other.transform.root;

            if (other.CompareTag("Drone") || droneRoot.CompareTag("Drone"))
            {
                Vector3 hitPosition = collisionEvents[i].intersection;

                if (explosionEffectPrefab != null)
                {
                    Instantiate(explosionEffectPrefab, hitPosition, Quaternion.identity);
                }

                //Debug.Log($"<color=red>[DESTROYED]</color> FPV-дрон {droneRoot.name} уничтожен!");//
                Destroy(droneRoot.gameObject);
                break;
            }
        }
    }
}