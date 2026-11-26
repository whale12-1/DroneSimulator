using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Вращение винтов витринного дрона. В префабах бункера лопасти запечены в общий
/// меш, отдельных узлов «prop/rotor» нет, поэтому при отсутствии реальных винтов
/// в якорях моторов (MotorFl/Fr/Bl/Br) строятся процедурные роторы: две тонкие
/// лопасти + полупрозрачный диск сметания. Копия дрона — только геометрия,
/// физика и скрипты полёта на ней отсутствуют.
/// </summary>
public class DroneShopPropellers : MonoBehaviour
{
    [Header("Обороты")]
    [Tooltip("Угловая скорость ротора, градусов в секунду")]
    [SerializeField] private float spinSpeed = 2200f;

    private static Texture2D rotorDiscTexture;

    private readonly List<Transform> spinTargets = new List<Transform>();
    private readonly List<float> spinDirections = new List<float>();

    public int RotorCount => spinTargets.Count;

    /// <summary>
    /// modelRoot — витринная копия модели (для поиска реальных винтов),
    /// rotorLocalPositions — точки моторов в локальных координатах hoverRoot.
    /// </summary>
    public void Build(Transform modelRoot, Transform hoverRoot, IList<Vector3> rotorLocalPositions, float radius)
    {
        spinTargets.Clear();
        spinDirections.Clear();

        if (CollectRealPropellers(modelRoot))
            return;

        BuildProceduralRotors(hoverRoot, rotorLocalPositions, Mathf.Max(0.02f, radius));
    }

    private bool CollectRealPropellers(Transform modelRoot)
    {
        if (modelRoot == null) return false;

        Transform[] nodes = modelRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < nodes.Length; i++)
        {
            Transform node = nodes[i];
            if (node == null || node == modelRoot) continue;
            if (node.GetComponent<Renderer>() == null) continue;

            string nodeName = node.name.ToLowerInvariant();
            if (nodeName.Contains("prop") || nodeName.Contains("rotor") || nodeName.Contains("blade") || nodeName.Contains("винт"))
            {
                spinTargets.Add(node);
                spinDirections.Add(spinTargets.Count % 2 == 0 ? 1f : -1f);
            }
        }

        return spinTargets.Count > 0;
    }

    private void BuildProceduralRotors(Transform hoverRoot, IList<Vector3> rotorLocalPositions, float radius)
    {
        if (hoverRoot == null || rotorLocalPositions == null) return;

        Material discMaterial = CreateMaterial(GetRotorDiscTexture(), new Color(0.88f, 0.92f, 0.96f, 0.22f));
        Material bladeMaterial = CreateMaterial(Texture2D.whiteTexture, new Color(0.12f, 0.13f, 0.14f, 0.85f));

        for (int i = 0; i < rotorLocalPositions.Count; i++)
        {
            GameObject rotor = new GameObject("Rotor_" + (i + 1));
            rotor.transform.SetParent(hoverRoot, false);
            rotor.transform.localPosition = rotorLocalPositions[i];
            rotor.transform.localRotation = Quaternion.identity;

            // Диск сметания в горизонтальной плоскости.
            CreateQuad(rotor.transform, "Disc", radius * 2f, radius * 2f, discMaterial, Quaternion.Euler(90f, 0f, 0f), 0.001f);

            // Две лопасти крестом: дают чёткое «вращение» даже на малом размере.
            CreateQuad(rotor.transform, "Blade_A", radius * 1.94f, radius * 0.26f, bladeMaterial, Quaternion.Euler(90f, 0f, 0f), 0.002f);
            CreateQuad(rotor.transform, "Blade_B", radius * 1.94f, radius * 0.26f, bladeMaterial, Quaternion.Euler(90f, 90f, 0f), 0.002f);

            spinTargets.Add(rotor.transform);
            spinDirections.Add(i % 2 == 0 ? 1f : -1f);
        }
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        float step = spinSpeed * deltaTime;

        for (int i = 0; i < spinTargets.Count; i++)
        {
            Transform target = spinTargets[i];
            if (target == null) continue;
            target.Rotate(0f, step * spinDirections[i], 0f, Space.Self);
        }
    }

    // ------------------------------------------------------------ геометрия и материалы

    private static void CreateQuad(Transform parent, string quadName, float width, float height, Material material, Quaternion rotation, float yOffset)
    {
        GameObject quad = new GameObject(quadName);
        quad.transform.SetParent(parent, false);
        quad.transform.localRotation = rotation;
        quad.transform.localPosition = new Vector3(0f, yOffset, 0f);

        MeshFilter filter = quad.AddComponent<MeshFilter>();
        filter.sharedMesh = BuildQuadMesh(width, height);

        MeshRenderer quadRenderer = quad.AddComponent<MeshRenderer>();
        quadRenderer.sharedMaterial = material;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
        quadRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        quadRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private static Mesh BuildQuadMesh(float width, float height)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        Mesh mesh = new Mesh { name = "DroneShopQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-halfWidth, -halfHeight, 0f),
            new Vector3(-halfWidth, halfHeight, 0f),
            new Vector3(halfWidth, halfHeight, 0f),
            new Vector3(halfWidth, -halfHeight, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateMaterial(Texture2D texture, Color color)
    {
        // Sprites/Default всегда в сборке: unlit, без записи в глубину, без отсечения
        // грани — ровно то, что нужно для диска сметания и тонких лопастей.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        Material material = new Material(shader) { name = "DroneShopRotor" };
        material.mainTexture = texture;
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return material;
    }

    private static Texture2D GetRotorDiscTexture()
    {
        if (rotorDiscTexture != null) return rotorDiscTexture;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "DroneShopRotorDisc",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);

                float falloff = Mathf.Clamp01(1f - radius);
                falloff *= falloff;

                // Следы лопастей: четыре сектора, чтобы диск читался как вращение.
                float angle = Mathf.Atan2(ny, nx);
                float blades = Mathf.Abs(Mathf.Cos(angle * 2f));
                float alpha = falloff * (0.35f + 0.65f * blades);

                pixels[y * size + x] = new Color32(235, 240, 246, (byte)(Mathf.Clamp01(alpha) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        rotorDiscTexture = texture;
        return texture;
    }
}
