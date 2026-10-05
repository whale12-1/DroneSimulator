using UnityEngine;

public class DroppedBomb : MonoBehaviour
{
    [Header("Параметры взрыва")]
    [SerializeField] private float explosionRadius = 6f;
    [SerializeField] private float damage = 150f;
    [SerializeField] private float explosionForce = 700f;
    [SerializeField] private GameObject explosionVFX;

    private bool isExploded;

    private void OnCollisionEnter(Collision collision)
    {
        if (isExploded) return;
        Explode();
    }

    private void Explode()
    {
        isExploded = true;

        EffectService.Spawn(explosionVFX, transform.position);
        ExplosionService.ApplySplash(transform.position, explosionRadius, damage, WarheadType.HE_Frag, source: gameObject);

        // Разброс физических обломков
        foreach (var hit in Physics.OverlapSphere(transform.position, explosionRadius))
        {
            Rigidbody rb = hit.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);
            }
        }

        GameEvents.RaiseExplosion(new ExplosionEventData(transform.position, explosionRadius, damage, WarheadType.HE_Frag, gameObject));
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}