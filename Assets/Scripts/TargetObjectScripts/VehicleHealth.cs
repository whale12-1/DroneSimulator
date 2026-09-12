using UnityEngine;

public class VehicleHealth : MonoBehaviour
{
    public float totalHealth = 300f;
    public GameObject explosionVFX;
    public Transform turretTransform; // Ссылка на объект башни для отрыва

    private bool isDestroyed = false;

    public void OnModuleHit(ModuleType module, float damage, bool isModuleDestroyed)
    {
        if (isDestroyed) return;

        totalHealth -= damage;

        // Детонация БК приводит к мгновенному катастрофическому уничтожению
        if (module == ModuleType.AmmoRack && isModuleDestroyed)
        {
            DestroyVehicle(catastrophicAmmoExplosion: true);
            return;
        }

        if (totalHealth <= 0)
        {
            DestroyVehicle(catastrophicAmmoExplosion: false);
        }
    }

    private void DestroyVehicle(bool catastrophicAmmoExplosion)
    {
        isDestroyed = true;

        if (explosionVFX != null)
            Instantiate(explosionVFX, transform.position, Quaternion.identity);

        if (catastrophicAmmoExplosion && turretTransform != null)
        {
            // Эффектный отрыв башни: отсоединяем и добавляем импульс вверх
            turretTransform.SetParent(null);
            Rigidbody turretRb = turretTransform.gameObject.AddComponent<Rigidbody>();
            turretRb.mass = 1500f;
            turretRb.AddForce(Vector3.up * 12000f + Random.insideUnitSphere * 3000f, ForceMode.Impulse);
            turretRb.AddTorque(Random.insideUnitSphere * 5000f, ForceMode.Impulse);
        }

        Debug.Log("Техника полностью выведена из строя!");
    }
}