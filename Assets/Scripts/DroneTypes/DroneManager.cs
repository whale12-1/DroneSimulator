using UnityEngine;

public class DroneManager : MonoBehaviour
{
    [Header("Объекты Дронов")]
    [SerializeField] private GameObject reconDrone;
    [SerializeField] private GameObject strikeDrone;

    private void Start()
    {
        // Старт с разведчика
        SelectRecon();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectRecon();
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectStrike();
    }

    public void SelectRecon()
    {
        if (reconDrone != null) reconDrone.SetActive(true);
        if (strikeDrone != null) strikeDrone.SetActive(false);
    }

    public void SelectStrike()
    {
        if (strikeDrone != null) strikeDrone.SetActive(true);
        if (reconDrone != null) reconDrone.SetActive(false);
    }
}