using System.Collections.Generic;
using UnityEngine;

public class TargetDetector : MonoBehaviour
{
    [SerializeField] private DroneConfig config;
    [SerializeField] private LayerMask obstacleMask;

    public List<TargetObject> detectedTargets = new List<TargetObject>();

    private void Update()
    {
        if (config == null) return;
        ScanForTargets();
    }

    private void ScanForTargets()
    {
        detectedTargets.Clear();
        TargetObject[] allTargets = FindObjectsByType<TargetObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var target in allTargets)
        {
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