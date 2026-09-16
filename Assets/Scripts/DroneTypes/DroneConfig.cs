using UnityEngine;

public enum WarheadType
{
    HEAT,         // Кумулятивная (ПГ-7В): Огромный точечный урон, отсутствие сплеша, чувствительность к углам
    HE_Frag,      // Осколочно-фугасная: Средний прямой урон, большой радиус поражения внешних модулей
    Thermobaric   // Термобарическая: Высокий урон в радиусе, игнорирование углов рикошета брони
}

[CreateAssetMenu(fileName = "NewDroneConfig", menuName = "FPV/Drone Config")]
public class DroneConfig : ScriptableObject
{
    [Header("Идентификация")]
    public string droneName = "FPV-7 PG-7VL";
    public WarheadType warheadType = WarheadType.HEAT;

    [Header("Параметры Урона")]
    [Tooltip("Прямой урон при точечном попадании")]
    public float directDamage = 250f;

    [Tooltip("Радиус взрывной волны и осколков")]
    public float splashRadius = 1.5f;

    [Tooltip("Максимальный урон по соседним модулям на границе радиуса")]
    public float splashDamage = 40f;

    [Header("Визуализация Взрыва")]
    public GameObject explosionPrefab;
    public AudioClip explosionSound;
}