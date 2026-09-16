using UnityEngine;

public class EWEmitter : MonoBehaviour
{
    [Header("Параметры РЭБ")]
    [Tooltip("Радиус купола РЭБ в метрах")]
    public float jamRadius = 150f;

    [Tooltip("Активен ли передатчик")]
    public bool isActive = true;

    private void OnDrawGizmosSelected()
    {
        // Визуализация сферы РЭБ в редакторе (красный купол)
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position, jamRadius);
    }
}