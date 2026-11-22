# 04 — Префабы и UI

## 1. Префабы, реально задействованные в 5 сценах

### 1.1 UI

| Префаб | Где используется | Скрипты внутри |
|---|---|---|
| `Assets/Prefabs/UI/MissionSelectPanel.prefab` | `MainMenu`, `Bunker(main)` | `MissionSelectScreen`, `MissionDetailsPanel`; вложенный `MissionCard.prefab` |
| `Assets/Prefabs/UI/MissionCard.prefab` | вложен в `MissionSelectPanel` | `MissionCardView` |
| `Assets/Prefabs/UI/SettingsPanel.prefab` | `MainMenu`, `Bunker(main)` | `SettingsPanel`, `SettingsListBuilder` |
| `Assets/Prefabs/UI/Cylinder.prefab` | `FirstMission(Forest)` (маркер) | — (`UnlitStolb.mat`) |

### 1.2 Дроны

| Префаб | Где | Скрипты |
|---|---|---|
| `Assets/Prefabs/Drones/GimbalDrone Variant.prefab` | `PreparingDemo`, `FirstMission(Forest)` | `DroneCameraModule`, `FPVZoom`, `ReconHUD`, `TargetDetector`, `TargetLockManager` (+ вложенный `GimbalDrone.prefab`) |
| `Assets/Prefabs/Drones/StrikeDrone 1.prefab` | `PreparingDemo`, `FirstMission(Forest)` | `DroneFlightController`, `DroneMotors`, `DroneCollision`, `DroneCameraModule`, `FPVZoom`, `StrikeHUD`, `FPVCameraShake`, `NightVisionController`, `EWReceiver`, `TargetDetector`, `TargetLockManager` |

Профили (`Assets/Settings/DroneConfigs/`):

* `MavicReconConfig.asset` — разведчик (`cameraType = ReconGimbal`).
* `SmallFpvHeatConfig.asset` — ударный FPV (`cameraType = StrikeFixed`, HEAT).
* `IntersectorConfig.asset` — перехватчик, **в 5 сценах не используется**.

### 1.3 Техника и окружение

| Префаб | Где | Скрипты |
|---|---|---|
| `Assets/Prefabs/Vehicles/btrWithCopeCage.prefab` | `FirstMission(Forest)` | `VehicleHealth`, `VehicleModule`, `VehiclePatrol`, `VehicleSmartAI`, `TargetObject`, `EWEmitter`; вложены `WildFire.prefab`, `SmokeEffect.prefab` |
| `Assets/Prefabs/Buildings/House.prefab` | `PreparingDemo` | `DestructibleWallZone` (`DepthMaskMat.mat`) |
| `Assets/Prefabs/Buildings/dust.prefab` | `PreparingDemo` | — (материал Cypress_Bark) |
| `Eletric_pole.prefab` (Polyzero) | `FirstMission(Forest)` | `ElectricCableSpline`, `ElectricCableWindVariation` |
| `BigExplosion.prefab`, `WildFire.prefab`, `SmokeEffect.prefab`, `VFX_Fire_01_Medium_Smoke.prefab` | миссии | VFX из `ParticlePack` / `Free Fire VFX` |

> `GimbalDrone.prefab` и `btr-4e1-cope.fbx` — вложенные ассеты, тянутся транзитивно; их скрипты учтены выше.

---

## 2. Канвасы и экранная структура по сценам

### 2.1 `Boot`

```
Canvas (Overlay)
 └─ (CanvasGroup) — экран загрузки, alpha управляется SceneLoader
EventSystem
Camera
```

### 2.2 `MainMenu`

```
Canvas (Overlay)
 ├─ MainPanel        [SimpleUIScreen + CanvasGroup]
 │   ├─ Title (TMP)
 │   ├─ PlayButton     → MainMenuController.OnOpenMissions()
 │   ├─ SettingsButton → MainMenuController.OnOpenSettings()
 │   └─ ExitButton     → MainMenuController.OnExit()
 ├─ SettingsPanel      [prefab instance — модальное окно 1400x920 + затемнение]
 └─ VideoBackground (VideoPlayer → MenuVideoRT)
EventSystem
MenuCamera
```

> С 22.11.2026 `SettingsPanel` — модальное окно по центру (`Scrim` + `Plate` 1400×920 + рамка с красными угловыми скобками). Главное меню (`MainPanel`) остаётся эталоном стиля и не меняется.
>
> С 22.11.2026 (Fix-QA) инстанс `MissionSelectPanel` из `MainMenu` **удалён**: панель ниоткуда не открывалась (мёртвый UI), а `MainMenuController` больше не держит поле `missionSelect`. Панель выбора миссий осталась только в `Bunker(main)`.

### 2.3 `Bunker(main)`

```
Canvas (Overlay)
 ├─ InteractionHint  [CanvasGroup + Label/TMP 20 px + Outline]
 │     — подсказка «Сесть [E] / Войти в систему [E]»: по центру, 75 % высоты (y −270), без плашки
 ├─ SittingHint      [CanvasGroup + Label/TMP 18 px + Outline]
 │     — подсказка «Встать [Пробел]»: по центру у нижнего края (y −470), без плашки
 ├─ ShopWindow       [CanvasGroup] — окно «Магазин» (контента нет)
 ├─ ComputerOS       [ComputerUIController, CanvasGroup, Image = пластина фона 1920×1080] ← «рабочий стол» ОС
 │    ├─ DesktopSweep [DesktopRadarSweep] — слой лепестка радара (пивот в центре радара, вращается от unscaled time)
 │    ├─ TerminalHeader (56 px, «ОПЕРАТОР · РАБОЧИЙ СТОЛ» / «СИСТЕМА: ОНЛАЙН»)
 │    ├─ Btn_Missions [DesktopIcon] — металлическая плитка 160×160, иконка HUF #15 + «МИССИИ»
 │    └─ TerminalFooter (48 px, «ВЫБЕРИТЕ РАЗДЕЛ» / «Esc / E — выйти»)
 ├─ MissionSelectPanel [MissionSelectScreen + MissionMenuController]
 │    ├─ Frame (тонкий контур + красные угловые скобки)
 │    ├─ Header (Title / Subtitle / TotalScore / Rule / BackButton)
 │    ├─ Scroll View (вертикальный список карточек 840×150)
 │    └─ MissionDetailsPanel (860×814, баннер/статус/статистика/цели/LaunchButton)
 ├─ PausePanel       [BunkerPauseController.pausePanel]
 │    ├─ Plate 560×620 (Scrim + Frame + Title/Subtitle/AccentRule)
 │    ├─ resume   «Продолжить»     → Resume()
 │    ├─ settings «Настройки»      → OpenSettings() → SettingsPanel.Show()
 │    ├─ exit     «Выйти в главное меню» → ExitToMainMenu()   (красная)
 │    ├─ Footer   «Esc — продолжить · E — терминал»
 │    └─ SettingsNotice — заглушка, если SettingsPanel не назначена
 └─ SettingsPanel    [prefab instance — модальное окно, см. §3.3]
```

### 2.4 `PreparingDemo` / `FirstMission(Forest)`

```
Canvas (Overlay)
 ├─ InstructionText (TMP)          — тексты инструктора / задачи
 ├─ PausePanel                     — MissionUIManager.TogglePause()
 ├─ CrashPanel                     — MissionUIManager.ShowCrashPanel()
 ├─ DebriefPanel                   — DebriefController (title, stats, ReturnButton)
 ├─ ScreenFader [только PreparingDemo]
 ├─ subtitlePanel (TMP) [только Forest — титры AmbushMissionDirector]
 ├─ HUD дрона (OnGUI)              — StrikeHUD / ReconHUD (не uGUI!)
 └─ FpsCounterHud (OnGUI, автосоздание)
DebriefCamera, BackgroundCamera
EventSystem
```

**Важно:** игровой HUD (перекрестие, рамка цели, дальномер, шум РЭБ) **не является** частью Canvas — он рисуется IMGUI-вызовами из скриптов на префабах дронов. Поэтому его нельзя настроить в префабе UI: только через `DroneConfig` и `SettingsService.Data.gameplay`.

---

## 3. Внутреннее устройство UI-префабов

### 3.1 `MissionSelectPanel.prefab`

```
MissionSelectPanel [Image (непрозрачный PanelFill), CanvasGroup, MissionSelectScreen, MissionMenuController]
├─ Frame (FrameTop/Bottom/Left/Right + CornerTL/TR/BL/BR — красные скобки)
├─ Header (1760×110, верх 40)
│   ├─ Title           32 Bold, белый
│   ├─ Subtitle        ← headerSubtitleLabel (13)
│   ├─ TotalScore      ← totalScoreLabel (15, справа)
│   ├─ Rule            2 px
│   └─ BackButton      160×56, HUF #1 + AccentRed ← backButton
├─ Scroll View (x 80…940, y 186…1000, вертикальный ScrollRect)
│   ├─ Viewport (840×814, только RectMask2D — без Image-based Mask)
│   │   └─ Content (VLG + CSF)      ← cardsContainer (карточки создаются в рантайме)
│   │       └─ (MissionCard 840×150, spacing 12)
│   └─ Scrollbar Vertical (8 px, HUF #28)
└─ MissionDetailsPanel [MissionDetailsPanel]  (860×814)
    ├─ Banner (812×150) / BannerCode / TechLine
    ├─ StatusIcon / StatusLabel / RewardLabel
    ├─ Title / Description
    ├─ StatsLabel
    │   ├─ TimeTile (394×72) → Label / Value
    │   ├─ ScoreTile          → Label / Value
    │   ├─ CompletionsTile    → Label / Value
    │   └─ TargetsTile        → Label / Value
    ├─ Note / GoalsLabel
    │   ├─ GoalLine0 / GoalMarker0
    │   ├─ GoalLine1 / GoalMarker1
    │   └─ GoalLine2 / GoalMarker2
    ├─ EmptyState
    └─ LaunchButton (812×64, AccentRed) / Label ← launchButton, launchLabel
```

`MissionSelectScreen.Populate()` очищает `Content` и инстанцирует `MissionCard` на каждую миссию; флаги «открыта/пройдена» карточка получает аргументами (`ProgressService.IsUnlocked/IsCompleted`).

> С 22.11.2026 `Content` — вертикальный список (`VerticalLayoutGroup` + `ContentSizeFitter`), карточки 840×150, `cardsContainer` указывает на `Scroll View/Viewport/Content`. Горизонтальный `Scrollbar Horizontal` удалён. Все размеры рассчитаны на Canvas 1920×1080.

> **22.11.2026 — исправлена невидимость карточек (legacy `Mask`).** На `Viewport` вместе с `RectMask2D` оставался старый `UnityEngine.UI.Mask` (`showMaskGraphic = false`) от первой вёрстки, а билдер при переходе на `RectMask2D` сделал `Image` у `Viewport` полностью прозрачным (`alpha = 0`). Материал `Mask` включает `UNITY_UI_ALPHACLIP`, поэтому при alpha = 0 он **не пишет stencil**, и все дочерние графики карточек (`Stencil Comp: Equal`) отсекаются — левая колонка выглядела сплошным чёрным прямоугольником. Клики при этом работали: raycast-фильтр `Mask`/`RectMask2D` проверяет только попадание в прямоугольник, а не альфу. Теперь `Viewport` содержит **только** `RectMask2D` (компоненты: `RectTransform`, `CanvasRenderer`, `Image` с alpha 0, `RectMask2D`), а `MissionSelectPanelBuilder.BuildScrollView()` при каждом прогоне удаляет legacy `Mask` через `DestroyImmediate`. Подложка списка — `TacticalUITheme.MissionListFill` (0.015/0.016/0.018, α 0.96), чтобы плашки карточек читались.

### 3.2 `MissionCard.prefab`

```
MissionCard [Image (flat 0.13/0.14/0.155 ≈ #212428), Button, LayoutElement 840×150, MissionCardView]
├─ Preview (Image 200×126, слева)       ← previewImage
├─ NameText (TMP 22 Bold)               ← nameLabel
├─ MetaText (TMP 14, TextSecondary)     ← metaText
├─ isCompleted (HUF #20, 28×28)          ← completedBadge
├─ LockedOverlay (0.72 scrim)
│   ├─ Image (HUF #50, 40×48)
│   └─ Caption (TMP 13 Bold «ЗАБЛОКИРОВАНО»)
└─ SelectedFrame (3 px, AccentRed)       ← selectedFrame
```

> Плитка карточки — плоская заливка: спрайт `#12` при растяжении 840×150 давал внутренние линии поверх превью и текста.

> С 22.11.2026 заливка плашки — `TacticalUITheme.MissionCardFill` (0.13/0.14/0.155 ≈ `#212428`), контрастнее подложки списка `MissionListFill`; раньше было 0.10/0.11/0.12, и карточка почти сливалась с фоном. Порядок отрисовки детей фиксирован: `Preview → NameText → MetaText → isCompleted → LockedOverlay → SelectedFrame` (иначе `MetaText` рисовался поверх затемнения и оставался ярким на заблокированной карточке). Порядок задаётся в `MissionSelectPanelBuilder.NormalizeCardOrder()`.

### 3.3 `SettingsPanel.prefab`

```
SettingsPanel [Image = Scrim (0,0,0,0.62), CanvasGroup, SettingsPanel]
└─ Plate (1400×920 по центру, flat PanelFill)
    ├─ Frame (FrameTop/Bottom/Left/Right + Corner* — красные скобки, raycast=false)
    ├─ Header (1400×80, flat 0.085/0.09/0.10)
    │   ├─ Title (TMP 30 Bold, «НАСТРОЙКИ»)
    │   ├─ ResetButton 168×44 (HUF #1 + AccentRed) ← resetButton
    │   ├─ CloseButton 48×48 (HUF #1 + AccentRed) → Icon (HUF #38) ← closeButton
    │   └─ AccentRule (3 px, AccentRed)
    ├─ Tabs (1336×52, HLG spacing 8)
    │   ├─ Tab_Graphics  ← tabButtons[0]  + Accent (3 px, активная)
    │   ├─ Tab_Controls  ← tabButtons[1]
    │   ├─ Tab_Audio     ← tabButtons[2]
    │   └─ Tab_Gameplay  ← tabButtons[3]
    └─ ScrollView (1336×740, верх 156)
        ├─ Viewport (RectMask2D)
        │   └─ Content (VLG spacing 6, CSF) ← SettingsListBuilder.content
        └─ Scrollbar Vertical (8 px, HUF #28)
```

> С 22.11.2026 `ResetButton` **входит в префаб** (`resetButton` сериализован), а сценовые дубликаты в `MainMenu` и `Bunker(main)` удалены — в каждой сцене ровно одна кнопка. Верх списка (`156`) ниже вкладок (`92…144`) на 12 px, поэтому первая строка больше не перекрывает вкладки.

Строки настроек **не хранятся в префабе** — они создаются кодом `SettingsListBuilder.Build(descriptors)` по `List<SettingDescriptor>`. Строка 50 px: подпись TMP 17 (`flexibleWidth`), значение TMP 16 (88 px, справа), контрол 320 px (слайдер/дропдаун) или тумблер 32 px. Спрайты строк — HUF Pack: `#49` дорожка/заливка, `#22` ручка, `#20/#21` чекбокс, `#2` плашка дропдауна, `#39` стрелка, `#0` опции. Раскрытые опции выравниваются по правому краю под кнопкой (320×40), а не на всю ширину списка. `SettingsPanel` формирует 4 набора дескрипторов:

| Вкладка | Кол-во параметров | Примеры |
|---|---|---|
| Графика | 13 | режим окна, разрешение, VSync, render scale, лимит FPS, качество, FOV дрона, дальность прорисовки (дрон), дистанция теней дрона, дистанция/мягкость теней бункера, постобработка бункера, Motion Blur бункера |
| Управление | 10 | чувствительность FPS, инверсии (FPS/дрон/гимбал), скорость стика, центрирование, скорость гимбала, скорость газа, мёртвая зона, фиксация газа |
| Аудио | 8 | мастер, музыка, SFX, моторы, РЭБ, UI, голос, приглушение в меню |
| Геймплей | 12 | прозрачность/масштаб HUD, прицел, толщина линий, рамка захвата, субтитры и их размер, подсказки взаимодействия, FPS-счётчик, тряска камеры/газа, подсказки обучения |

---

## 4. Логика «окон» в бункере

Четыре слоя модальности:

| Слой | Кто открывает | Кто закрывает | Признак «открыто» |
|---|---|---|---|
| Пауза | `BunkerPauseController.Update` (`Esc`) | `Resume()` / `Esc` | `BunkerPauseController.IsOpen`, `IsPaused` |
| Настройки | кнопка паузы | `CloseButton`, `Esc` | `SettingsPanel.IsOpen` |
| ComputerOS | `ComputerInteractable` (сидя, `E`) | `E` / `Esc` | `ComputerUIController.IsOpen` |
| Меню миссий | `DesktopIcon`, `BunkerStateRestorer` | `BackButton`, `Esc` | `MissionMenuController.IsOpen` |

**С 22.11.2026 `Time.timeScale` и `Cursor` принадлежат координатору `BunkerModalStack`** (`Assets/Scripts/Interaction/BunkerModalStack.cs`), а не отдельным окнам:

* окна регистрируются в `Awake` (`Register`) и снимаются в `OnDestroy` (`Unregister`); `SettingsPanel` сообщает о себе через `NotifySettingsChanged`;
* `AnyOpen` = `computer.IsOpen || missionMenu.IsOpen || pause.IsOpen || settings.IsOpen` (уничтоженные объекты считаются закрытыми, состояние не протекает между сценами);
* `Apply()` после каждого изменения: открыто хотя бы одно окно → `timeScale = 0`, `Cursor.None` + visible; иначе → `timeScale = 1`, `Cursor.Locked` + hidden.

Поэтому закрытие панели миссий больше не зависит от того, открыт ли за ней рабочий стол: если ОС открыт — остаётся `timeScale = 0`, если нет — управление сразу возвращается игроку. `PlayerInteractionDetector` при `AnyOpen` скрывает подсказку и не рейкастит.

Чтобы `Esc` не закрывал сразу всё, в коде остались «окна блокировки» по 0.2 с (`lastOpenTime`, `lastCloseTime`, `LastClosedTime`). Дополнительно (22.11.2026): пока `ComputerUIController.IsOpen == true`, игрок **не может встать со стула** — `ChairInteractable.IsStandUpBlocked` блокирует `Пробел`; `Esc`/`E` закрывают рабочий стол, и только после этого вставание разблокируется. Порядок проверок в `BunkerPauseController.Update()`:

```
restorer.IsRestoring                        → выйти
состояние не Gameplay/Paused                → выйти
нажали Esc:
   пауза открыта? → (настройки открыты? закрыть настройки) иначе Resume
   меню миссий открыто / закрылось <0.2 с   → выйти
   ComputerOS открыт / был открыт в прошлом кадре → выйти
   игрок сидит                              → выйти
   прошло <0.25 с с OnEnable                → выйти
   иначе OpenPause()
```

Это по-прежнему самая хрупкая часть UI-логики бункера, но теперь у неё два обязательных правила: новое модальное окно нужно (1) вписать в список проверок `Esc` и (2) зарегистрировать в `BunkerModalStack` — иначе `timeScale`/курсор не будут учитывать его при закрытии остальных окон.

### 4.1 Что изменилось в этапах Fix-QA (22.11.2026)

| Место | Было | Стало |
|---|---|---|
| Модалки бункера | каждое окно само ставило `timeScale`/курсор | единый `BunkerModalStack.Apply()` |
| `MissionMenuController.CloseMissionMenu()` | не восстанавливал состояние (предполагал открытый ОС) | `Apply()`: ОС открыт → 0, иначе 1 + захват курсора |
| `ComputerUIController.CloseDesktop()` | безусловно `timeScale = 1` + lock | `Apply()` (учитывает другие окна) |
| `PlayerInteractionDetector` | подсказка висела поверх терминала, рейкаст при `timeScale = 0` | `AnyOpen` → `HideHint()` + выход |
| Панель миссий | первый `Esc` закрывал только детали | один `Esc` = «НАЗАД» |
| Открытие панели миссий | ничего не выбрано | автовыбор первой доступной миссии |
| Пустая панель деталей | кнопка «ЗАПУСТИТЬ» выглядела рабочей | `interactable = false`, «ВЫБЕРИТЕ ОПЕРАЦИЮ» |
| Настройки | сброс с одного клика, дропдаун «запоминался» | «Сброс» → «Подтвердить?» (3 с), `CollapseDropdown()` при закрытии |
| `MainMenu` | `MissionSelectPanel` (мёртвый UI) | удалён из сцены; `missionSelect` убран из `MainMenuController` |
| `ExitButton` | выход с одного клика | «Выход» → «Выйти?» (3 с, `Esc` отменяет) |

---

## 5. HUD (IMGUI) — как это работает и как менять

Все игровые HUD-элементы рисуются в `OnGUI()` и **не** участвуют в UI-канвасе:

| Элемент | Скрипт | Что рисует |
|---|---|---|
| Перекрестие + рамка захвата + `[DRONE]` / `ARMED` + предупреждение РЭБ | `StrikeHUD` | `GUI.DrawTexture` (1×1 белая текстура), `GUI.Label` |
| Дальномер (LRF), сетка координат, адаптивная рамка цели, дистанция | `ReconHUD` | то же + `Camera.WorldToScreenPoint` по 8 вершинам `Renderer.bounds` |
| «Снег», горизонтальные разрывы, джиттер кадра/FOV | `EWReceiver` | процедурная `Texture2D` (шум) + `cam.rect.y` + `FPVZoom.SetFovOffset` |
| FPS | `FpsCounterHud` | `GUI.Label` в правом верхнем углу |

Общие правила отрисовки:

1. Ранний выход, если `SceneLoader.Instance.IsLoading` или `MissionUIManager.IsHudHidden`.
2. Масштаб/прозрачность — `HudStyleService.Begin()` / `End(prevMatrix)` вокруг всей отрисовки.
3. Цвет и толщина линий — из `DroneConfig` (`hudColor`, `reconColor`, `hudLineWidth`, `crosshairSize`, `targetBoxSize`) с приоритетом пользовательских настроек (`HudStyleService.CrosshairSize/LineThickness`).
4. Шум РЭБ (`EWReceiver.OnGUI`) рисуется всегда: настройка «Уменьшить опасные эффекты» удалена 22.11.2026 вместе с графическими «Туман у дрона», «Дисторсия объектива», «Искажения РЭБ», «Усиление ПНВ».

**Как менять HUD:**

* Хочешь другой вид прицела → `StrikeHUD.OnGUI()` / `ReconHUD.OnGUI()` (это единственное место).
* Хочешь глобальный стиль (масштаб/прозрачность) → `HudStyleService`.
* Хочешь убрать HUD из конкретной ситуации → выставляй `MissionUIManager.IsHudHidden` (сейчас управляется только паузой/дебрифом/crash-панелью).

---

## 6. Правила безопасной правки UI

1. **`CanvasGroup` обязателен** на любом объекте, который наследует `UIScreen` (иначе `Show()/Hide()` пишет `LogError` и ничего не делает). `MainMenuController.OnValidate()` предупреждает об этом.
2. **Z = 0** для панелей в Screen Space Overlay: `SettingsPanel.NormalizeZ()` — вынужденная мера. Новые панели лучше сразу ставить в `localPosition.z = 0`.
3. **Не редактировать `MissionSelectPanel.prefab` / `SettingsPanel.prefab` вручную «насовсем»** — есть Editor-билдеры, которые могут перезаписать структуру. С 22.11.2026 оба билдера работают **in-place** (`PrefabUtility.LoadPrefabContents` → мутация → `SaveAsPrefabAsset`) и идемпотентно: объекты ищутся по имени, `Header`/`MissionDetailsPanel` не пересоздаются, fileID компонентов сохраняются, ссылки сцен не рвутся.
4. **Ссылки задаются в инспекторе**, а не через `GetComponentInChildren` (кроме фолбэков в `Awake`). При переименовании объектов ломаются: `MissionMenuController.FindBackButton()` (ищет `"BackButton"`), `DesktopIcon`, `CloseMissionBoardButton`. `ResetButton` живёт в `SettingsPanel.prefab`; **не добавляйте его повторно в сцены** — иначе `FindButton()` возьмёт случайную кнопку.
5. **Кнопки подписываются кодом** (`onClick.AddListener`), а не в инспекторе — при копировании префаба подписки не теряются, но при открытии префаба в редакторе список `OnClick` будет пустым. Это нормально для этого проекта.
6. **`Time.timeScale = 0`** ставит почти каждое окно. Если добавляешь своё окно — не забудь вернуть `timeScale = 1` при закрытии и восстановить `Cursor.lockState`.
7. **`Time.timeScale` и `Cursor` — глобальные**: любое окно может «испортить» состояние другому. Отлаживай через `BunkerPauseController.IsPaused`, `ComputerUIController.IsOpen`, `MissionMenuController.IsOpen`.

---

## 7. Какие UI-ассеты являются внешними

* `Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-png/HUF Pack.png` — основной атлас спрайтов меню/панелей.
* `Assets/UnityTechnologies/ParticlePack/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` — шрифт TMP (единственный, используется и в меню, и в миссиях).
* `Assets/Materials/DefaultUI.mat` — материал UI в меню.
* `Assets/Art/Videos/24e0aa0c-d9f9-461b-bd0e-22f8c02b0ced.mp4` + `MenuVideoRT.renderTexture` — видео-фон главного меню.
* `Assets/Textures/DesktopBackground/DesktopBackground_plate.png` — пластина рабочего стола бункера (640×360, кадр 0 исходного GIF: пустой радар, карта, рамка), **очищенная** 22.11.2026: мусорный текст справа сверху заклеен клоном фона, круг радара приведён к чистому графиту, кольца 26/44.5/63 и обод **r = 79.5** с центром **(524, 179)** перерисованы ровными окружностями; буквы N/E/S/W и красные дуги сохранены (подробности — §9.5).
* `Assets/Textures/DesktopBackground/DesktopBackground_sweep.png` — «лепесток» радара **1024×1024 RGBA32**, сгенерированный процедурно в `DesktopBackgroundSetup.cs`: сектор 65°, передняя кромка `#FF2A55`, шлейф `#DB143D` (alpha 0.42 → 0), за пределами сектора `alpha = 0`. Вращается скриптом `DesktopRadarSweep`, поэтому анимация зациклена без разрыва.

> Шрифт TMP один на весь проект, поэтому «разные шрифты в разных меню» сейчас невозможны без добавления новых TMP-ассетов.

---

## 8. Единая система оформления UI (22.11.2026)

Эталон — главное меню: металлические плиты `HUF Pack.png`, белый TMP-текст, тёмно-красный акцент `#DB143D`. Все экраны `MainMenu` и `Bunker(main)` приведены к нему.

### 8.1 Палитра

| Токен | Значение | Где |
|---|---|---|
| `AccentRed` | `(0.86, 0.08, 0.24)` #DB143D | выход, закрытие, сброс, «Назад», «Запустить», красные уголки рамки |
| `PanelFill` | `(0.045, 0.05, 0.055, 0.985)` | тело модальной панели |
| `PanelFillSoft` | `(0.075, 0.08, 0.09, 0.98)` | панель деталей миссии, шапка ОС |
| `MissionListFill` | `(0.015, 0.016, 0.018, 0.96)` | подложка списка карточек миссий (`Scroll View`) |
| `MissionCardFill` | `(0.13, 0.14, 0.155)` ≈ `#212428` | плашка карточки миссии |
| `ControlPlate` | `(0.16, 0.17, 0.19)` | плашка дропдауна/вкладки |
| `Scrim` | `(0,0,0,0.62)` | затемнение под модалкой |
| `TextPrimary` / `TextSecondary` / `TextCaption` | `white` / `white 0.62` / `white 0.45` | заголовки / подписи / мелкие метки |
| `Steel` | `(0.72, 0.75, 0.78)` | заливка слайдера, ручка скролла |
| `Divider` | `(1,1,1,0.16)` | разделители, контур рамки |

Второй красный `#DC3132`, синий `(0.3,0.55,0.85)` и зелёный `(0.3,0.85,0.4)` из контролов удалены. Семантические статусы миссий остались приглушёнными: выполнено `(0.55,0.72,0.45)`, не выполнено `(0.9,0.68,0.22)`, заблокировано `(0.86,0.2,0.24)`.

### 8.2 Спрайты (`Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-png/HUF Pack.png`)

| Роль | Спрайт |
|---|---|
| Кнопка/вкладка | `UI Starter Pack_1` (281×97) |
| Опасная кнопка | `UI Starter Pack_1` с тинтом `AccentRed` |
| Контрол/дропдаун | `UI Starter Pack_2` (272×93) |
| Плоская плашка | `UI Starter Pack_0` (477×66) |
| Ручка/трек слайдера | `UI Starter Pack_49` (68×10), ручка `#22` |
| Чекбокс | `UI Starter Pack_20` (вкл) / `_21` (выкл) |
| Стрелка дропдауна | `UI Starter Pack_39` (52×31) |
| X / замок / список | `#38` / `#50` / `#15` |
| Пиксель-заливка | `UI Starter Pack_28` (2×2) |

Крупные панели (`SettingsPanel`, `MissionSelectPanel`) используют **плоскую заливку + собственную рамку** (`FrameTop/Bottom/Left/Right` + красные `Corner*`), а не растянутый `#12`/`#7`: у этих спрайтов есть внутренние декоративные линии, которые проходят поверх строк и карточек.

### 8.3 Типографика (только `LiberationSans SDF`)

| Роль | Размер |
|---|---|
| Заголовок экрана | 32 Bold, uppercase, spacing +2 |
| Заголовок модалки | 30 Bold |
| Кнопка | 24; вторичная 20 |
| Вкладка | 20 (активная Bold) |
| Подпись строки | 17 |
| Значение | 16 |
| Body | 15 |
| Caption | 11–13 |

Legacy `UnityEngine.UI.Text` в настройках больше не используется. Однострочные подписи — `NoWrap` + Ellipsis; описания миссий — `Normal` + Ellipsis.

### 8.4 Геометрия и состояния

* Экран 1920×1080, поля 64 px; модалка настроек 1400×920, пауза 560×620, магазин 720×420.
* Кнопки 60 px (основная), 44 px (вторичная/сброс/закрыть), 48×48 (X).
* Строка настроек 50 px + 6 px; шапка 80 px; вкладки 52 px; список начинается через 12 px после вкладок.
* Карточка миссии 840×150; панель деталей 860×814; баннер 812×150; плитка статистики 394×72; LaunchButton 812×64.
* `ColorBlock` у всех кнопок: normal `1`, highlighted `0.96`, pressed `0.78`, disabled `(0.5,0.5,0.5,0.6)`, fade 0.1. Опасные кнопки — тот же блок поверх красного `Image.color`.

### 8.5 Где реализовано

| Файл | Роль |
|---|---|
| `Assets/Scripts/UI/TacticalUITheme.cs` | константы палитры/размеров, `ButtonColorBlock()` |
| `Assets/Scripts/UI/SettingsPanel.cs` | активная вкладка (цвет `Image`, Bold, красная полоса), сброс скролла, сброс настроек |
| `Assets/Scripts/UI/SettingsListBuilder.cs` | строки на TMP + HUF-спрайтах, drop-down 320 px справа |
| `Assets/Scripts/Editor/TacticalUIBuilder.cs` | загрузка спрайтов/шрифта, `EnsureImage/EnsureText/EnsureButton`, `BuildTacticalFrame` |
| `Assets/Scripts/Editor/SettingsPanelPrefabBuilder.cs` | in-place стилизация `SettingsPanel.prefab` |
| `Assets/Scripts/Editor/MissionSelectPanelBuilder.cs` | in-place стилизация `MissionSelectPanel.prefab` + `MissionCard.prefab` |
| `Assets/Scripts/Editor/DesktopBackgroundSetup.cs` | меню `Tools ▸ DroneSimulator ▸ UI`: импорт-настройки кадров фона и привязка анимированного фона к `ComputerOS` в `Bunker(main)` |

### 8.6 Проверка (22.11.2026)

Play Mode, `MainMenu` и `Bunker(main)`:
- вкладки: 13 / 10 / 8 / 12 строк, активная вкладка белая с красной полосой и Bold, неактивные 0.55;
- дропдаун раскрывается (3 опции 320×40 справа), «Сброс» оставляет панель открытой и пересобирает строки, «X»/`Esc` закрывают;
- пауза: кнопки 460×60, persistent-вызовы `PauseManager.Resume/OpenSettings/ExitToMainMenu` сохранены, выход красный;
- миссии: карточки 816×150 (внутри 860-й колонки), детали 860×814, `LaunchButton` 812×64 с отступом 14 px, «Назад» закрывает меню;
- список миссий (22.11.2026): обе карточки видимы и читаемы — плашка `#212428` на подложке `MissionListFill`, превью 200×126, заголовок 22 Bold, метаданные, замок + «ЗАБЛОКИРОВАНО» (затемнение накрывает всю карточку), выбранная подсвечена рамкой `#DB143D` и детали справа переключаются (см. §3.1);
- подсказки: компактные белые надписи без плашек (TMP 20 / 18 px, `Outline` 0,0,0,0.85 при 1.5 px), `InteractionHint` на 75 % высоты, `SittingHint` в 52 px от низа; при одновременном показе (сидя + взгляд на терминал) вертикальный зазор 162 px — без наложений;
- рабочий стол: иконка 160×160 не пересекается с шапкой (56 px) и футером (48 px);
- консоль Unity — 0 ошибок; геометрические проверки — без наложений и выходов за границы.

---

## 9. Анимированный фон рабочего стола `ComputerOS` (22.11.2026)

Задача: заменить статичный `comp.png` фоном оператора БПЛА (вращающийся радар, пустая левая зона под ярлыки) так, чтобы радар крутился при `Time.timeScale = 0`, а цикл был бесшовным.

### 9.1 Что показала проверка исходника

| Что проверили | Результат |
|---|---|
| Импорт GIF в Unity | `textureType: Default`, `spriteMode: 0`, flipbook 1×1 — Unity берёт **только первый кадр**, анимации нет |
| Параметры GIF | 640×360, 30 кадров по 10 cs → 10 fps, длина 3.0 с, loop = 1 |
| Разница соседних кадров (яркость) | 1.1–1.4, а стык `f29 → f0` — **3.66** (в 2.6× больше шага) |
| Угол лепестка по кадрам | голова делает полный оборот, но ширина следа растёт 15° → 80°, а `f0` — вообще пустой радар |
| Вывод | GIF — это «разгон радара», а не цикл: покадрово идеальный цикл из него собрать нельзя |
| `ffmpeg` в системе | отсутствует; статического `MediaEncoder.EncodeVideo` в Unity 6000.6 нет → mp4 пришлось бы писать своим энкодером |

### 9.2 Принятое решение: пластина + вращаемый лепесток

* **Пластина** — кадр 0 (пустой радар, карта, рамка) → `DesktopBackground_plate.png`. Назначается на `Image` самого `ComputerOS` (растянут на весь канвас, `Simple`, `preserveAspect = false`) — ровно то место, где раньше был `comp.png`.
* **Лепесток** — с 22.11.2026 генерируется процедурно (`DesktopBackgroundSetup.GenerateSweepTexture`), а не вырезается из GIF: 1024×1024 RGBA32, ровный сектор 65°, `alpha = 0` вне сектора (см. §9.5). Старый вариант — пиксельная разница кадра 14 и пластины с обрезкой по R ≈ 105.5 — тянул за лучом серый «призрак» всего радара и не вписывался в обод.
* **`DesktopSweep`** — дочерний объект с индексом **0** (значит, под `Btn_Missions`, `TerminalHeader` и `TerminalFooter`), `Image` = лепесток, `raycastTarget = false`, квадрат **473.55×473.55**, `anchorMin = anchorMax = (0.5, 0.5)`, `pivot (0.5, 0.5)`, `anchoredPosition (612, 3)` → центр квадрата совпадает с центром радара (524;179), вращение идёт строго вокруг него.
* **`DesktopRadarSweep`** вращает слой по `Time.unscaledDeltaTime` (120°/с по часовой) — при `timeScale = 0` вращение продолжается. Когда `IsOpen == false`, кадры не тратятся: `CloseDesktop()` не выключает объект ОС, поэтому проверка идёт по `IsOpen`.
* **Цикличность**: поворот на 360° неотличим от 0°, поэтому шва нет вообще — не нужны ни обрезка кадров, ни кросс-фейд, ни «склейка» стыка.

### 9.3 Импорт

`Tools ▸ DroneSimulator ▸ UI ▸ Prepare Desktop Background Assets` — Sprite/Single, mipmaps off, max 1024, Bilinear, Clamp, **Uncompressed**. Сжатие `Normal` (DXT) давало блочность на контурах карты и кольцах радара (видно на контрольном скриншоте), поэтому кадры 640×360 держим без сжатия (~0.9 МБ на текстуру).

### 9.4 Проверка (Play Mode, `Bunker(main)`)

1. `OpenDesktop()` → `IsOpen = true`, `Time.timeScale = 0`, `DesktopSweep.localEulerAngles.z` растёт (0 → 179° → 350°).
2. Фон заполняет 1920×1080, иконка «МИССИИ» 160×160 читается в свободной левой зоне, шапка и футер ОС — поверх фона.
3. `Btn_Missions.onClick` → `MissionMenuController.IsOpen = true`, `CanvasGroup.alpha = 1` (панель миссий открывается).
4. `CloseDesktop()` → `IsOpen = false`, `timeScale = 1`, `z` замирает (138.87 → 138.87 спустя несколько секунд); повторное открытие возобновляет вращение.
5. Консоль Unity — **0 ошибок и варнингов**.

### 9.5 Процедурный луч радара и очистка пластины (22.11.2026)

Причина артефактов: лепесток был вырезан из GIF вместе с остаточным «призраком» всего радара и обрезан по `R ≈ 105.5` — шире обода, а пивот стоял в `(526, 184)`, хотя перекрестие радара на пластине — `(524, 179)`. Отсюда биение при обороте и серые пятна, которые «тянулись» за лучом.

| Что | Было | Стало |
|---|---|---|
| Пивот `DesktopSweep` | `(0.821875, 0.488889)` = (526;184) | `(0.5, 0.5)` внутри квадрата `473.55×473.55`, `anchoredPosition (612, 3)` = центр (524;179) |
| Размер слоя | растянут на 1920×1080 | квадрат `473.55×473.55`; кромка луча = внутренняя кромка обода (`R = 78` px источника ≈ 234 px канваса) |
| Лепесток | пиксельная разница кадра GIF + мусор по всему кругу | процедурная генерация 1024×1024 RGBA32, `alpha = 0` вне сектора 65° |
| Пластина | мусорный текст справа сверху, зелёный шум в круге, обод-эллипс | текст заклеен клоном фона, круг — чистый графит, кольца/перекрестие/обод перерисованы окружностями |

**Замеры исходной пластины (640×360, ось Y сверху).** Центр — пересечение перекрестия: строка **179**, столбец **524**. Старый обод оказался **эллипсом** 81 × 84.7 (GIF растянут по высоте), кольца — 26 / 44.5 / 63 (по вертикали 27 / 46.5 / 66), красные дуги — окружность `r ≈ 88–91`. Поэтому на очищенной пластине обод и кольца нормализованы в правильную окружность (`r = 79.5` и 26 / 44.5 / 63): иначе круглый луч не может одновременно доходить до обода и не выходить за него.

**Луч (`GenerateSweepTexture`).** 1024×1024 RGBA32, суперсэмплинг 3×3:
* радиус луча 506 px из 512 (запас 6 px до края текстуры, `BeamRadiusFraction = 506/512`);
* голова: `#FF2A55`, полная толщина ≈ 5 px текстуры (≈ 2.4 px на экране) + мягкое свечение;
* шлейф: `#DB143D`, `alpha = 0.42 · exp(-δ/20°) · (1 − δ/65°)^0.7`, ровно 0 на 65°;
* жёсткая отсечка по центру пикселя: за пределами сектора `alpha = 0` (проверено — 0 ненулевых пикселей вне 65°);
* импорт: Sprite/Single, Bilinear, Clamp, mipmaps off, Uncompressed, max 1024.

**Пластина (`CleanPlateTexture`).**
* правый верх: нечитаемый текст заклеен клоном фона из той же строки левее (−96 px), поэтому сохраняется родной дизеринг; отдельно удалён ряд на одной строке с «RADAR/ONLINE» (зеркальный тайл чистого промежутка x 508–535 с пофазным сдвигом строк). Горизонтальная линия под надписью (строка 47) клонируется вместе с фоном и остаётся ровной, «RADAR/ONLINE» и рамка x = 625 не затрагиваются;
* круг: эллипс старого обода (`a = 83`, `b = 86.5`) заливается чистым графитом (медиана фона, виньетка, шум ±1) — `maxChroma = 4`, зелёный шум уходит;
* буквы N/E/S/W защищены маской `lum ≥ 130` в их боксах + дилатация 2 px;
* кольца (26 / 44.5 / 63), перекрестие и обод `r = 79.5` рисуются заново с аналитическим сглаживанием; красные дуги (`r ≈ 88–91`), карта и надписи не затрагиваются;
* операция идемпотентна: круг каждый раз заново заливается графитом, затем кольца рисуются поверх.

**Привязка (`BindBunker`).** `ApplySweepRect` считает размер и позицию от эталонного канваса 1920×1080: `size = 2 · 78 · 3 / (506/512) = 473.55`, `anchoredPosition = (612, 3)`, `pivot = (0.5, 0.5)`, `anchorMin = anchorMax = (0.5, 0.5)`. Если `ComputerOS` в сцене не 1920×1080, билдер пишет предупреждение: лейаут, как и остальной UI бункера, рассчитан на 1920×1080 (пластина тянется на весь канвас).

**Проверка (Play Mode, `Bunker(main)`, 22.11.2026).**
1. Два контрольных кадра с разными углами луча (замеры `DesktopSweep.localEulerAngles.z` между кадрами: 257.6° и 148.0°): луч вращается вокруг центра радара, кромка доходит до обода и не выходит за него, шлейф спадает плавно, пятен и «призраков» нет.
2. Численно: `DesktopSweep` — `size 473.55`, `pivot (0.5, 0.5)`, мировой центр `(1572, 543)` = `524/640·1920` и `(1 − 179/360)·1080`; радиус луча 234 px против 238.5 px обода.
3. Пластина: внутри круга `maxChroma = 4`; в зоне бывшего текста нет пикселей > 60 (mean 9.9 против 9.8 у соседнего чистого фона); буквы N/E/S/W и красные дуги на месте.
4. Консоль Unity — 0 ошибок; `Temp/UIAudit/` удалён.

---

## 10. UI тренировочной миссии `PreparingDemo` в стиле Tactical Metal (22.11.2026)

Раньше панели миссии были legacy-вёрсткой: спрайт `UISprite`, серая заливка кнопок `(0.25,0.25,0.25)`, затемнение 0.39, текст «Вы рызбились», плейсхолдеры `MISSION`/`STATS`, кнопки 320×70. Теперь панели собираются по тому же стандарту, что `MainMenu` и `Bunker(main)`.

### 10.1 Структура панелей

```
PausePanel / CrashPanel            DebriefPanel
 └ Scrim (0,0,0,0.62)               └ Scrim (0,0,0,0.62)
    └ Plate 560×620                    └ Plate 720×640
       ├ Frame 2 px + 4 красных уголка    ├ Frame 2 px + 4 красных уголка
       ├ Title 30 Bold                    ├ DebriefTitle 30 Bold
       ├ Subtitle 15 (0.62)               ├ AccentRule 3 px #DB143D
       ├ AccentRule 3 px #DB143D          ├ DebriefStats 18 (0.62), 600×170
       ├ 3 × Button 460×60                ├ RetryButton     460×60 «ПОВТОРИТЬ»
       └ Footer 11 (0.45)                 ├ ReturnButton    460×60 «ВЕРНУТЬСЯ В БУНКЕР»
                                          ├ MainMenuButton  460×60 красная «В ГЛАВНОЕ МЕНЮ»
                                          └ Footer 11 (0.45)
```

* Плита — `UI Starter Pack_0` (HUF Pack), заливка `TacticalUITheme.PanelFill`; кнопки — `UI Starter Pack_1`, `ColorBlock` из `TacticalUITheme.ButtonColorBlock()`.
* «Опасные» действия (выход в бункер/меню) — заливка `#DB143D`, остальные — белые с тинтом.
* Тексты: «БОРТ ПОТЕРЯН» / «СВЯЗЬ С БОРТОМ ПРЕРВАНА» / «Перезапустить»; исправлены «Вы разбились», лишняя кавычка и хвостовые пробелы.
* Кнопки подписываются **кодом** (`MissionUIManager` / `DebriefController`), persistent-вызовов нет; клики и наведение озвучены (`UISoundService`, `AudioBus.UI`).
* `SettingsPanel` в миссии — тот же инстанс префаба, что в бункере: 4 вкладки 13/10/8/12.

### 10.2 Подсказка инструктора и субтитры

```
InstructionText (Canvas, top-right 460×176, inset 40)
 ├ Image = UI Starter Pack_0 (0.03/0.035/0.04, α 0.92)
 ├ Frame + AccentBar слева (4 px #DB143D)
 └ Label (TMP 18, TopLeft, margin 24/14) ← TrainingManager.instructionText
```

* Клавиши в тексте выделены `#DB143D` (было `#00FF00`).
* Корень скрывается паузой (`MissionUIManager.SetHudVisible`), настройка «Подсказки обучения» очищает текст.
* `SubtitleText` (низ экрана, TMP 24, Center) — канал уведомлений инструктора; `FadeText` заголовков глав — `Center`, 64 px (заголовок больше не смещён влево на ~500 px).

### 10.3 HUD (IMGUI)

`ReconHUD`/`StrikeHUD` переведены на русский и палитру Tactical Metal:

| Было | Стало |
|---|---|
| `[RECON GIMBAL ACTIVE]`, `ALT:`, `LRF DIST:`, `GRID POS:` | `[КАМЕРА РАЗВЕДКИ АКТИВНА]`, `ВЫС:`, `ДАЛЬНОСТЬ:`, `СЕТКА:` |
| `LOCKED:`, `DIST:` | `ЗАХВАТ:`, `ДИСТ:` |
| `[FPV-7 HEAT]`, `ARMED / HEAT` | `[УДАРНЫЙ БОРТ · FPV-7 HEAT]`, `ВЗВЕДЁН / КУМУЛЯТИВНЫЙ` |
| `[WARNING: EW JAMMING n%]` | `[ВНИМАНИЕ: ПОМЕХИ РЭБ n%]` |
| зелёный `hudColor`/`reconColor` из конфига | `TacticalUITheme.Steel`, красный `AccentRed` только для рамки захвата и предупреждений |

### 10.4 Проверка (Play Mode, 22.11.2026)

1. `PausePanel`: scrim 0.62, плита 560×620, кнопки 460×60 (170/242/314 сверху), «Выйти в бункер» красная; клик «Продолжить» → `timeScale = 1`, `FSM = Gameplay`, звук снят с паузы.
2. «Настройки» → `SettingsPanel` поверх паузы, вкладки 13/10/8/12; `Esc` закрывает настройки (пауза остаётся), второй `Esc` снимает паузу.
3. `InstructionText`: плашка + Label 18, клавиша `[W]` цвета `#DB143D`; в паузе корень скрыт, после — текст восстановлен.
4. `DebriefPanel`: «МИССИЯ ВЫПОЛНЕНА» (30, семантический зелёный), статистика 18 «3 / 3 · 175 очков», три кнопки 460×60; «ПОВТОРИТЬ» → `Loading` и перезапуск миссии, «В ГЛАВНОЕ МЕНЮ» → `MainMenu`.
5. `FadeText` — `Center`, 64 px; `SubtitleText` — `Center`, 24 px.
6. Консоль — 0 ошибок и 0 предупреждений проекта.

### 10.5 Клики мышью в панелях миссии (22.11.2026)

Симптом: `Esc` открывал и закрывал паузу, но **любые кнопки мышью** (`PausePanel`: «Продолжить»/«Настройки»/«Выйти в бункер»; `DebriefPanel`: «Повторить»/«Вернуться в бункер»/«В главное меню»; `CrashPanel`) не срабатывали. В `MainMenu` и `Bunker(main)` всё работало.

Причина (Play Mode + исходники пакета Input System):

| Шаг | Что происходит | Где |
|---|---|---|
| 1 | Панель отпускает курсор, чтобы им можно было кликать | `MissionUIManager.ApplyPausedState()` / `ShowCrashPanel()`, `DebriefController.ShowDebrief()` |
| 2 | `DroneFlightController.Update()` выполняется и при `timeScale = 0`; на ЛКМ при `Cursor.lockState == None` захватывает курсор | `DroneFlightController.cs:113-119` |
| 3 | При `Cursor.lockState == Locked` модуль ввода принудительно ставит UI-указатель в `(-1,-1)` | `InputSystemUIInputModule.cs:428` (`CursorLockBehavior.OutsideScreen`, дефолт) |
| 4 | Указатель вне экрана → `raycast = NULL` → `Button.onClick` не вызывается (клик в uGUI срабатывает на отпускании) | замер: `pointer=(-1.00,-1.00)`, `raycast=NULL` |

`RaycastAll` по центру кнопок при открытой паузе до фикса давал корректный топ-хит (`continue`/`settings`/`exit`), т.е. невидимого перекрытия не было — проблема была именно в позиции указателя, а не в иерархии.

Решение (физика дрона не менялась):

* Панели миссии держат курсор в `CursorLockMode.Confined`: это **не** `None` (дрон не захватывает курсор) и **не** `Locked` (указатель UI остаётся на экране). `MissionUIManager.LateUpdate()` при `IsModalOpen` удерживает `Confined` + `visible` (страховка на случай, если дрон выставил `None` в том же кадре).
* `TrainingPanelsBuilder.HardenClickThrough()` (вызывается перед сохранением сцены):
  * `ScreenFader` — `raycastTarget = false` у всех `Graphic`, `CanvasGroup.blocksRaycasts = false` / `interactable = false`, `siblingIndex = 1` (под панелями, сразу после `InstructionText`). Это закрывает второй, «спящий» дефект: `ScreenFader.FadeOut()` включает `blocksRaycasts` на время перехода главы и мог «съедать» клики;
  * `SettingsPanel` — последний сиблинг (поверх паузы);
  * внутри `PausePanel`/`CrashPanel`/`DebriefPanel` `raycastTarget = true` только у `Image` самих кнопок и корневого скрима;
  * `LogError`, если на `Canvas` нет `GraphicRaycaster`, в сцене нет `EventSystem` или на нём нет `InputSystemUIInputModule`.

Проверено в Play Mode (прямой старт `PreparingDemo` и запуск из `Bunker(main)`):

**`PausePanel`** — `FSM = Paused`, `timeScale = 0`, `Cursor.lockState = Confined`, `visible = true`, `IsModalOpen = true`; `RaycastAll` даёт `continue`/`settings`/`exit` первым хитом; ЛКМ не переводит курсор в `Locked`; после «Продолжить» — `Gameplay`, `timeScale = 1`, `Locked` + `visible = false`, дрон управляется.

**`SettingsPanel`** — `Confined`, верхний хит по `CloseButton`; `Esc` закрывает только настройки (пауза остаётся), второй `Esc` снимает паузу.

**`CrashPanel`** — `Confined`; верхние хиты `return`/`exit`.

**`DebriefPanel`** — `Confined`; верхние хиты `RetryButton`/`ReturnButton`/`MainMenuButton`; реальный клик по «Вернуться в бункер» → `Loading` → `Bunker(main)`.

**После запуска из бункера** — `EventSystem` 1, `Canvas` 1, `MissionContext = training_mission`; пауза и клики работают так же.



