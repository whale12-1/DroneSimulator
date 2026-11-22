using System.Collections.Generic;
using UnityEngine;

public class TargetDetector : MonoBehaviour
{
    [SerializeField] private DroneConfig config;
    [SerializeField] private LayerMask obstacleMask;

    // FindObjectsByType слишком дорог для вызова каждый кадр: кэшируем список целей.
    private const float TargetCacheRefreshInterval = 0.5f;

    public List<TargetObject> detectedTargets = new List<TargetObject>();

    private TargetObject[] cachedTargets = System.Array.Empty<TargetObject>();
    private float nextCacheRefreshTime;

    private void Update()
    {
        if (config == null) return;

        if (Time.time >= nextCacheRefreshTime)
        {
            nextCacheRefreshTime = Time.time + TargetCacheRefreshInterval;
            RefreshTargetCache();
        }

        ScanForTargets();
    }

    private void RefreshTargetCache()
    {
        cachedTargets = FindObjectsByType<TargetObject>(FindObjectsInactive.Exclude);
    }

    private void ScanForTargets()
    {
        detectedTargets.Clear();

        for (int i = 0; i < cachedTargets.Length; i++)
        {
            TargetObject target = cachedTargets[i];
            if (target == null) continue;

            Vector3 dirToTarget = (target.TargetPosition - transform.position).normalized;
            float distanceToTarget = Vector3.Distance(transform.position, target.TargetPosition);

            if (distanceToTarget > config.detectionRadius) continue;

            if (Vector3.Angle(transform.forward, dirToTarget) < config.viewAngle / 2f)
            {
                if (!Physics.Raycast(transform.position, dirToTarget, distanceToTarget, obstacleMask))
                {
                    detectedTargets.Add(target);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (config == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, config.detectionRadius);
    }
}