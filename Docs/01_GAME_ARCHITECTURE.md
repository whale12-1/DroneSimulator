# 01 — Архитектура игры

## 1. Общая картина

Проект — **сингловая одиночная игра** с линейным циклом «меню → база → миссия → дебриф → база».
Архитектура гибридная:

* **Сценовые MonoBehaviour** — вся игровая логика конкретной сцены (бункер, обучение, миссия).
* **Глобальные сервисы** (`DontDestroyOnLoad` или статические классы) — загрузка сцен, настройки, прогресс, шина событий, FSM.
* **ScriptableObject-конфиги** — описания миссий, дронов, реестр миссий.

Нет DI-контейнера, нет Addressables, нет сохранения состояния сцены — переходы всегда идут через `SceneLoader.LoadAsync(name)` (одиночная загрузка, `LoadSceneMode.Single`).

---

## 2. Поток сцен

```
                 ┌───────────────────────────────────────────────┐
                 │ Boot.unity                                    │
                 │  Bootstrapper.Start():                        │
                 │   EnsureService<SceneLoader>                  │
                 │   EnsureService<SettingsService>              │
                 │   FSM → MainMenu                              │
                 │   SceneLoader.LoadAsync("MainMenu")           │
                 └───────────────────┬───────────────────────────┘
                                     ▼
                 ┌───────────────────────────────────────────────┐
                 │ MainMenu.unity                                │
                 │  MainMenuController: Play / Settings / Exit    │
                 │  Play → FSM Loading + Load("Bunker(main)")    │
                 └───────────────────┬───────────────────────────┘
                                     ▼
                 ┌───────────────────────────────────────────────┐
                 │ Bunker(main).unity  (хаб)                     │
                 │  PlayerController + ChairInteractable         │
                 │  Компьютер (ComputerOS) → «рабочий стол» ОС   │
                 │  DesktopIcon → MissionMenuController →        │
                 │  MissionSelectScreen (карточки миссий)        │
                 │  «Запустить» → MissionContext.Begin(...) +    │
                 │  SceneLoader.LoadAsync(mission.sceneName)     │
                 └───────┬───────────────────────────────┬───────┘
                         ▼                               ▼
        ┌────────────────────────────────┐   ┌────────────────────────────────┐
        │ PreparingDemo.unity (обучение) │   │ FirstMission(Forest).unity     │
        │ TrainingManager — «мозг»       │   │ AmbushMissionDirector — «мозг» │
        │ MissionController(autocomplete │   │ MissionController(autocomplete │
        │   = 0 в сцене)                 │   │   гасит директор в Start)      │
        └───────────────┬────────────────┘   └───────────────┬────────────────┘
                        │                                    │
                        │ MissionController.EndMission()      │
                        ▼                                    ▼
              FSM → GameState.Debrief (внутри той же сцены)
                        │  DebriefController показывает панель
                        │  «Вернуться на базу» →
                        │  MissionContext.ReturnedFromMission = true
                        ▼
              SceneLoader.LoadAsync("Bunker(main)")
                        │  BunkerStateRestorer: сажает игрока за стол,
                        │  открывает ComputerOS и меню миссий заново
                        ▼
                    Bunker(main)
```

### Сцены в Build Settings (`ProjectSettings/EditorBuildSettings.asset`)

| # | Путь | В области аудита |
|---|---|---|
| 0 | `Assets/Scenes/Boot.unity` | ✅ |
| 1 | `Assets/Scenes/MainMenu.unity` | ✅ |
| 2 | `Assets/Scenes/SampleScene.unity` | ❌ вне области |
| 3 | `Assets/Scenes/TerrainDemoScene.unity` | ❌ вне области |
| 4 | `Assets/Scenes/Bunker(main).unity` | ✅ |
| 5 | `Assets/Scenes/PreparingDemo.unity` | ✅ |
| 6 | `Assets/Scenes/FirstMission(Forest).unity` | ✅ |

Файлы сцен, **отсутствующие** в Build Settings: `player.unity`, `FunctionalUI-Demo.unity`, `FirstMission(SpizjenyiVaarik).unity` — они не загружаемы через `SceneLoader` (там есть проверка `IsSceneInBuildSettings`, при провале загрузка отменяется с ошибкой в лог).

---

## 3. Машина состояний (`GameStateMachine`)

Статический класс, состояние живёт всё время работы приложения (`Assets/Scripts/Core/GameStateMachine.cs`).

```csharp
enum GameState { Boot, MainMenu, MissionSelect, Loading, Gameplay, Paused, Debrief }
```

Разрешённые переходы (матрица `allowed`):

| Из | Куда можно |
|---|---|
| `Boot` | `MainMenu` |
| `MainMenu` | `MissionSelect`, `Loading`, `Boot` |
| `MissionSelect` | `MainMenu`, `Loading`, `Gameplay` |
| `Loading` | `Gameplay`, `MainMenu` |
| `Gameplay` | `Paused`, `Debrief`, `MainMenu`, `MissionSelect` |
| `Paused` | `Gameplay`, `MainMenu` |
| `Debrief` | `MainMenu`, `Loading` |

Недопустимый переход не бросает исключение — пишет `Debug.LogWarning("[FSM] Недопустимый переход ...")` и **молча игнорируется**. Поэтому любой код, который «дёргает» FSM, обязан проверять `CanTransitionTo(...)`, иначе переход просто не произойдёт.

Событие: `GameStateMachine.StateChanged(prev, next)`. Подписчики: `MainMenuController`, `MissionUIManager`, `DebriefController`, `BunkerPauseController`.

**Практическое следствие:** `Loading` — это не сцена, а состояние. Его выставляет вызывающая сторона (`MainMenuController`, `MissionMenuController`, `DebriefController`, `MissionUIManager`), а **завершает** тот, кто первым инициализируется в загруженной сцене:

* `MissionController.Awake()` — если `Current == Loading`, переводит в `Gameplay`;
* `BunkerStateRestorer.Start()` — если `Current == Loading`, переводит в `Gameplay`.

---

## 4. Глобальные сервисы

| Сервис | Тип | Время жизни | Ответственность |
|---|---|---|---|
| `SceneLoader` | MonoBehaviour (singleton) | `DontDestroyOnLoad`, создаётся в `Boot` | Асинхронная загрузка сцен, экран загрузки (`CanvasGroup`), ожидание `BunkerStateRestorer` |
| `SettingsService` | MonoBehaviour (singleton) | `DontDestroyOnLoad`, создаётся в `Boot` | Хранение/сохранение/миграция `settings.json`, применение настроек при загрузке сцен |
| `GameSession` | MonoBehaviour (singleton) | `DontDestroyOnLoad` | **Не используется** (мёртвый сервис), хранит `SelectedDroneConfig` |
| `PostFxRouter` | MonoBehaviour (singleton) | `DontDestroyOnLoad`, `[DefaultExecutionOrder(-100)]` | Создаёт runtime-`Volume`-профили для РЭБ / ПНВ / Motion Blur и раздаёт эффекты |
| `FpsCounterHud` | MonoBehaviour | Создаётся автоматически `[RuntimeInitializeOnLoadMethod]` | Счётчик FPS, если включён в настройках |
| `EffectService` | static | — | Пул из 16 `AudioSource` для одноразовых звуков + `Instantiate` эффектов |
| `ExplosionService` | static | — | Единый расчёт урона по площади (`OverlapSphere` + затухание) |
| `ProgressService` | static | — | `progress.json`: записи по миссиям, очки, разблокировки |
| `GameEvents` | static event bus | — | Взрывы, урон, уничтожение техники/модулей, переключение дрона |
| `MissionContext` | static | — | Текущая миссия, выбранный дрон, результат последней сессии, флаг возврата из миссии |
| `EWService` | static | — | Реестр активных РЭБ-передатчиков + `BoundaryJamIntensity` |
| `SettingsScope` | static | — | Флаг «мы в миссии» (`FindAnyObjectByType<DroneManager>() != null`) |
| `AudioBus` | static | — | Загрузка `Resources/Audio/MainMixer`, доступ к группам и громкостям |
| `SettingsApplier` / `GraphicsApplier` / `CameraApplier` / `AudioApplier` / `HudStyleService` | static | — | Применение настроек и стиля HUD |

### Порядок создания в `Bootstrapper`

```csharp
EnsureService<SceneLoader>("SceneLoader");
EnsureService<SettingsService>("SettingsService");
GameStateMachine.TransitionTo(GameState.MainMenu);
SceneLoader.Instance.LoadAsync("MainMenu");
```

`SettingsService.Awake()` сам чинит окружение: `AudioBus.Reset()`, `SettingsApplier.ResetScreenCache()`, `SettingsScope.Refresh()`, `ApplyAll()` и подписка на `SceneManager.sceneLoaded` — при каждой загрузке сцены настройки применяются заново. Это ключевой механизм, потому что камеры и URP-ассет пересоздаются вместе со сценой.

---

## 5. Управление миссией и данными

### 5.1 Данные

* `MissionDefinition` (ScriptableObject, `CreateAssetMenu "FPV/Mission"`): `id`, `displayName`, `description`, `tacticalHint`, `preview`, **`sceneName`**, `objectiveType`, `targetCount`, `timeLimit`, `defaultDrone`, `availableDrones[]`, `prerequisites[]`, `rewardPoints`.
* `MissionRegistry` (ScriptableObject, `CreateAssetMenu "FPV/Mission Registry"`): `allMissions[]`; лежит в `Assets/Resources/MissionRegistry.asset`, поэтому грузится через `Resources.Load<MissionRegistry>("MissionRegistry")` (так делает `MissionMenuController`).
* `DroneConfig` (ScriptableObject, `CreateAssetMenu "FPV/Drone Config"`): 12 групп полей — от боевой части до HUD/РЭБ/подвеса/зума. Фактически это «единый профиль дрона», на него ссылаются почти все системы дрона.

Содержимое реестра на момент аудита (в реестре 2 миссии из 3 существующих ассетов):

| Asset | id | displayName | sceneName | objective | targetCount |
|---|---|---|---|---|---|
| `Resources/Missions/Mission1.asset` | `training_mission` | «Тренировка» | `PreparingDemo` | `DestroyAllTargets` (0) | 1 |
| `Resources/Missions/Mission2.asset` | `mission_1` | «БТР и РСЗО» | `FirstMission(Forest)` | `DestroyAllTargets` (0) | 2 |
| `Resources/Missions/Mission3.asset` | `mission_3` | «Патруль» | *(пусто)* | `DestroyAllTargets` | 3 |

`Mission3.asset` **не подключён** к `MissionRegistry.allMissions` и не имеет `sceneName`/`defaultDrone` → недостижимая миссия.

### 5.2 Жизненный цикл миссии

1. `MissionMenuController.StartMission(mission)`:
   * `MissionContext.Begin(mission, mission.defaultDrone)`;
   * FSM → `Loading`;
   * `Time.timeScale = 1`, курсор захвачен;
   * `SceneLoader.LoadAsync(mission.sceneName)`.
2. В загруженной сцене:
   * `DroneManager.Awake()` строит `activeSlots` из `mission.availableDrones` (сопоставление по ссылке на `DroneConfig`), всех дронов сцены предварительно выключает;
   * `MissionController.Awake()` — FSM `Loading → Gameplay`, читает `MissionContext.Current`, стартует таймер;
   * `MissionController.Start()` — `droneManager.SelectByConfig(mission.defaultDrone)`.
3. Завершение — всегда через `MissionController.EndMission(result, targetsDestroyed)`:
   * `MissionContext.MarkResult(...)`;
   * `ProgressService.RecordResult(...)` (пишет `progress.json`);
   * FSM → `Debrief`.
   Альтернативный вход — `MissionController.CompleteMission(result, targets)` (используется `TrainingManager` и `AmbushMissionDirector`).
4. `DebriefController` ловит `StateChanged → Debrief`, включает панель и `debriefCamera`, считает очки через `ProgressService.ComputeScore`.
5. «Вернуться на базу» → `MissionContext.ReturnedFromMission = true` → `LoadAsync("Bunker(main)")`.
6. В бункере `BunkerStateRestorer` видит флаг и восстанавливает позу: сажает игрока (`ChairInteractable.Interact(go, force: true)`) → `ComputerUIController.OpenDesktop()` → ждёт окончания анимации (до 2 с) → `MissionMenuController.OpenMissionMenu()`.

### 5.3 Подсчёт очков (`ProgressService`)

```
score = rewardPoints + targetsDestroyed * 25 + round(clamp01(1 - timeElapsed / timeLimit) * 50)
```

Бонус за время начисляется только если `timeLimit > 0`. Результат `Failed` даёт 0 очков, но увеличивает `attempts` и пишет `lastTime/lastScore`.

---

## 6. Сохранения

Оба файла — в `Application.persistentDataPath`.

| Файл | Кто пишет | Формат | Особенности |
|---|---|---|---|
| `settings.json` | `SettingsService` | `JsonUtility`, pretty-print | Схема версионируется (`SchemaVersion = 3`), есть миграция и запасной путь при ошибке чтения. Запись атомарная: `.tmp` → `File.Replace` (с бэкапом `.bak`). Сохранение дебаунсится на 0.5 с (`MarkDirty()` / `SaveNow()` / `OnApplicationQuit`). |
| `progress.json` | `ProgressService` | `JsonUtility`, pretty-print | `SaveData { completedMissionIds, unlockedMissionIds, totalScore, records[] }`. `MissionRecord` хранит `completed, bestTime, lastTime, bestScore, lastScore, completions, attempts, bestTargets`. Запись — прямой `File.WriteAllText` (без атомарности). |

Разблокировка миссий: `ProgressService.IsUnlocked(m)` — либо у миссии нет `prerequisites`, либо все пререквизиты в `completedMissionIds`, либо `id` находится в `unlockedMissionIds`.

---

## 7. Рендер, постобработка, ПНВ

* URP. `GraphicsApplier.Apply()` **клонирует** текущий `UniversalRenderPipelineAsset` в runtime-инстанс (`"... (Runtime)"`), назначает его `QualitySettings.renderPipeline` и правит `renderScale`, `shadowDistance`, каскадность, MSAA, soft shadows, разрешение shadowmap. При выходе из приложения возвращает исходный ассет.
* `PostFxRouter` создаёт три глобальных `Volume` с приоритетами:
  * `PostFX_EW` (priority 10) — `ChromaticAberration`, `Vignette`, `FilmGrain` для РЭБ;
  * `PostFX_NV` (priority 20) — `ColorAdjustments` для ПНВ;
  * `PostFX_Settings` (priority 30) — `MotionBlur` из настроек.
* `EWReceiver` берёт эффекты у `PostFxRouter` и рулит их `intensity` по уровню помех; «снег» и горизонтальные разрывы строки рисуются в `OnGUI` процедурной текстурой шума.
* Ночное видение реализовано **не** через Volume, а через `ScriptableRendererFeature` в URP-рендерере с именем **`Night Vision Feature`** — `NightVisionController` находит фичу рефлексией через `UniversalRenderPipelineAsset.m_RendererDataList` и включает/выключает её.
* Скоуп настроек по сцене: `SettingsScope.IsMission` (наличие `DroneManager`) переключает `shadowDistance` между «бункерным» и «дроновым» значением.

---

## 8. Ввод

`activeInputHandler: 2` (Both), поэтому в проекте **сосуществуют два стиля**:

* **Новый Input System** — весь UI и бункер: `Keyboard.current`, `Mouse.current`, `escapeKey`, `eKey`, `spaceKey` (`MainMenuController`, `PlayerController`, `PlayerInteractionDetector`, `ChairInteractable`, `ComputerUIController`, `MissionMenuController`, `MissionUIManager`, `BunkerPauseController`).
* **Старый Input** — дроны и обучающая логика: `Input.GetKey/GetKeyDown(KeyCode.*)`, `Input.GetAxis("Mouse X"/"Mouse Y")` (`DroneFlightController`, `DroneCollision`, `DroneCameraModule`, `FPVZoom`, `TargetLockManager`, `NightVisionController`, `TrainingManager`, `DroneManager`).

Назначение клавиш (по коду):

| Действие | Клавиши |
|---|---|
| Ходьба FPS | `WASD` |
| Сесть / встать | `E` / `Space` |
| Пауза (бункер и миссия) | `Esc` |
| Запуск мотора дрона | `W` (однократно) |
| Газ | `W` / `S`, фиксация газа — настройка `throttleMode` |
| Крен / тангаж | стрелки, либо мышь (если `useMouseControl`) |
| Рыскание | `A` / `D` |
| Зависание / Angle-режим | `H` / `M` (только `ReconDrop`) |
| Гимбал | `R`/`F` (pitch), `Q`/`E` (yaw), `C` — сброс |
| Зум | `Z` / `X` (уровни из `DroneConfig.zoomLevels`) |
| Захват цели / ручная детонация | `Space` (по конфигу) |
| ПНВ | `N` |
| Смена дрона | `1..9` |

`SettingsService.Data.controls` подмешивается в рантайме: `PlayerController` (чувствительность/инверсия), `DroneFlightController` (скорость стика, центрирования, газа, режим газа), `DroneCameraModule` (скорость гимбала), `FPVZoom` (базовый FOV).

---

## 9. Слои и реальные связи

```
UI (MainMenu*, SimpleUIScreen, SettingsPanel, MissionSelectScreen, MissionCardView,
    BunkerPauseController, ComputerUIController, InteractionUIController, MissionMenuController)
        │            ▲
        │            └── читает ProgressService, SettingsService
        ▼
Оркестрация миссии (MissionController, TrainingManager, AmbushMissionDirector, DebriefController)
        │
        ├── MissionContext / ProgressService / GameStateMachine / SceneLoader
        ▼
Геймплейные системы (DroneManager, DroneFlightController, DroneCollision, DroneMotors,
                     VehicleHealth, VehicleModule, VehiclePatrol, VehicleSmartAI, HimarsStrike)
        │
        ├── GameEvents (шина) ──► MissionController, DroneManager, TrainingManager, CameraApplier, TargetPingSystem
        ├── DroneConfig (SO-профили)
        └── ExplosionService / EffectService / EWService / AudioBus
```

**Наблюдения:**

* Шина `GameEvents` действительно снимает прямые ссылки между боевыми системами и «директорами» миссий — это сильная сторона архитектуры.
* Есть «протечки слоёв»: `SceneLoader` (ядро) знает про `BunkerStateRestorer` (сцена бункера); `MissionController` проверяет подстроку `"PreparingDemo"`; `DroneManager` ищет дрон по подстроке `"FPV"` в имени конфига.
* UI бункера и меню миссий используют **один и тот же** `MissionSelectScreen` + `MissionSelectPanel.prefab` — переиспользование сделано корректно.
* Статическое состояние (`GameStateMachine`, `MissionContext`, `EWService`, `ProgressService`, `SettingsService.Instance`) при запуске Play без Domain Reload может протекать между сессиями. `GameEvents.ClearAll()` существует, но **нигде не вызывается**.



