using UnityEngine;

public class TargetObject : MonoBehaviour
{
    [Header("Параметры Цели")]
    public string targetName = "Техника";
    public bool isHostile = true;

    // Точка прицеливания (центр объекта)
    public Vector3 TargetPosition => transform.position + Vector3.up * 0.5f;
}