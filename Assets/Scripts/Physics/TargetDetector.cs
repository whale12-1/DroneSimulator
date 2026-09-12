using System.Collections.Generic;
using UnityEngine;

public class TargetDetector : MonoBehaviour
{
    [Header("Настройки Сенсора")]
    [SerializeField] private float detectionRadius = 300f; // Дальность поиска (м)
    [SerializeField] private float viewAngle = 70f;         // Угол обзора (град)
    [SerializeField] private LayerMask obstacleMask;        // Маска стен/земли для Raycast

    [Header("Найденные Цели")]
    public List<TargetObject> detectedTargets = new List<TargetObject>();

    private void Update()
    {
        ScanForTargets();
    }

    private void ScanForTargets()
    {
        detectedTargets.Clear();
        TargetObject[] allTargets = FindObjectsByType<TargetObject>(FindObjectsSortMode.None);

        foreach (var target in allTargets)
        {
            Vector3 dirToTarget = (target.TargetPosition - transform.position).normalized;
            float distanceToTarget = Vector3.Distance(transform.position, target.TargetPosition);

            if (distanceToTarget > detectionRadius) continue;

            // Проверка попадания в угол обзора
            if (Vector3.Angle(transform.forward, dirToTarget) < viewAngle / 2f)
            {
                // Проверка прямой видимости (Raycast)
                if (!Physics.Raycast(transform.position, dirToTarget, distanceToTarget, obstacleMask))
                {
                    detectedTargets.Add(target);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}