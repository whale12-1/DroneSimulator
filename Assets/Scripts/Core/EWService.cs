using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Реестр активных РЭБ-передатчиков.
/// Убирает FindObjectsByType из Update() в EWReceiver.
/// </summary>
public static class EWService
{
    private static readonly List<EWEmitter> emitters = new List<EWEmitter>();

    public static void Register(EWEmitter e)
    {
        if (e != null && !emitters.Contains(e)) emitters.Add(e);
    }

    public static void Unregister(EWEmitter e)
    {
        emitters.Remove(e);
    }

    /// <summary>Максимальная интенсивность помех в точке. 0..1</summary>
    public static float SampleJamAt(Vector3 position)
    {
        float max = 0f;
        for (int i = emitters.Count - 1; i >= 0; i--)
        {
            var e = emitters[i];
            if (e == null) { emitters.RemoveAt(i); continue; }
            if (!e.isActive) continue;

            float dist = Vector3.Distance(position, e.transform.position);
            if (dist >= e.jamRadius) continue;

            float factor = 1f - dist / e.jamRadius;
            if (factor > max) max = factor;
        }
        return max;
    }
}