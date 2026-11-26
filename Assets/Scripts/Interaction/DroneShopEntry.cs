using UnityEngine;

/// <summary>
/// Запись витрины магазина бункера: карточка дрона с ТТХ и блоком реальной теории.
/// Данные живут в сцене на DroneShopController; оригинальные префабы дронов не
/// изменяются — витринная копия собирается на лету (см. DroneShopController.SpawnDrone).
/// </summary>
[System.Serializable]
public class DroneShopEntry
{
    [Header("Карточка")]
    [Tooltip("Название борта на витрине")]
    public string displayName = "Дрон-разведчик";

    [Tooltip("Класс и назначение (подзаголовок карточки)")]
    public string role = "Recon / ISR БПЛА";

    [Header("Тактико-технические характеристики (по строке на пункт)")]
    public string[] specs = new string[0];

    [Header("Теория и практика применения")]
    [TextArea(8, 24)] public string theory = string.Empty;

    [Header("Витрина")]
    [Tooltip("Префаб дрона (показывается копия без физики, камер и скриптов полёта)")]
    public GameObject prefab;

    [Tooltip("Максимальный габарит модели на витрине, метры")]
    public float displaySize = 0.6f;

    [Tooltip("Радиус процедурного ротора в масштабе витрины, метры")]
    public float rotorRadius = 0.11f;

    [Tooltip("Доворот модели по Y, если нос смотрит не в камеру")]
    public float modelYawOffset = 0f;
}
