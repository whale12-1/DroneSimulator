using UnityEngine;

public class DroneManager : MonoBehaviour
{
    [Header("Объекты Дронов")]
    [SerializeField] private GameObject reconDrone;
    [SerializeField] private GameObject strikeDrone;
    [SerializeField] private GameObject strikeDrone_2;
    [SerializeField] private GameObject dropDrone;
    private void Start()
    {
        // Старт с разведчика
        SelectRecon();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectRecon();
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectStrike();
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectStrike1();
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectDropDrone();
    }

    public void SelectRecon()
    {
        if (reconDrone != null) reconDrone.SetActive(true);
        if (strikeDrone != null) strikeDrone.SetActive(false);
        if (strikeDrone_2 != null) strikeDrone_2.SetActive(false);
        if(dropDrone!=null) dropDrone.SetActive(false);
    }

    public void SelectStrike()
    {
        if (strikeDrone != null) strikeDrone.SetActive(true);
        if (reconDrone != null) reconDrone.SetActive(false);
        if (strikeDrone_2 != null) strikeDrone_2.SetActive(false);
        if(dropDrone!= null) dropDrone.SetActive(false);
    }

    public void SelectStrike1()
    {
        if (strikeDrone_2 != null) strikeDrone_2.SetActive(true);
        if (strikeDrone != null) strikeDrone.SetActive(false);
        if (reconDrone != null) reconDrone.SetActive(false);
        if(dropDrone != null) dropDrone.SetActive(false);
    }

    public void SelectDropDrone()
    {
        if (strikeDrone_2 != null) strikeDrone_2.SetActive(false);
        if (strikeDrone != null) strikeDrone.SetActive(false);
        if (reconDrone != null) reconDrone.SetActive(false);
        if (dropDrone != null) dropDrone.SetActive(true);
    }
}