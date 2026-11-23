using UnityEngine;

public class TrainingGate : MonoBehaviour
{
    // ����������� �������, ����� �������� �������� ��� �� ���� �����������
    public static event System.Action<TrainingGate> OnGatePassed;

    private Color originalColor = Color.white;
    private bool hasOriginalColor;

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

        if (other.CompareTag("Drone") || other.GetComponentInParent<DroneFlightController>() != null)
        {
            CacheOriginalColor();

            isPassed = true;
            SetPassedVisual(true);
            OnGatePassed?.Invoke(this);
        }
    }

    /// <summary>
    /// Возвращает кольцо в исходное состояние: пролёт снова вызовет OnGatePassed.
    /// Вызывается при входе в шаг «полоса препятствий» и после замены борта.
    /// </summary>
    public void ResetGate()
    {
        isPassed = false;
        SetPassedVisual(false);
    }

    private void CacheOriginalColor()
    {
        if (hasOriginalColor) return;

        if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer == null) return;

        originalColor = meshRenderer.material.color;
        hasOriginalColor = true;
    }

    private void SetPassedVisual(bool passed)
    {
        if (meshRenderer == null) return;

        if (passed)
        {
            meshRenderer.material.color = new Color(0.2f, 1f, 0.2f, 0.5f);
            return;
        }

        if (hasOriginalColor)
            meshRenderer.material.color = originalColor;
    }

}