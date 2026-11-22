using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Настройка анимированного фона рабочего стола ComputerOS в Bunker(main).
/// Пластина (пустой радар, карта, рамка) очищается от GIF-мусора, а «лепесток» радара
/// генерируется процедурно: ровный сектор 65 градусов, вписанный в обод радара.
/// Лепесток вращается скриптом DesktopRadarSweep, поэтому фон зациклен без разрыва.
/// </summary>
public static class DesktopBackgroundSetup
{
    private const string Folder = "Assets/Textures/DesktopBackground";
    private const string PlatePath = Folder + "/DesktopBackground_plate.png";
    private const string SweepPath = Folder + "/DesktopBackground_sweep.png";
    private const string ScenePath = "Assets/Scenes/Bunker(main).unity";
    private const string BackgroundRootName = "ComputerOS";
    private const string SweepObjectName = "DesktopSweep";

    // Геометрия радара в пикселях исходной пластины 640x360 (ось Y считается сверху).
    // Центр найден по перекрестию (строка 179, столбец 524). Старый обод был эллипсом
    // 81 x 84.7 (GIF растянут по высоте), поэтому при очистке он приводится к ровному кругу.
    private const float RadarCenterX = 524f;
    private const float RadarCenterY = 179f;
    private const float SourceWidth = 640f;
    private const float SourceHeight = 360f;
    private const float RadarBeamRadius = 78f;
    private const float RadarRimRadius = 79.5f;
    private const float EraseEllipseA = 83f;
    private const float EraseEllipseB = 86.5f;
    private const float ReferenceCanvasWidth = 1920f;
    private const float ReferenceCanvasHeight = 1080f;

    // Мусорный текст под надписью RADAR. Правее — рамка панели на x = 625, её не задеваем.
    // Верхняя граница — 44: строка 47 содержит горизонтальную линию, её клонируем вместе с фоном.
    private const int JunkBoxX0 = 538;
    private const int JunkBoxY0 = 44;
    private const int JunkBoxX1 = 624;
    private const int JunkBoxY1 = 72;
    private const int JunkSourceOffsetX = -96;

    // Второй мусорный ряд — на одной строке с «RADAR/ONLINE» (левее надпись, её не трогаем).
    private const int JunkTopX0 = 536;
    private const int JunkTopY0 = 29;
    private const int JunkTopX1 = 624;
    private const int JunkTopY1 = 43;

    // Процедурный луч.
    private const int SweepTextureSize = 1024;
    private const float SweepBeamRadiusFraction = 506f / 512f;
    private const float BeamSectorDegrees = 65f;
    private const float BeamHeadAngleDegrees = 90f;
    private const float BeamTailAlpha = 0.42f;
    private const float BeamHeadAlpha = 0.95f;
    private static readonly Color BeamTailColor = new Color(219f / 255f, 20f / 255f, 61f / 255f);
    private static readonly Color BeamHeadColor = new Color(1f, 42f / 255f, 85f / 255f);

    // Оформление радара на пластине (значения сняты с исходного кадра GIF).
    private static readonly float[] RingRadii = { 26f, 44.5f, 63f };
    private static readonly Color RingColor = new Color32(58, 60, 61, 255);
    private static readonly Color CrosshairColor = new Color32(48, 50, 52, 255);
    private static readonly Color RimColor = new Color32(124, 128, 130, 255);

    // Буквы N/E/S/W: плотные яркие глифы, их нельзя затирать при очистке круга.
    private static readonly RectInt[] LetterBoxes =
    {
        new RectInt(512, 78, 27, 23),
        new RectInt(513, 259, 27, 23),
        new RectInt(424, 168, 26, 25),
        new RectInt(597, 168, 24, 25)
    };

    [MenuItem("Tools/DroneSimulator/UI/Prepare Desktop Background Assets")]
    public static void PrepareAssets()
    {
        CleanPlateTexture();
        GenerateSweepTexture();

        ConfigureTexture(PlatePath, false);
        ConfigureTexture(SweepPath, true);
        AssetDatabase.SaveAssets();
        Debug.Log("[DesktopBackgroundSetup] Пластина очищена, луч сгенерирован (1024x1024), импорт настроен.");
    }

    private static void ConfigureTexture(string assetPath, bool keepAlpha)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[DesktopBackgroundSetup] Не найден импортер для " + assetPath);
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 1024;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = keepAlpha;
        importer.isReadable = false;
        // Кадры 640x360: без сжатия это ~0.9 МБ на текстуру, зато нет артефактов DXT на тонких линиях карты и кольцах радара.
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    [MenuItem("Tools/DroneSimulator/UI/Bind Desktop Background (Bunker)")]
    public static void BindBunker()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (!string.Equals(active.path, ScenePath))
        {
            if (active.isDirty)
            {
                Debug.LogError("[DesktopBackgroundSetup] Открыта другая сцена с несохранёнными изменениями. Сохраните её и повторите.");
                return;
            }

            active = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Sprite plate = AssetDatabase.LoadAssetAtPath<Sprite>(PlatePath);
        Sprite sweep = AssetDatabase.LoadAssetAtPath<Sprite>(SweepPath);
        if (plate == null || sweep == null)
        {
            Debug.LogError("[DesktopBackgroundSetup] Не найдены спрайты фона. Сначала выполните Prepare Desktop Background Assets.");
            return;
        }

        GameObject root = FindByName(active, BackgroundRootName);
        if (root == null)
        {
            Debug.LogError("[DesktopBackgroundSetup] В сцене не найден объект " + BackgroundRootName);
            return;
        }

        Image background = root.GetComponent<Image>();
        if (background == null)
        {
            Debug.LogError("[DesktopBackgroundSetup] На " + BackgroundRootName + " нет Image — фон некуда назначать.");
            return;
        }

        background.sprite = plate;
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = Color.white;

        Transform existing = root.transform.Find(SweepObjectName);
        GameObject sweepObject = existing != null ? existing.gameObject : CreateSweepObject(root.transform);

        Image sweepImage = sweepObject.GetComponent<Image>();
        sweepImage.sprite = sweep;
        sweepImage.type = Image.Type.Simple;
        sweepImage.preserveAspect = false;
        sweepImage.raycastTarget = false;

        // Квадрат вокруг центра радара: пивот в центре, кромка луча совпадает с внутренней кромкой обода.
        ApplySweepRect((RectTransform)sweepObject.transform, background);

        ComputerUIController controller = root.GetComponent<ComputerUIController>();
        DesktopRadarSweep animator = sweepObject.GetComponent<DesktopRadarSweep>();
        if (animator == null) animator = sweepObject.AddComponent<DesktopRadarSweep>();

        var serialized = new SerializedObject(animator);
        serialized.FindProperty("sweepTransform").objectReferenceValue = sweepObject.GetComponent<RectTransform>();
        serialized.FindProperty("computerUI").objectReferenceValue = controller;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);
        Debug.Log("[DesktopBackgroundSetup] Фон рабочего стола привязан: " + PlatePath + " + вращаемый слой " + SweepObjectName + ".");
    }

    private static GameObject CreateSweepObject(Transform parent)
    {
        var sweepObject = new GameObject(SweepObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        sweepObject.transform.SetParent(parent, false);
        sweepObject.transform.SetSiblingIndex(0);
        return sweepObject;
    }

    /// <summary>
    /// Квадрат DesktopSweep по центру радара: пивот ровно в центре (вращение без биения),
    /// сторона подобрана так, чтобы кромка нарисованного луча совпала с внутренней кромкой обода.
    /// </summary>
    private static void ApplySweepRect(RectTransform rect, Image background)
    {
        float scale = ReferenceCanvasWidth / SourceWidth;
        float beamRadius = RadarBeamRadius * scale;
        float size = 2f * beamRadius / SweepBeamRadiusFraction;
        float offsetX = (RadarCenterX / SourceWidth - 0.5f) * ReferenceCanvasWidth;
        float offsetY = ((1f - RadarCenterY / SourceHeight) - 0.5f) * ReferenceCanvasHeight;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = new Vector2(offsetX, offsetY);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        if (background != null)
        {
            Rect backgroundRect = background.rectTransform.rect;
            if (Mathf.Abs(backgroundRect.width - ReferenceCanvasWidth) > 1f ||
                Mathf.Abs(backgroundRect.height - ReferenceCanvasHeight) > 1f)
            {
                Debug.LogWarningFormat(
                    "[DesktopBackgroundSetup] ComputerOS = {0}x{1}, а лейаут рассчитан на {2}x{3}. " +
                    "Пластина тянется на весь канвас, поэтому на другом разрешении кромка луча разойдётся с ободом.",
                    backgroundRect.width, backgroundRect.height, ReferenceCanvasWidth, ReferenceCanvasHeight);
            }
        }

        Debug.LogFormat(
            "[DesktopBackgroundSetup] DesktopSweep: pivot (0.5, 0.5), size {0:0.0}, anchoredPosition ({1:0.0}, {2:0.0}).",
            size, offsetX, offsetY);
    }

    /// <summary>
    /// Чистит пластину: убирает мусорный текст справа сверху, приводит фон круга радара
    /// к чистому графиту и перерисовывает ровные кольца, перекрестие и обод r = 79.5.
    /// Буквы N/E/S/W сохраняются (маска по яркости внутри их боксов).
    /// </summary>
    private static void CleanPlateTexture()
    {
        if (!File.Exists(PlatePath))
        {
            Debug.LogError("[DesktopBackgroundSetup] Не найден " + PlatePath);
            return;
        }

        Texture2D plate = LoadPng(PlatePath);
        int width = plate.width;
        int height = plate.height;
        Color32[] pixels = plate.GetPixels32();

        // Копия «до чистки»: из неё берутся нетронутые буквы и чистый патч для заплатки.
        var original = (Color32[])pixels.Clone();

        Color graphite = SampleGraphite(original, width, height);
        RemoveTopRightJunk(pixels, original, width, height);
        bool[] protectedPixels = BuildLetterMask(original, width, height);
        FlattenRadarDisc(pixels, width, height, graphite, protectedPixels);
        DrawRadarLinework(pixels, width, height, protectedPixels);

        plate.SetPixels32(pixels);
        plate.Apply();
        SavePng(plate, PlatePath);

        Debug.Log("[DesktopBackgroundSetup] Пластина очищена: мусорный текст удалён, круг радара — графит, кольца и обод перерисованы.");
    }

    /// <summary>Цвет чистого графита: медиана фона над радаром с частичным снятием зелёного налёта GIF.</summary>
    private static Color SampleGraphite(Color32[] pixels, int width, int height)
    {
        var reds = new System.Collections.Generic.List<byte>();
        var greens = new System.Collections.Generic.List<byte>();
        var blues = new System.Collections.Generic.List<byte>();

        for (int y = 48; y <= 74; y++)
        {
            for (int x = 442; x <= 516; x++)
            {
                Color32 color = pixels[Index(x, y, width, height)];
                if (Luminance(color) > 60f) continue;
                reds.Add(color.r);
                greens.Add(color.g);
                blues.Add(color.b);
            }
        }

        if (reds.Count == 0) return new Color(0.07f, 0.08f, 0.09f);

        reds.Sort();
        greens.Sort();
        blues.Sort();
        int mid = reds.Count / 2;
        Color median = new Color(reds[mid] / 255f, greens[mid] / 255f, blues[mid] / 255f);
        float medianLuminance = 0.2126f * median.r + 0.7152f * median.g + 0.0722f * median.b;
        // Чистый графит: оставляем 40 % исходного оттенка, чтобы круг не выпадал из панели.
        return Color.Lerp(median, new Color(medianLuminance, medianLuminance, medianLuminance), 0.6f);
    }

    /// <summary>
    /// Затирает нечитаемый мусорный текст под «RADAR/ONLINE»: бокс заполняется чистым
    /// патчем фона из той же строки (левее на 96 px), поэтому сохраняется родной дизеринг.
    /// </summary>
    private static void RemoveTopRightJunk(Color32[] pixels, Color32[] original, int width, int height)
    {
        // 1) Две нижние строки мусора + горизонтальная линия под надписью (строка 47):
        //    патч берётся из той же строки левее, поэтому линия остаётся ровной.
        ClonePatch(pixels, original, width, height,
            JunkBoxX0, JunkBoxY0, JunkBoxX1, JunkBoxY1, JunkSourceOffsetX, 0);

        // 2) Мусор на одной строке с «RADAR/ONLINE»: берём чистый промежуток между
        //    надписью и мусором (x 508..535) и зеркально тайлим его по строке —
        //    тон и дизеринг совпадают, вертикальный градиент не вносится.
        FillTopJunkFromStrip(pixels, width, height);
    }

    /// <summary>
    /// Заполняет верхний ряд мусора зеркальной копией чистого промежутка той же строки.
    /// </summary>
    private static void FillTopJunkFromStrip(Color32[] pixels, int width, int height)
    {
        const int sourceX = 508;
        const int sourceWidth = 28; // 508..535: чистый промежуток между надписью и мусором
        int period = sourceWidth * 2;
        int rows = JunkTopY1 - JunkTopY0 + 1;

        // Снимок полосы до записи: иначе часть источника уже была бы перезаписана.
        var strip = new Color32[sourceWidth * rows];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < sourceWidth; column++)
            {
                strip[row * sourceWidth + column] = pixels[Index(sourceX + column, JunkTopY0 + row, width, height)];
            }
        }

        // Заменяем весь бокс целиком (без растушёвки): иначе остатки прошлой заплатки
        // дают светлую рамку. Границы бокса проходят по пустому фону с тем же тоном.
        for (int y = JunkTopY0; y <= JunkTopY1; y++)
        {
            int row = y - JunkTopY0;
            // Фаза сдвигается по строкам, чтобы повтор полосы не читался как узор.
            int phase = (row * 11) % sourceWidth;
            for (int x = JunkTopX0; x <= JunkTopX1; x++)
            {
                int offset = (x - JunkTopX0 + phase) % period;
                int sourceOffset = offset < sourceWidth ? offset : period - 1 - offset;
                pixels[Index(x, y, width, height)] = strip[row * sourceWidth + sourceOffset];
            }
        }
    }

    /// <summary>
    /// Заполняет прямоугольник патчем из source со сдвигом (sourceDx, sourceDy),
    /// с выравниванием средней яркости и плавным переходом по краям.
    /// </summary>
    private static void ClonePatch(Color32[] pixels, Color32[] source, int width, int height,
        int x0, int y0, int x1, int y1, int sourceDx, int sourceDy)
    {
        const int feather = 3;

        float sourceLuminance = 0f;
        float destinationLuminance = 0f;
        int sourceCount = 0;
        int destinationCount = 0;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                // Порог 40: фон пластины тёмный (6..30), текст — 60..200.
                // Остатки текста в зоне не должны подмешиваться в среднюю яркость заплатки.
                float sourceValue = Luminance(source[Index(x + sourceDx, y + sourceDy, width, height)]);
                if (sourceValue < 40f)
                {
                    sourceLuminance += sourceValue;
                    sourceCount++;
                }

                float destinationValue = Luminance(source[Index(x, y, width, height)]);
                if (destinationValue < 40f)
                {
                    destinationLuminance += destinationValue;
                    destinationCount++;
                }
            }
        }

        float delta = (sourceCount > 0 && destinationCount > 0)
            ? Mathf.Clamp((destinationLuminance / destinationCount) - (sourceLuminance / sourceCount), -10f, 10f)
            : 0f;
        float offset = delta / 255f;

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Color32 sourcePixel = source[Index(x + sourceDx, y + sourceDy, width, height)];
                Color fill = new Color(
                    Mathf.Clamp01(sourcePixel.r / 255f + offset),
                    Mathf.Clamp01(sourcePixel.g / 255f + offset),
                    Mathf.Clamp01(sourcePixel.b / 255f + offset));

                int edge = Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(y - y0, y1 - y));
                float blend = Mathf.Clamp01((edge + 1f) / (feather + 1f));

                int index = Index(x, y, width, height);
                pixels[index] = Color.Lerp(pixels[index], fill, blend);
            }
        }
    }

    /// <summary>Маска букв N/E/S/W: яркие пиксели внутри их боксов + дилатация 2 px под сглаженные кромки.</summary>
    private static bool[] BuildLetterMask(Color32[] pixels, int width, int height)
    {
        var mask = new bool[width * height];
        for (int i = 0; i < LetterBoxes.Length; i++)
        {
            RectInt box = LetterBoxes[i];
            for (int y = box.yMin; y < box.yMax; y++)
            {
                for (int x = box.xMin; x < box.xMax; x++)
                {
                    if (Luminance(pixels[Index(x, y, width, height)]) >= 130f)
                        mask[Index(x, y, width, height)] = true;
                }
            }
        }

        var dilated = (bool[])mask.Clone();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!mask[Index(x, y, width, height)]) continue;

                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        dilated[Index(nx, ny, width, height)] = true;
                    }
                }
            }
        }

        return dilated;
    }

    /// <summary>
    /// Заменяет шум внутри эллипса старого обода (81 x 84.7) чистым графитом.
    /// Кромка эллипса плавно смешивается с исходным фоном, буквы не затрагиваются.
    /// </summary>
    private static void FlattenRadarDisc(Color32[] pixels, int width, int height, Color graphite, bool[] protectedPixels)
    {
        const float featherStart = 0.86f;
        float featherRange = 1f - featherStart * featherStart;

        int minX = Mathf.Max(0, Mathf.FloorToInt(RadarCenterX - EraseEllipseA));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(RadarCenterX + EraseEllipseA));
        int minY = Mathf.Max(0, Mathf.FloorToInt(RadarCenterY - EraseEllipseB));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(RadarCenterY + EraseEllipseB));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float nx = (x - RadarCenterX) / EraseEllipseA;
                float ny = (y - RadarCenterY) / EraseEllipseB;
                float normalized = nx * nx + ny * ny;
                if (normalized > 1f) continue;

                int index = Index(x, y, width, height);
                if (protectedPixels[index]) continue;

                float blend = Mathf.Clamp01((1f - normalized) / featherRange);
                pixels[index] = Color.Lerp(pixels[index], GraphiteAt(x, y, graphite), blend);
            }
        }
    }

    /// <summary>Чистый графит с лёгкой виньеткой и шумом ±1, чтобы не читалась плоская заливка.</summary>
    private static Color GraphiteAt(int x, int y, Color graphite)
    {
        float dx = x - RadarCenterX;
        float dy = y - RadarCenterY;
        float distance = Mathf.Sqrt(dx * dx + dy * dy);
        float falloff = Mathf.Lerp(1.12f, 0.88f, Mathf.Clamp01(distance / EraseEllipseA));
        float offset = ((Hash(x, y) % 3) - 1) / 255f;

        return new Color(
            Mathf.Clamp01(graphite.r * falloff + offset),
            Mathf.Clamp01(graphite.g * falloff + offset),
            Mathf.Clamp01(graphite.b * falloff + offset));
    }

    /// <summary>
    /// Рисует поверх графита ровные кольца (26 / 44.5 / 63), перекрестие и обод r = 79.5
    /// с аналитическим сглаживанием. Защищённые пиксели букв не перекрываются.
    /// </summary>
    private static void DrawRadarLinework(Color32[] pixels, int width, int height, bool[] protectedPixels)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(RadarCenterX - RadarRimRadius - 3f));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(RadarCenterX + RadarRimRadius + 3f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(RadarCenterY - RadarRimRadius - 3f));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(RadarCenterY + RadarRimRadius + 3f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int index = Index(x, y, width, height);
                if (protectedPixels[index]) continue;

                float dx = x - RadarCenterX;
                float dy = y - RadarCenterY;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance > RadarRimRadius + 2f) continue;

                Color pixel = pixels[index];

                for (int i = 0; i < RingRadii.Length; i++)
                {
                    float ringCoverage = Coverage(distance, RingRadii[i], 0.65f);
                    if (ringCoverage > 0f) pixel = Color.Lerp(pixel, RingColor, ringCoverage * 0.9f);
                }

                float crosshair = 0f;
                if (distance <= RadarBeamRadius)
                {
                    crosshair = Mathf.Max(Coverage(Mathf.Abs(dx), 0f, 0.6f), Coverage(Mathf.Abs(dy), 0f, 0.6f));
                }

                if (crosshair > 0f) pixel = Color.Lerp(pixel, CrosshairColor, crosshair * 0.85f);

                float rimCoverage = Coverage(distance, RadarRimRadius, 1.15f);
                if (rimCoverage > 0f) pixel = Color.Lerp(pixel, RimColor, rimCoverage);

                pixels[index] = pixel;
            }
        }
    }

    /// <summary>
    /// Генерирует DesktopBackground_sweep.png: 1024x1024 RGBA32, ровный круг,
    /// передняя кромка #FF2A55 (2-3 px на экране), шлейф #DB143D с экспоненциальным спадом по дуге 65°.
    /// За пределами сектора альфа ровно 0.
    /// </summary>
    private static void GenerateSweepTexture()
    {
        int size = SweepTextureSize;
        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center * SweepBeamRadiusFraction;
        const int samples = 3;
        const float sampleCount = samples * samples;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Жёсткая отсечка по центру пикселя: за пределами сектора альфа ровно 0,
                // даже у самой вершины луча, где угол подпиксельных выборок гуляет.
                float centerAngle = Mathf.Atan2(y - center, x - center) * Mathf.Rad2Deg;
                if (Mathf.Repeat(centerAngle - BeamHeadAngleDegrees, 360f) > BeamSectorDegrees) continue;

                float alphaSum = 0f;
                float redSum = 0f;
                float greenSum = 0f;
                float blueSum = 0f;

                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        float px = x + (sx + 0.5f) / samples - (center + 0.5f);
                        float py = y + (sy + 0.5f) / samples - (center + 0.5f);
                        float distance = Mathf.Sqrt(px * px + py * py);
                        if (distance > radius + 1f) continue;

                        float radialCoverage = Mathf.Clamp01(radius + 0.5f - distance);
                        if (radialCoverage <= 0f) continue;

                        float angle = Mathf.Atan2(py, px) * Mathf.Rad2Deg;
                        float delta = Mathf.Repeat(angle - BeamHeadAngleDegrees, 360f);
                        if (delta > BeamSectorDegrees) continue;

                        float normalized = delta / BeamSectorDegrees;
                        float tail = BeamTailAlpha * Mathf.Exp(-delta / 20f) * Mathf.Pow(1f - normalized, 0.7f);

                        float headArc = delta * Mathf.Deg2Rad * Mathf.Max(distance, 0.5f);
                        float head = Mathf.Clamp01(2.1f - headArc) * BeamHeadAlpha;
                        float glow = 0.22f * Mathf.Exp(-headArc / 3f);

                        float alpha = Mathf.Max(Mathf.Max(tail, head), glow) * radialCoverage;
                        if (alpha <= 0f) continue;

                        float headMix = Mathf.Clamp01(1f - headArc / 5f);
                        Color color = Color.Lerp(BeamTailColor, BeamHeadColor, headMix);

                        alphaSum += alpha;
                        redSum += color.r * alpha;
                        greenSum += color.g * alpha;
                        blueSum += color.b * alpha;
                    }
                }

                if (alphaSum <= 0.0001f) continue;

                float inverse = 1f / alphaSum;
                pixels[y * size + x] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(redSum * inverse * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(greenSum * inverse * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(blueSum * inverse * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(alphaSum / sampleCount * 255f), 0, 255));
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        SavePng(texture, SweepPath);

        Debug.Log("[DesktopBackgroundSetup] Луч радара сгенерирован: " + size + "x" + size + ", сектор " + BeamSectorDegrees + " град.");
    }

    private static Texture2D LoadPng(string assetPath)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(assetPath));
        return texture;
    }

    private static void SavePng(Texture2D texture, string assetPath)
    {
        byte[] png = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);
        File.WriteAllBytes(assetPath, png);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    /// <summary>Индекс пикселя в массиве GetPixels32 (ось Y текстуры идёт снизу вверх).</summary>
    private static int Index(int x, int y, int width, int height)
    {
        return (height - 1 - y) * width + x;
    }

    private static float Luminance(Color32 color)
    {
        return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
    }

    /// <summary>Покрытие пикселя штрихом: 1 внутри линии, 0 за её пределами.</summary>
    private static float Coverage(float distance, float radius, float halfWidth)
    {
        return Mathf.Clamp01(halfWidth + 0.5f - Mathf.Abs(distance - radius));
    }

    private static int Hash(int x, int y)
    {
        int value = x * 374761393 + y * 668265263;
        value = (value ^ (value >> 13)) * 1274126177;
        value ^= value >> 16;
        return value & 0x7FFFFFFF;
    }

    private static GameObject FindByName(Scene scene, string name)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Transform found = FindInHierarchy(rootObject.transform, name);
            if (found != null) return found.gameObject;
        }

        return null;
    }

    private static Transform FindInHierarchy(Transform current, string name)
    {
        if (current.name == name) return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindInHierarchy(current.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }
}
