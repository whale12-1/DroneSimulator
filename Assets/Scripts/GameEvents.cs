using System;
using UnityEngine;

public static class GameEvents
{
    // Передаем позицию взрыва и сам дрон (чтобы слушатели могли прочитать его DroneConfig)
    public static Action<Vector3, InterceptorCollision> OnDroneExploded;
}