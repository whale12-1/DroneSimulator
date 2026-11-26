using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Магазин-витрина в бункере. Открывается иконкой «Магазин» на рабочем столе
/// ComputerOS: рабочий стол прячется, включается DroneShopCamera над деревянным
/// столом, над столешницей появляется витринная копия дрона (без физики, камер и
/// скриптов полёта), а HUD DroneShopUI показывает ТТХ и блок реальной теории.
/// Выход (Esc / «Назад») полностью уничтожает копию и возвращает рабочий стол.
/// </summary>
public class DroneShopController : MonoBehaviour
{
    [Header("Сцена витрины")]
    [SerializeField] private Camera shopCamera;
    [SerializeField] private Light shopLight;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private DroneShopUI shopUI;

    [Header("Интеграция с компьютером")]
    [SerializeField] private ComputerUIController computerUI;

    [Tooltip("HUD часов бункера — на время витрины скрывается, чтобы не перекрывал заголовок")]
    [SerializeField] private GameObject clockHud;

    [Header("Каталог (порядок листания)")]
    [SerializeField] private DroneShopEntry[] drones = new DroneShopEntry[0];

    private Camera playerCamera;
    private GameObject droneRoot;
    private DroneShopShowcase showcase;
    private int currentIndex;
    private float lastOpenTime;

    public bool IsOpen { get; private set; }
    public float LastClosedTime { get; private set; } = -10f;
    public int CurrentIndex => currentIndex;
    public int DroneCount => drones != null ? drones.Length : 0;

    /// <summary>Текущая витринная копия (для проверок в Play Mode).</summary>
    public GameObject CurrentDrone => droneRoot;

    private void Awake()
    {
        playerCamera = Camera.main;
        ResolveReferences();

        if (shopCamera != null) shopCamera.enabled = false;
        if (shopLight != null) shopLight.enabled = false;

        if (shopUI != null)
        {
            shopUI.PrevRequested += Previous;
            shopUI.NextRequested += Next;
            shopUI.ResetRequested += ResetView;
            shopUI.CloseRequested += CloseShop;
            shopUI.Hide();
        }

        BunkerModalStack.Register(this);
    }

    private void OnDestroy()
    {
        BunkerModalStack.Unregister(this);

        if (shopUI != null)
        {
            shopUI.PrevRequested -= Previous;
            shopUI.NextRequested -= Next;
            shopUI.ResetRequested -= ResetView;
            shopUI.CloseRequested -= CloseShop;
        }

        DestroyDrone();
    }

    private void OnDisable()
    {
        DestroyDrone();
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Time.unscaledTime - lastOpenTime < 0.2f) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseShop();
            return;
        }

        if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
        {
            Previous();
            return;
        }

        if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
        {
            Next();
            return;
        }

        if (keyboard.rKey.wasPressedThisFrame)
            ResetView();
    }

    // ------------------------------------------------------------ открытие / закрытие

    public void Open()
    {
        if (IsOpen) return;

        ResolveReferences();

        if (drones == null || drones.Length == 0)
        {
            Debug.LogError("[DroneShop] Каталог пуст: нечего показывать на витрине.", this);
            return;
        }

        IsOpen = true;
        lastOpenTime = Time.unscaledTime;

        if (computerUI != null) computerUI.HideForShop();
        if (clockHud != null) clockHud.SetActive(false);
        if (playerCamera != null) playerCamera.enabled = false;
        if (shopCamera != null) shopCamera.enabled = true;
        if (shopLight != null) shopLight.enabled = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        currentIndex = Mathf.Clamp(currentIndex, 0, drones.Length - 1);

        if (shopUI != null)
        {
            shopUI.Show();
            shopUI.ShowEntry(drones[currentIndex], currentIndex, drones.Length);
        }

        SpawnDrone(currentIndex);

        BunkerModalStack.Apply();
    }

    public void CloseShop()
    {
        if (!IsOpen) return;

        IsOpen = false;
        LastClosedTime = Time.unscaledTime;

        DestroyDrone();

        if (shopUI != null) shopUI.Hide();
        if (clockHud != null) clockHud.SetActive(true);
        if (shopCamera != null) shopCamera.enabled = false;
        if (shopLight != null) shopLight.enabled = false;
        if (playerCamera != null) playerCamera.enabled = true;

        if (computerUI != null) computerUI.ShowAfterShop();

        BunkerModalStack.Apply();
    }

    // ------------------------------------------------------------ листание каталога

    public void Next()
    {
        if (!IsOpen || DroneCount < 2) return;
        Select(currentIndex + 1);
    }

    public void Previous()
    {
        if (!IsOpen || DroneCount < 2) return;
        Select(currentIndex - 1);
    }

    public void Select(int index)
    {
        if (drones == null || drones.Length == 0) return;

        int wrapped = ((index % drones.Length) + drones.Length) % drones.Length;
        if (wrapped == currentIndex && droneRoot != null) return;

        currentIndex = wrapped;
        SpawnDrone(currentIndex);

        if (shopUI != null) shopUI.ShowEntry(drones[currentIndex], currentIndex, drones.Length);
    }

    public void ResetView()
    {
        if (showcase != null) showcase.ResetView();
    }

    private void ResolveReferences()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (computerUI == null) computerUI = FindAnyObjectByType<ComputerUIController>(FindObjectsInactive.Include);
        if (shopUI == null) shopUI = FindAnyObjectByType<DroneShopUI>(FindObjectsInactive.Include);
    }

    // ------------------------------------------------------------ витринная копия дрона

    private void SpawnDrone(int index)
    {
        DestroyDrone();

        if (drones == null || drones.Length == 0) return;

        DroneShopEntry entry = drones[Mathf.Clamp(index, 0, drones.Length - 1)];
        if (entry == null || entry.prefab == null)
        {
            Debug.LogError("[DroneShop] В карточке дрона не задан префаб.", this);
            return;
        }

        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position + Vector3.up * 0.3f;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        if (shopCamera != null)
        {
            Vector3 toCamera = shopCamera.transform.position - position;
            if (toCamera.sqrMagnitude > 0.0001f)
                rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        GameObject stage = new GameObject("DroneShopStage");
        stage.transform.SetParent(transform, false);
        stage.SetActive(false);
        stage.transform.SetPositionAndRotation(position, rotation * Quaternion.Euler(0f, entry.modelYawOffset, 0f));

        // Ключевой момент: префаб инстанцируется под неактивным родителем,
        // поэтому Awake скриптов полёта и камер не выполняется вообще.
        GameObject model = Instantiate(entry.prefab, stage.transform, false);
        model.name = entry.prefab.name + "_Showcase";

        // У части префабов корневой узел модели выключен (ReconDrone.prefab):
        // для витрины геометрия должна быть видимой.
        model.SetActive(true);

        StripForShowcase(model);
        FrameModel(model, stage.transform, entry);

        GameObject yawPivot = new GameObject("YawPitch");
        yawPivot.transform.SetParent(stage.transform, false);

        GameObject hoverRoot = new GameObject("Hover");
        hoverRoot.transform.SetParent(yawPivot.transform, false);

        Bounds modelBounds = ComputeRendererBounds(model);
        List<Vector3> rotorPositions = CollectRotorPositions(model, hoverRoot.transform, modelBounds);

        model.transform.SetParent(hoverRoot.transform, true);

        DroneShopPropellers propellers = hoverRoot.AddComponent<DroneShopPropellers>();
        propellers.Build(model.transform, hoverRoot.transform, rotorPositions, entry.rotorRadius);

        showcase = stage.AddComponent<DroneShopShowcase>();
        showcase.Initialize(yawPivot.transform, hoverRoot.transform);

        droneRoot = stage;
        stage.SetActive(true);
    }

    private void DestroyDrone()
    {
        showcase = null;

        if (droneRoot == null) return;

        droneRoot.SetActive(false);
        Destroy(droneRoot);
        droneRoot = null;
    }

    /// <summary>
    /// Убирает с копии всё, кроме геометрии и материалов: физику, коллайдеры,
    /// камеры, AudioListener/AudioSource, скрипты управления, источники света,
    /// партиклы и прочие компоненты. Оригинальный префаб не меняется.
    /// </summary>
    private static void StripForShowcase(GameObject model)
    {
        // 1) Сначала скрипты. UniversalAdditionalCameraData объявлен с
        //    [RequireComponent(typeof(Camera))] и мгновенно возвращает только что
        //    удалённую камеру, а DroneMotorAudio/EWAudio так же тянут AudioSource.
        //    Поэтому убираем поведение первым и сразу (DestroyImmediate), без
        //    отложенного кадра, в котором камера успела бы вернуться.
        Component[] components = model.GetComponentsInChildren<Component>(true);
        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component component = components[i];
            if (component is MonoBehaviour) DestroyImmediate(component);
        }

        // 2) Затем всё, кроме чистой геометрии и материалов: физика, коллайдеры,
        //    камеры, AudioListener/AudioSource, свет, партиклы, Canvas.
        //    Порядок — с конца: зависимые компоненты (RequireComponent) уходят
        //    раньше тех, от кого они зависят, иначе Unity пишет ошибки и
        //    отказывается удалять Rigidbody/DroneMotors.
        components = model.GetComponentsInChildren<Component>(true);
        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component component = components[i];
            if (component == null) continue;
            if (component is Transform) continue;
            if (component is MeshFilter || component is MeshRenderer || component is SkinnedMeshRenderer) continue;

            DestroyImmediate(component);
        }

        // 3) Страховка: пустые узлы камер и слушателей удаляем целиком, чтобы на
        //    витрине не оставалось «висящих» объектов сцены.
        foreach (Camera camera in model.GetComponentsInChildren<Camera>(true))
        {
            if (camera != null && camera.gameObject != model) DestroyImmediate(camera.gameObject);
        }

        foreach (AudioListener listener in model.GetComponentsInChildren<AudioListener>(true))
        {
            if (listener != null) DestroyImmediate(listener);
        }
    }

    /// <summary>Масштаб под displaySize и центрирование относительно точки спавна.</summary>
    private static void FrameModel(GameObject model, Transform stage, DroneShopEntry entry)
    {
        Bounds bounds = ComputeRendererBounds(model);
        float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

        if (maxDimension > 0.0001f && entry.displaySize > 0.0001f)
        {
            model.transform.localScale *= entry.displaySize / maxDimension;
            bounds = ComputeRendererBounds(model);
        }

        model.transform.position += stage.position - bounds.center;
    }

    private static List<Vector3> CollectRotorPositions(GameObject model, Transform hoverRoot, Bounds modelBounds)
    {
        List<Vector3> positions = new List<Vector3>();

        Transform[] nodes = model.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < nodes.Length; i++)
        {
            Transform node = nodes[i];
            if (node == null || node == model.transform) continue;
            if (!node.name.ToLowerInvariant().StartsWith("motor")) continue;
            positions.Add(hoverRoot.InverseTransformPoint(node.position));
        }

        if (positions.Count >= 3) return positions;

        // Запасной вариант: четыре угла над корпусом.
        positions.Clear();
        Vector3 center = modelBounds.center;
        float offsetX = modelBounds.extents.x * 0.62f;
        float offsetZ = modelBounds.extents.z * 0.62f;
        float height = modelBounds.center.y + modelBounds.extents.y * 0.28f;

        positions.Add(hoverRoot.InverseTransformPoint(new Vector3(center.x - offsetX, height, center.z - offsetZ)));
        positions.Add(hoverRoot.InverseTransformPoint(new Vector3(center.x + offsetX, height, center.z - offsetZ)));
        positions.Add(hoverRoot.InverseTransformPoint(new Vector3(center.x - offsetX, height, center.z + offsetZ)));
        positions.Add(hoverRoot.InverseTransformPoint(new Vector3(center.x + offsetX, height, center.z + offsetZ)));
        return positions;
    }

    /// <summary>
    /// Габариты видимой геометрии в мировых координатах. Берём Renderer.bounds:
    /// именно это рисуется на витрине, и такой расчёт не зависит от того, что
    /// mesh.bounds у части glTF-мешей занижен или завышен.
    /// </summary>
    private static Bounds ComputeRendererBounds(GameObject root)
    {
        bool hasBounds = false;
        Bounds result = new Bounds(root.transform.position, Vector3.zero);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer candidate = renderers[i];
            if (candidate == null) continue;

            if (!hasBounds)
            {
                result = candidate.bounds;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(candidate.bounds);
            }
        }

        return result;
    }
}
