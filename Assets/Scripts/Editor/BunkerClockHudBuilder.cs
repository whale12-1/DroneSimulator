using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Сборка HUD-часов бункера в InteractionCanvas и привязка BunkerTimeManager
// на объекте BunkerStory. Меню идемпотентно: повторный запуск обновляет
// существующий виджет и ссылки, дубликаты не создаются.
//
// Оформление общее для бункера: полупрозрачная тёмная плашка, красный акцент
// #DB143D, белый TMP (LiberationSans SDF) с Outline, у всех графических
// элементов raycastTarget = false. Виджет ставится первым сиблингом канваса,
// поэтому полноэкранные ComputerOS и пауза его перекрывают.
public static class BunkerClockHudBuilder
{
    private const string ScenePath = "Assets/Scenes/Bunker(main).unity";
    private const string CanvasName = "InteractionCanvas";
    private const string HudName = "StoryClockHUD";
    private const string StoryObjectName = "BunkerStory";
    private const string BedObjectName = "Prop_MilitaryBed";
    private const string ClockSample = "ДЕНЬ 1  |  06:00";

    private const float HudLeft = 32f;
    private const float HudTop = 28f;
    private const float HudWidth = 360f;
    private const float HudHeight = 72f;
    private const float AccentWidth = 4f;
    private const float LabelLeftPadding = 18f;
    private const float LabelRightPadding = 12f;
    private const float FontSize = 22f;

    private static readonly Color PlateColor = new Color(0.045f, 0.05f, 0.055f, 0.55f);
    private static readonly Color AccentColor = new Color(0.86f, 0.08f, 0.24f, 1f);
    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color TextColor = Color.white;
    private static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.85f);
    private static readonly Vector2 OutlineDistance = new Vector2(1.5f, -1.5f);

    [MenuItem("Tools/DroneSimulator/UI/Build Bunker Clock HUD (Bunker)")]
    public static void Build()
    {
        Scene scene = EnsureSceneOpen();
        if (!scene.IsValid()) return;

        Transform canvas = FindByName(scene, CanvasName);
        if (canvas == null)
        {
            Debug.LogError($"[BunkerClockHudBuilder] В сцене не найден объект {CanvasName}.");
            return;
        }

        Transform story = FindByName(scene, StoryObjectName);
        if (story == null)
        {
            Debug.LogError($"[BunkerClockHudBuilder] В сцене не найден объект {StoryObjectName}.");
            return;
        }

        BunkerStoryProgress progress = story.GetComponent<BunkerStoryProgress>();
        if (progress == null)
        {
            Debug.LogError($"[BunkerClockHudBuilder] На {StoryObjectName} нет BunkerStoryProgress.");
            return;
        }

        BunkerTimeManager timeManager = story.GetComponent<BunkerTimeManager>();
        if (timeManager == null) timeManager = story.gameObject.AddComponent<BunkerTimeManager>();
        ApplyTimeManagerDefaults(timeManager, progress);

        GameObject hud = BuildWidget(canvas);

        BunkerClockHUD clockHud = hud.GetComponent<BunkerClockHUD>();
        if (clockHud == null) clockHud = hud.AddComponent<BunkerClockHUD>();
        BindClockHud(clockHud, hud, timeManager, progress);

        BindBed(scene, timeManager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BunkerClockHudBuilder] HUD часов собран: плашка, акцент #DB143D, TMP с Outline; " +
                  "BunkerTimeManager привязан к BunkerStory и кровати.");
    }

    private static Scene EnsureSceneOpen()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (string.Equals(active.path, ScenePath)) return active;

        if (active.isDirty)
        {
            Debug.LogError("[BunkerClockHudBuilder] Открыта другая сцена с несохранёнными изменениями. Сохраните её и повторите.");
            return default;
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    // Эталонные значения из ТЗ: старт 06:00, 1 реальная секунда = 1 игровая минута.
    private static void ApplyTimeManagerDefaults(BunkerTimeManager timeManager, BunkerStoryProgress progress)
    {
        var serialized = new SerializedObject(timeManager);
        serialized.FindProperty("startHour").intValue = 6;
        serialized.FindProperty("startMinute").intValue = 0;
        serialized.FindProperty("minutesPerRealSecond").floatValue = 1f;
        serialized.FindProperty("morningHour").intValue = 6;
        serialized.FindProperty("advanceDayAtMorning").boolValue = true;
        serialized.FindProperty("progress").objectReferenceValue = progress;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BindClockHud(BunkerClockHUD clockHud, GameObject hud,
        BunkerTimeManager timeManager, BunkerStoryProgress progress)
    {
        TMP_Text label = hud.GetComponentInChildren<TMP_Text>(true);

        var serialized = new SerializedObject(clockHud);
        serialized.FindProperty("clockText").objectReferenceValue = label;
        serialized.FindProperty("timeManager").objectReferenceValue = timeManager;
        serialized.FindProperty("storyProgress").objectReferenceValue = progress;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BindBed(Scene scene, BunkerTimeManager timeManager)
    {
        Transform bed = FindByName(scene, BedObjectName);
        if (bed == null)
        {
            Debug.LogWarning($"[BunkerClockHudBuilder] Не найден {BedObjectName}: сон останется на старом фейдере.");
            return;
        }

        BunkerBedInteractable interactable = bed.GetComponent<BunkerBedInteractable>();
        if (interactable == null)
        {
            Debug.LogWarning($"[BunkerClockHudBuilder] На {BedObjectName} нет BunkerBedInteractable.");
            return;
        }

        var serialized = new SerializedObject(interactable);
        serialized.FindProperty("timeManager").objectReferenceValue = timeManager;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject BuildWidget(Transform canvas)
    {
        GameObject hud = TacticalUIBuilder.EnsureChild(canvas, HudName);
        hud.layer = 5;

        RectTransform rect = TacticalUIBuilder.RT(hud);
        TacticalUIBuilder.SetTopLeft(rect, HudLeft, HudTop, HudWidth, HudHeight);

        Image plate = TacticalUIBuilder.EnsureImage(hud, null, PlateColor, Image.Type.Simple);
        plate.raycastTarget = false;

        // Красный акцент слева — как у остальных плашек бункера.
        GameObject accent = TacticalUIBuilder.EnsureChild(hud.transform, "Accent");
        RectTransform accentRect = TacticalUIBuilder.RT(accent);
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(AccentWidth, 0f);
        TacticalUIBuilder.EnsureImage(accent, null, AccentColor, Image.Type.Simple).raycastTarget = false;

        // Тонкий разделитель по нижней кромке.
        GameObject divider = TacticalUIBuilder.EnsureChild(hud.transform, "Divider");
        RectTransform dividerRect = TacticalUIBuilder.RT(divider);
        dividerRect.anchorMin = new Vector2(0f, 0f);
        dividerRect.anchorMax = new Vector2(1f, 0f);
        dividerRect.pivot = new Vector2(0.5f, 0f);
        dividerRect.anchoredPosition = Vector2.zero;
        dividerRect.sizeDelta = new Vector2(0f, 1f);
        TacticalUIBuilder.EnsureImage(divider, null, DividerColor, Image.Type.Simple).raycastTarget = false;

        GameObject labelObject = TacticalUIBuilder.EnsureChild(hud.transform, "Label");
        RectTransform labelRect = TacticalUIBuilder.RT(labelObject);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(LabelLeftPadding, 0f);
        labelRect.offsetMax = new Vector2(-LabelRightPadding, 0f);

        TextMeshProUGUI label = TacticalUIBuilder.EnsureText(
            labelObject, ClockSample, FontSize, TextColor, TextAlignmentOptions.MidlineLeft);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        Outline outline = labelObject.GetComponent<Outline>();
        if (outline == null) outline = labelObject.AddComponent<Outline>();
        outline.effectColor = OutlineColor;
        outline.effectDistance = OutlineDistance;
        outline.useGraphicAlpha = true;

        // Первый сиблинг: модальные окна (ComputerOS, пауза) рисуются поверх часов.
        hud.transform.SetSiblingIndex(0);
        return hud;
    }

    private static Transform FindByName(Scene scene, string objectName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Transform found = FindInHierarchy(rootObject.transform, objectName);
            if (found != null) return found;
        }

        return null;
    }

    private static Transform FindInHierarchy(Transform current, string objectName)
    {
        if (current.name == objectName) return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindInHierarchy(current.GetChild(i), objectName);
            if (found != null) return found;
        }

        return null;
    }
}
