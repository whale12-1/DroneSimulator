using UnityEngine;

public class FXManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnEnable()
    {
        GameEvents.OnDroneExploded += HandleExplosionFX;
    }

    private void OnDisable()
    {
        GameEvents.OnDroneExploded -= HandleExplosionFX;
    }

    private void HandleExplosionFX(Vector3 point, InterceptorCollision drone)
    {
        GameObject prefab = drone.Config != null ? drone.Config.explosionPrefab : null;
        AudioClip sound = drone.Config != null ? drone.Config.explosionSound : null;

        if (prefab != null) Instantiate(prefab, point, Quaternion.identity);
        if (sound != null) AudioSource.PlayClipAtPoint(sound, point, 1.0f);
    }
}
