using UnityEngine;

/// <summary>
/// Иконка «Магазин» на рабочем столе ComputerOS. По клику запускает 3D-витрину
/// (DroneShopController.Open), а не плоское окно внутри монитора.
/// </summary>
public class DesktopShopIcon : MonoBehaviour
{
    private void Awake()
    {
        // Клик по иконке озвучиваем в шине UI, как остальные кнопки ОС.
        UISoundService.Attach(gameObject);
    }

    public void OnIconClicked()
    {
        DroneShopController shop = FindAnyObjectByType<DroneShopController>(FindObjectsInactive.Include);
        if (shop == null)
        {
            Debug.LogError("[DesktopShopIcon] В сцене нет DroneShopController.", this);
            return;
        }

        shop.Open();
    }
}
