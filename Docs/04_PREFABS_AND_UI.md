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
 ├─ MissionSelectPanel [prefab instance — НЕ открывается из меню, но оформлен в общем стиле]
 └─ VideoBackground (VideoPlayer → MenuVideoRT)
EventSystem
MenuCamera
```

> С 22.11.2026 `SettingsPanel` — модальное окно по центру (`Scrim` + `Plate` 1400×920 + рамка с красными угловыми скобками). Главное меню (`MainPanel`) остаётся эталоном стиля и не меняется.

### 2.3 `Bunker(main)`

```
Canvas (Overlay)
 ├─ InteractionHint  [CanvasGroup + Label/TMP 20 px + Outline]
 │     — подсказка «Сесть [E] / Войти в систему [E]»: по центру, 75 % высоты (y −270), без плашки
 ├─ SittingHint      [CanvasGroup + Label/TMP 18 px + Outline]
 │     — подсказка «Встать [Пробел]»: по центру у нижнего края (y −470), без плашки
 ├─ ShopWindow       [CanvasGroup] — окно «Магазин» (контента нет)
 ├─ ComputerOS       [ComputerUIController, CanvasGroup] ← «рабочий стол» ОС
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
│   ├─ Viewport (840×814, RectMask2D)
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

### 3.2 `MissionCard.prefab`

```
MissionCard [Image (flat 0.10/0.11/0.12), Button, LayoutElement 840×150, MissionCardView]
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

Одновременно живут **четыре независимых слоя модальности**, все ставят `Time.timeScale = 0`:

| Слой | Кто открывает | Кто закрывает | Признак «открыто» |
|---|---|---|---|
| Пауза | `BunkerPauseController.Update` (`Esc`) | `Resume()` / `Esc` | `BunkerPauseController.IsOpen`, `IsPaused` |
| Настройки | кнопка паузы | `CloseButton`, `Esc` | `SettingsPanel.IsOpen` |
| ComputerOS | `ComputerInteractable` (сидя, `E`) | `E` / `Esc` | `ComputerUIController.IsOpen` |
| Меню миссий | `DesktopIcon`, `BunkerStateRestorer` | `BackButton`, `Esc` | `MissionMenuController.IsOpen` |

Чтобы `Esc` не закрывал сразу всё, в коде расставлены «окна блокировки» по 0.2 с (`lastOpenTime`, `lastCloseTime`, `LastClosedTime`). Дополнительно (22.11.2026): пока `ComputerUIController.IsOpen == true`, игрок **не может встать со стула** — `ChairInteractable.IsStandUpBlocked` блокирует `Пробел`; `Esc`/`E` закрывают рабочий стол, и только после этого вставание разблокируется. Порядок проверок в `BunkerPauseController.Update()`:

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
* `Assets/Textures/comp.png` — картинка рабочего стола бункера.

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

### 8.6 Проверка (22.11.2026)

Play Mode, `MainMenu` и `Bunker(main)`:
- вкладки: 13 / 10 / 8 / 12 строк, активная вкладка белая с красной полосой и Bold, неактивные 0.55;
- дропдаун раскрывается (3 опции 320×40 справа), «Сброс» оставляет панель открытой и пересобирает строки, «X»/`Esc` закрывают;
- пауза: кнопки 460×60, persistent-вызовы `PauseManager.Resume/OpenSettings/ExitToMainMenu` сохранены, выход красный;
- миссии: карточки 816×150 (внутри 860-й колонки), детали 860×814, `LaunchButton` 812×64 с отступом 14 px, «Назад» закрывает меню;
- подсказки: компактные белые надписи без плашек (TMP 20 / 18 px, `Outline` 0,0,0,0.85 при 1.5 px), `InteractionHint` на 75 % высоты, `SittingHint` в 52 px от низа; при одновременном показе (сидя + взгляд на терминал) вертикальный зазор 162 px — без наложений;
- рабочий стол: иконка 160×160 не пересекается с шапкой (56 px) и футером (48 px);
- консоль Unity — 0 ошибок; геометрические проверки — без наложений и выходов за границы.


