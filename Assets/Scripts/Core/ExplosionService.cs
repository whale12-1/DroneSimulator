using UnityEngine;

/// <summary>
/// Единый расчёт урона по площади.
/// Убирает дублирование между DroneCollision / InterceptorCollision / DroppedBomb.
/// </summary>
public static class ExplosionService
{
    public static void ApplySplash(Vector3 center, float radius, float baseDamage,
                                   WarheadType warhead,
                                   Collider ignoredCollider = null,
                                   GameObject source = null)
    {
        if (radius <= 0f || baseDamage <= 0f) return;

        Collider[] hits = Physics.OverlapSphere(center, radius);
        Transform sourceRoot = source != null ? source.transform.root : null;

        foreach (var col in hits)
        {
            if (col == ignoredCollider) continue;
            if (sourceRoot != null && col.transform.root == sourceRoot) continue;

            IDamageable target = ResolveDamageable(col);
            if (target == null) continue;

            float distance = Vector3.Distance(center, col.transform.position);
            float attenuation = Mathf.Clamp01(1f - distance / radius);
            float finalDamage = baseDamage * attenuation;

            if (finalDamage <= 1f) continue;

            Vector3 dir = (col.transform.position - center).normalized;
            target.ApplyDamage(new DamageData(finalDamage, col.transform.position, -dir, dir, warhead, ModuleType.Armor));
        }
    }

    private static IDamageable ResolveDamageable(Collider col)
    {
        // Приоритет: сам коллайдер → родители → дети
        if (col.TryGetComponent(out IDamageable d)) return d;
        d = col.GetComponentInParent<IDamageable>();
        if (d != null) return d;
        return col.GetComponentInChildren<IDamageable>();
    }
}