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
 ├─ SettingsPanel      [prefab instance]
 ├─ MissionSelectPanel [prefab instance — НЕ открывается из меню]
 └─ VideoBackground (VideoPlayer → MenuVideoRT)
EventSystem
MenuCamera
```

### 2.3 `Bunker(main)`

```
Canvas (Overlay)
 ├─ InteractionCanvas  [InteractionUIController]
 │    ├─ hintGroup / hintText              — подсказка по центру
 │    ├─ sittingHintGroup / sittingHintText — «Встать [Пробел]»
 │    ├─ shopGroup                         — окно «Магазин»
 │    └─ missionBoardGroup                 — окно «Карта миссий»
 ├─ ComputerOS        [ComputerUIController, CanvasGroup]  ← «рабочий стол» ОС
 │    └─ Btn_Missions [DesktopIcon]
 │         └─ MissionSelectPanel [MissionSelectScreen + MissionMenuController]
 ├─ PausePanel        [BunkerPauseController.pausePanel]
 │    ├─ «Продолжить»     → Resume()
 │    ├─ «Настройки»      → OpenSettings() → SettingsPanel.Show()
 │    └─ «В главное меню» → ExitToMainMenu()
 ├─ SettingsPanel     [prefab instance]
 └─ settingsNotice    — заглушка, если SettingsPanel не назначена
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
MissionSelectPanel [CanvasGroup, MissionSelectScreen, MissionMenuController]
├─ Header
│   ├─ Title
│   ├─ Subtitle                     ← headerSubtitleLabel
│   ├─ TotalScore                   ← totalScoreLabel
│   └─ BackButton                   ← backButton (MissionMenuController)
├─ ScrollView
│   ├─ Viewport
│   │   └─ Content                  ← cardsContainer (карточки создаются в рантайме)
│   │       └─ (MissionCard instances)
│   ├─ Scrollbar Vertical / Sliding Area / Handle
│   └─ Scrollbar Horizontal
└─ MissionDetailsPanel [MissionDetailsPanel]
    ├─ Banner / BannerCode
    ├─ StatusIcon / StatusLabel
    ├─ Title / Description / Note
    ├─ RewardLabel
    ├─ StatsLabel
    │   ├─ TimeTile → Value
    │   ├─ ScoreTile → Value
    │   ├─ CompletionsTile → Value
    │   └─ TargetsTile → Value
    ├─ GoalsLabel
    │   ├─ GoalLine0 / GoalMarker0
    │   ├─ GoalLine1 / GoalMarker1
    │   └─ GoalLine2 / GoalMarker2
    ├─ TechLine
    ├─ EmptyState
    └─ LaunchButton / Label          ← launchButton, launchLabel
```

`MissionSelectScreen.Populate()` очищает `Content` и инстанцирует `MissionCard` на каждую миссию; флаги «открыта/пройдена» карточка получает аргументами (`ProgressService.IsUnlocked/IsCompleted`).

### 3.2 `MissionCard.prefab`

```
MissionCard [Button, MissionCardView]
├─ Preview (Image)          ← previewImage
├─ Top / Bottom / Left / Right (декор)
├─ NameText (TMP)           ← nameLabel
├─ MetaText (TMP)           ← metaText
├─ Check                    ← completedBadge
├─ LockedOverlay            ← lockedOverlay
├─ SelectedFrame            ← selectedFrame
└─ Image / Caption          (фон/подпись)
```

### 3.3 `SettingsPanel.prefab`

```
SettingsPanel [CanvasGroup, SettingsPanel, SettingsListBuilder]
├─ Header
│   ├─ Title
│   └─ CloseButton        ← closeButton
├─ Tabs
│   ├─ Tab_Graphics       ← tabButtons[0]
│   ├─ Tab_Controls       ← tabButtons[1]
│   ├─ Tab_Audio          ← tabButtons[2]
│   └─ Tab_Gameplay       ← tabButtons[3]
├─ ScrollView
│   ├─ Viewport / Content ← SettingsListBuilder.content (строки генерируются)
│   └─ Scrollbar Vertical / Sliding Area / Handle
└─ ResetButton            ← resetButton
```

Строки настроек **не хранятся в префабе** — они создаются кодом `SettingsListBuilder.Build(descriptors)` по `List<SettingDescriptor>`. `SettingsPanel` формирует 4 набора дескрипторов:

| Вкладка | Кол-во параметров | Примеры |
|---|---|---|
| Графика | 17 | режим окна, разрешение, VSync, render scale, лимит FPS, качество, FOV дрона, дальность, тени, туман, дисторсия, постобработка бункера, искажения РЭБ, усиление ПНВ |
| Управление | 10 | чувствительность FPS, инверсии (FPS/дрон/гимбал), скорость стика, центрирование, скорость гимбала, скорость газа, мёртвая зона, фиксация газа |
| Аудио | 8 | мастер, музыка, SFX, моторы, РЭБ, UI, голос, приглушение в меню |
| Геймплей | 13 | прозрачность/масштаб HUD, прицел, толщина линий, рамка захвата, субтитры и их размер, подсказки, FPS-счётчик, тряска камеры/газа, подсказки обучения, «уменьшить опасные эффекты» |

---

## 4. Логика «окон» в бункере

Одновременно живут **четыре независимых слоя модальности**, все ставят `Time.timeScale = 0`:

| Слой | Кто открывает | Кто закрывает | Признак «открыто» |
|---|---|---|---|
| Пауза | `BunkerPauseController.Update` (`Esc`) | `Resume()` / `Esc` | `BunkerPauseController.IsOpen`, `IsPaused` |
| Настройки | кнопка паузы | `CloseButton`, `Esc` | `SettingsPanel.IsOpen` |
| ComputerOS | `ComputerInteractable` (сидя, `E`) | `E` / `Esc` | `ComputerUIController.IsOpen` |
| Меню миссий | `DesktopIcon`, `BunkerStateRestorer` | `BackButton`, `Esc` | `MissionMenuController.IsOpen` |

Чтобы `Esc` не закрывал сразу всё, в коде расставлены «окна блокировки» по 0.2 с (`lastOpenTime`, `lastCloseTime`, `LastClosedTime`). Порядок проверок в `BunkerPauseController.Update()`:

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

Это самая хрупкая часть UI-логики бункера: любое новое модальное окно нужно вписать в этот список, иначе `Esc` начнёт закрывать два окна сразу.

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
4. «Уменьшить опасные эффекты» (`gameplay.reduceDangerousEffects`) полностью отключает шум РЭБ.

**Как менять HUD:**

* Хочешь другой вид прицела → `StrikeHUD.OnGUI()` / `ReconHUD.OnGUI()` (это единственное место).
* Хочешь глобальный стиль (масштаб/прозрачность) → `HudStyleService`.
* Хочешь убрать HUD из конкретной ситуации → выставляй `MissionUIManager.IsHudHidden` (сейчас управляется только паузой/дебрифом/crash-панелью).

---

## 6. Правила безопасной правки UI

1. **`CanvasGroup` обязателен** на любом объекте, который наследует `UIScreen` (иначе `Show()/Hide()` пишет `LogError` и ничего не делает). `MainMenuController.OnValidate()` предупреждает об этом.
2. **Z = 0** для панелей в Screen Space Overlay: `SettingsPanel.NormalizeZ()` — вынужденная мера. Новые панели лучше сразу ставить в `localPosition.z = 0`.
3. **Не редактировать `MissionSelectPanel.prefab` / `SettingsPanel.prefab` вручную «насовсем»** — есть Editor-билдеры, которые могут перезаписать структуру.
4. **Ссылки задаются в инспекторе**, а не через `GetComponentInChildren` (кроме фолбэков в `Awake`). При переименовании объектов ломаются: `MissionMenuController.FindBackButton()` (ищет `"BackButton"`), `DesktopIcon`, `CloseMissionBoardButton`.
5. **Кнопки подписываются кодом** (`onClick.AddListener`), а не в инспекторе — при копировании префаба подписки не теряются, но при открытии префаба в редакторе список `OnClick` будет пустым. Это нормально для этого проекта.
6. **`Time.timeScale = 0`** ставит почти каждое окно. Если добавляешь своё окно — не забудь вернуть `timeScale = 1` при закрытии и восстановить `Cursor.lockState`.
7. **`Time.timeScale` и `Cursor` — глобальные**: любое окно может «испортить» состояние другому. Отлаживай через `BunkerPauseController.IsPaused`, `ComputerUIController.IsOpen`, `MissionMenuController.IsOpen`.

---

## 7. Какие UI-ассеты являются внешними

* `Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-png/HUF Pack.png` — основной атлас спрайтов меню/панелей.
* `Assets/UnityTechnologies/ParticlePack/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` — шрифт TMP (единственный, используется и в меню, и в миссиях).
* `Assets/Materials/DefaultUI.mat` — материал UI в меню.
* `Assets/Art/Videos/24e0aa0c-d9f9-461b-bd0e-22f8c02b0ced.mp4` + `MenuVideoRT.renderTexture` — видео-фон главного меню.
* `Assets/Textures/comp.png` — картинка рабочего стола бункера.

> Шрифт TMP один на весь проект, поэтому «разные шрифты в разных меню» сейчас невозможны без добавления новых TMP-ассетов.


