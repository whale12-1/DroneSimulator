using UnityEngine;

// WarheadType и ModuleType остаются там, где были (DroneConfig.cs / VehicleModule.cs),
// чтобы не ломать существующие ссылки.

public struct DamageData
{
    public float Amount;
    public Vector3 HitPoint;
    public Vector3 HitNormal;
    public Vector3 FlightDirection;
    public WarheadType Warhead;
    public ModuleType Module;
    public GameObject Source;

    public DamageData(float amount, Vector3 hitPoint, Vector3 hitNormal, Vector3 flightDir,
                      WarheadType warhead = WarheadType.HEAT,
                      ModuleType module = ModuleType.Armor,
                      GameObject source = null)
    {
        Amount = amount;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        FlightDirection = flightDir;
        Warhead = warhead;
        Module = module;
        Source = source;
    }
}

public interface IDamageable
{
    void ApplyDamage(in DamageData data);
}

public struct ExplosionEventData
{
    public Vector3 Position;
    public float Radius;
    public float Damage;
    public WarheadType Warhead;
    public GameObject Source;

    public ExplosionEventData(Vector3 position, float radius, float damage, WarheadType warhead, GameObject source = null)
    {
        Position = position; Radius = radius; Damage = damage; Warhead = warhead; Source = source;
    }
}