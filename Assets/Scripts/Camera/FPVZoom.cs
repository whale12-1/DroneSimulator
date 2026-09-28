using UnityEngine;

public class FPVZoom : MonoBehaviour
{
    private Camera cam;

    [Header("”ровни Zoom (FOV в градусах)")]
    // 100∞ = Ўирокий угол (1x), 50∞ = 2x, 25∞ = 4x, 10∞ = 10x
    [SerializeField] private float[] zoomLevels = new float[] { 100f, 50f, 25f, 10f };
    [SerializeField] private float zoomSpeed = 10f;

    private int currentZoomIndex = 0; // Ќачальный уровень (100∞)

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = zoomLevels[0];
        }
    }

    private void Update()
    {
        // 'Z' Ч ”величение кратности (уменьшение FOV)
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (currentZoomIndex < zoomLevels.Length - 1)
            {
                currentZoomIndex++;
            }
        }

        // 'X' Ч ”меньшение кратности (увеличение FOV)
        if (Input.GetKeyDown(KeyCode.X))
        {
            if (currentZoomIndex > 0)
            {
                currentZoomIndex--;
            }
        }

        // ѕлавный переход к целевому FOV
        float targetFOV = zoomLevels[currentZoomIndex];
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
    }
}