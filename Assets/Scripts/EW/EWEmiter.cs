using UnityEngine;

public class EWEmitter : MonoBehaviour
{
    [Header("Параметры РЭБ")]
    public float jamRadius = 150f;
    public bool isActive = true;

    private void OnEnable() => EWService.Register(this);
    private void OnDisable() => EWService.Unregister(this);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position, jamRadius);
    }
}