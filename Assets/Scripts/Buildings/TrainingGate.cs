using UnityEngine;

public class TrainingGate : MonoBehaviour
{
    // Статическое событие, чтобы менеджер обучения мог на него подписаться
    public static event System.Action OnGatePassed;

    private bool isPassed = false;
    private Renderer meshRenderer;

    private void Start()
    {
        meshRenderer = GetComponent<Renderer>();
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<Renderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isPassed) return;

        // Проверяем, что пролетел именно дрон
        if (other.CompareTag("Drone") || other.GetComponentInParent<DroneFlightController>() != null)
        {
            isPassed = true;
            OnGatePassed?.Invoke();

            // Меняем цвет на зеленый для визуального фидбека
            if (meshRenderer != null)
                meshRenderer.material.color = new Color(0.2f, 1f, 0.2f, 0.5f); // Полупрозрачный зеленый
        }
    }
}