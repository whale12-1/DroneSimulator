using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ImpactVfxBuilder
{
    private const string VfxFolder = "Assets/Prefabs/VFX";
    private const string WeaponsFolder = "Assets/Prefabs/Weapons";
    private const string ArtFolder = "Assets/Art/VFX";
    private const string SfxFolder = "Assets/Audio/SFX";

    private const string ExplosionPrefabPath = "Assets/Prefabs/VFX/Explosion.prefab";
    private const string BombPrefabPath = "Assets/Prefabs/Weapons/Bomb.prefab";
    private const string ExplosionClipPath = "Assets/Audio/SFX/Explosion.wav";

    private const string GlowTexturePath = "Assets/Art/VFX/SoftGlow.png";
    private const string SmokeTexturePath = "Assets/Art/VFX/SmokePuff.png";

    private const string FlashMaterialPath = "Assets/Art/VFX/ParticleFlash.mat";
    private const string SmokeMaterialPath = "Assets/Art/VFX/ParticleSmoke.mat";
    private const string BombMaterialPath = "Assets/Art/VFX/BombMetal.mat";

    private static readonly string[] DroneConfigPaths =
    {
        "Assets/Settings/DroneConfigs/MavicReconConfig.asset",
        "Assets/Settings/DroneConfigs/SmallFpvHeatConfig.asset",
        "Assets/Settings/DroneConfigs/IntersectorConfig.asset"
    };

    [MenuItem("Tools/DroneSimulator/Impact/1. Build Explosion And Bomb", false, 200)]
    public static void BuildImpactPrefabs()
    {
        EnsureFolder(VfxFolder);
        EnsureFolder(WeaponsFolder);
        EnsureFolder(ArtFolder);
        EnsureFolder(SfxFolder);

        Material flashMaterial = BuildFlashMaterial();
        Material smokeMaterial = BuildSmokeMaterial();
        Material bombMaterial = BuildBombMaterial();

        GameObject explosionPrefab = BuildExplosionPrefab(flashMaterial, smokeMaterial);
        GameObject bombPrefab = BuildBombPrefab(bombMaterial, explosionPrefab);
        AudioClip explosionClip = BuildExplosionClip();

        int assigned = AssignToDroneConfigs(explosionPrefab, bombPrefab, explosionClip);
        int bombFields = AssignBombVfx(explosionPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=lime>[ImpactVfxBuilder]</color> Готово. Взрыв: {ExplosionPrefabPath}, бомба: {BombPrefabPath}, звук: {ExplosionClipPath}. Конфигов обновлено: {assigned}, DroppedBomb: {bombFields}.");
    }

    private static GameObject BuildExplosionPrefab(Material flashMaterial, Material smokeMaterial)
    {
        GameObject root = new GameObject("Explosion");

        GameObject flash = BuildFlash(root.transform, flashMaterial);
        GameObject smoke = BuildSmoke(root.transform, smokeMaterial);

        flash.transform.localPosition = Vector3.zero;
        smoke.transform.localPosition = Vector3.zero;

        PrefabUtility.SaveAsPrefabAsset(root, ExplosionPrefabPath);
        Object.DestroyImmediate(root);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPrefabPath);
        Debug.Log($"[ImpactVfxBuilder] Создан префаб взрыва: {ExplosionPrefabPath} (оранжевая вспышка + чёрный дым).");
        return prefab;
    }

    private static GameObject BuildFlash(Transform parent, Material material)
    {
        GameObject host = new GameObject("Flash");
        host.transform.SetParent(parent, false);

        ParticleSystem system = host.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.35f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.68f, 0.18f, 1f),
            new Color(1f, 0.94f, 0.66f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 256;
        main.gravityModifier = 0f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildFlashGradient());

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.4f, 1.35f),
            new Keyframe(1f, 1.9f)));

        ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortingFudge = -20f;
        renderer.maxParticleSize = 8f;

        return host;
    }

    private static GameObject BuildSmoke(Transform parent, Material material)
    {
        GameObject host = new GameObject("Smoke");
        host.transform.SetParent(parent, false);

        ParticleSystem system = host.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.6f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.10f, 0.09f, 0.09f, 0.92f),
            new Color(0.28f, 0.26f, 0.25f, 0.82f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 256;
        main.gravityModifier = -0.04f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildSmokeGradient());

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.35f, 1.1f),
            new Keyframe(1f, 2.3f)));

        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.35f;

        ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortingFudge = 20f;
        renderer.maxParticleSize = 14f;

        return host;
    }

    private static Gradient BuildFlashGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.72f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.12f), 0.35f),
                new GradientColorKey(new Color(0.35f, 0.16f, 0.06f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });

        return gradient;
    }

    private static Gradient BuildSmokeGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.22f, 0.20f, 0.19f), 0f),
                new GradientColorKey(new Color(0.09f, 0.09f, 0.10f), 0.6f),
                new GradientColorKey(new Color(0.05f, 0.05f, 0.06f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.12f),
                new GradientAlphaKey(0.55f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });

        return gradient;
    }

    private static GameObject BuildBombPrefab(Material bombMaterial, GameObject explosionPrefab)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.name = "Bomb";
        root.transform.localScale = new Vector3(0.14f, 0.22f, 0.14f);

        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = bombMaterial;

        CapsuleCollider collider = root.GetComponent<CapsuleCollider>();
        if (collider != null)
        {
            collider.radius = 0.55f;
            collider.height = 1.85f;
        }

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 8f;
        body.linearDamping = 0.05f;
        body.angularDamping = 0.15f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        DroppedBomb bomb = root.AddComponent<DroppedBomb>();
        SerializedObject serialized = new SerializedObject(bomb);
        SerializedProperty vfx = serialized.FindProperty("explosionVFX");
        if (vfx != null) vfx.objectReferenceValue = explosionPrefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BombPrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[ImpactVfxBuilder] Создан префаб бомбы: {BombPrefabPath} (цилиндр + Rigidbody + DroppedBomb).");
        return prefab;
    }

    private static AudioClip BuildExplosionClip()
    {
        const int sampleRate = 44100;
        const float duration = 1.6f;

        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        System.Random random = new System.Random(1439);
        float low = 0f;
        float mid = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);

            low = Mathf.Lerp(low, white, 0.045f);
            mid = Mathf.Lerp(mid, white, 0.22f);

            float body = low * 2.6f + mid * 0.65f;
            float rumble = Mathf.Sin(2f * Mathf.PI * (52f - 20f * Mathf.Clamp01(time)) * time) * 0.5f * Mathf.Exp(-time * 3.2f);
            float crack = white * Mathf.Exp(-time * 26f) * 0.85f;
            float envelope = Mathf.Exp(-time * 2.7f);

            samples[i] = Mathf.Clamp((body + rumble + crack) * envelope * 0.75f, -1f, 1f);
        }

        WriteWavFile(ExplosionClipPath, samples, sampleRate);
        AssetDatabase.ImportAsset(ExplosionClipPath, ImportAssetOptions.ForceUpdate);

        AudioImporter importer = AssetImporter.GetAtPath(ExplosionClipPath) as AudioImporter;
        if (importer != null)
        {
            importer.forceToMono = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.8f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ExplosionClipPath);
        Debug.Log($"[ImpactVfxBuilder] Сгенерирован звук взрыва: {ExplosionClipPath} (clip={(clip != null)}).");
        return clip;
    }

    private static void WriteWavFile(string path, float[] samples, int sampleRate)
    {
        int dataSize = samples.Length * 2;

        using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            for (int i = 0; i < samples.Length; i++)
                writer.Write((short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
        }
    }

    private static Material BuildFlashMaterial()
    {
        Texture2D texture = CreateGlowTexture(GlowTexturePath);
        return CreateParticleMaterial(FlashMaterialPath, texture, "ParticleFlash");
    }

    private static Material BuildSmokeMaterial()
    {
        Texture2D texture = CreateSmokeTexture(SmokeTexturePath);
        return CreateParticleMaterial(SmokeMaterialPath, texture, "ParticleSmoke");
    }

    private static Material BuildBombMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(BombMaterialPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = "BombMetal";
        material.SetColor("_BaseColor", new Color(0.17f, 0.17f, 0.15f, 1f));
        material.SetFloat("_Metallic", 0.65f);
        material.SetFloat("_Smoothness", 0.32f);

        AssetDatabase.CreateAsset(material, BombMaterialPath);
        return material;
    }

    private static Material CreateParticleMaterial(string path, Texture2D texture, string name)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null && texture != null)
        {
            existing.mainTexture = texture;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = ResolveParticleShader();
        Material material = new Material(shader);
        material.name = name;
        if (texture != null) material.mainTexture = texture;
        material.color = Color.white;

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Shader ResolveParticleShader()
    {
        string[] candidates =
        {
            "Sprites/Default",
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Unlit",
            "Unlit/Transparent"
        };

        foreach (string candidate in candidates)
        {
            Shader shader = Shader.Find(candidate);
            if (shader == null) continue;

            Debug.Log($"[ImpactVfxBuilder] Материал частиц использует шейдер: {candidate}");
            return shader;
        }

        Debug.LogError("[ImpactVfxBuilder] Не найден ни один шейдер для частиц, используется Standard.");
        return Shader.Find("Standard");
    }

    private static Texture2D CreateGlowTexture(string path)
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.1f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ConfigureTexture(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D CreateSmokeTexture(string path)
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float falloff = Mathf.Clamp01(1f - distance);

                float coarse = Mathf.PerlinNoise(x * 0.075f, y * 0.075f);
                float fine = Mathf.PerlinNoise(x * 0.22f + 31f, y * 0.22f + 17f);
                float puffs = Mathf.Clamp01(0.35f + coarse * 0.75f + fine * 0.35f);

                float alpha = Mathf.Clamp01(Mathf.Pow(falloff, 1.35f) * puffs);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ConfigureTexture(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void ConfigureTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    private static int AssignToDroneConfigs(GameObject explosionPrefab, GameObject bombPrefab, AudioClip explosionClip)
    {
        int updated = 0;

        foreach (string path in DroneConfigPaths)
        {
            DroneConfig config = AssetDatabase.LoadAssetAtPath<DroneConfig>(path);
            if (config == null)
            {
                Debug.LogWarning($"[ImpactVfxBuilder] Конфиг дрона не найден: {path}");
                continue;
            }

            SerializedObject serialized = new SerializedObject(config);
            SetObject(serialized, "explosionPrefab", explosionPrefab);
            SetObject(serialized, "bombPrefab", bombPrefab);
            SetObject(serialized, "explosionSound", explosionClip);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(config);
            updated++;

            Debug.Log($"[ImpactVfxBuilder] Назначены эффекты в конфиг: {path}");
        }

        return updated;
    }

    private static int AssignBombVfx(GameObject explosionPrefab)
    {
        int updated = 0;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".prefab")) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            DroppedBomb[] bombs = root.GetComponentsInChildren<DroppedBomb>(true);
            foreach (DroppedBomb bomb in bombs)
            {
                SerializedObject serialized = new SerializedObject(bomb);
                SerializedProperty vfx = serialized.FindProperty("explosionVFX");
                if (vfx == null || vfx.objectReferenceValue != null) continue;

                vfx.objectReferenceValue = explosionPrefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                updated++;
                Debug.Log($"[ImpactVfxBuilder] DroppedBomb.explosionVFX назначен в префабе: {path}");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        return updated;
    }

    private static void SetObject(SerializedObject serialized, string field, Object value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning($"[ImpactVfxBuilder] Поле '{field}' не найдено в {serialized.targetObject.GetType().Name}");
            return;
        }

        property.objectReferenceValue = value;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string leaf = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
