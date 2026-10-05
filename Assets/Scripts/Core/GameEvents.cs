using System;
using UnityEngine;

/// <summary>
/// ≈дина€ шина сигналов. ¬се системы общаютс€ через неЄ, а не через пр€мые ссылки.
/// —обыти€ намеренно приватные Ч дЄргать их можно только через Raise-методы.
/// </summary>
public static class GameEvents
{
    public static event Action<ExplosionEventData> ExplosionOccurred;
    public static event Action<Vector3, MonoBehaviour> DroneExploded;
    public static event Action<DamageData> DamageApplied;
    public static event Action<VehicleHealth> VehicleDestroyed;
    public static event Action<VehicleModule> ModuleDestroyed;
    public static event Action<GameObject> DroneSwitched;

    public static void RaiseExplosion(in ExplosionEventData data) => ExplosionOccurred?.Invoke(data);
    public static void RaiseDroneExploded(Vector3 pos, MonoBehaviour drone) => DroneExploded?.Invoke(pos, drone);
    public static void RaiseDamage(in DamageData data) => DamageApplied?.Invoke(data);
    public static void RaiseVehicleDestroyed(VehicleHealth v) => VehicleDestroyed?.Invoke(v);
    public static void RaiseModuleDestroyed(VehicleModule m) => ModuleDestroyed?.Invoke(m);
    public static void RaiseDroneSwitched(GameObject drone) => DroneSwitched?.Invoke(drone);

    /// <summary>—брос между сценами/тестами Ч статика не должна протекать.</summary>
    public static void ClearAll()
    {
        ExplosionOccurred = null;
        DroneExploded = null;
        DamageApplied = null;
        VehicleDestroyed = null;
        ModuleDestroyed = null;
        DroneSwitched = null;
    }
}