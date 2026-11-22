# 02 — Обзор сцен

Разобраны только 5 сцен игрового цикла. Для каждой: назначение → ключевые объекты → «мозг» → подключённые скрипты → внешние ассеты → вход/выход → заметки.

---

## 1. `Boot.unity` (~25 КБ)

**Назначение:** техническая сцена-загрузчик. Не содержит игрового контента, живёт один кадр (пока грузится `MainMenu`).

**Ключевые объекты:**

| Объект | Компоненты | Роль |
|---|---|---|
| (объект с `Bootstrapper`) | `Bootstrapper` | Поднимает сервисы и запускает переход в меню |
| `SceneLoader` | `SceneLoader` (singleton, `DontDestroyOnLoad`) | Грузит `MainMenu`, держит экран загрузки |
| Canvas + `CanvasGroup` | `Canvas`, `CanvasScaler`, `GraphicRaycaster`, `CanvasGroup` | Экран загрузки (alpha 0→1) |
| Camera | `Camera` | Пустая сцена |
| EventSystem | `EventSystem`, Input System UI module | UI-события |

**«Мозг» сцены:** `Bootstrapper` (`Assets/Scripts/Core/Bootstrapper.cs`, 22 строки).

**Скрипты:** `Bootstrapper`, `SceneLoader`.
**Ассеты:** шрифт TMP `LiberationSans SDF`, стандартные URP-материалы UI.

**Вход:** индекс 0 в Build Settings (старт игры).
**Выход:** всегда `MainMenu` (`GameStateMachine.Boot → MainMenu`).

**Заметки:**

* `Bootstrapper` имеет сериализованное поле `MissionRegistry missionRegistry` с комментарием «чтобы грузился раньше UI», но поле нигде не используется — сам реестр грузится позже через `Resources.Load` в `MissionMenuController`.
* `EnsureService<T>` использует `FindFirstObjectByType<T>()` — если сервис уже есть в сцене, дубликат не создаётся.

---

## 2. `MainMenu.unity` (~77 КБ)

**Назначение:** главное меню: видео-фон, три кнопки, панель настроек и (фактически неиспользуемая) панель выбора миссии.

**Ключевые объекты:**

| Объект | Компоненты | Роль |
|---|---|---|
| `MainMenuController` | `MainMenuController` | «Мозг» меню: навигация по панелям, запуск бункера, выход |
| `Canvas` (Screen Space Overlay) | `Canvas`, `CanvasScaler`, `GraphicRaycaster` | Корневой UI |
| `MainPanel` | `SimpleUIScreen` + `CanvasGroup` | Главная панель |
| `PlayButton` / `SettingsButton` / `ExitButton` | `Button`, `Image`, `TMP_Text` | Кнопки (`OnOpenMissions`, `OnOpenSettings`, `OnExit`) |
| `MissionSelectPanel` (prefab instance) | `MissionSelectScreen` | Панель миссий; **в меню не открывается** |
| `SettingsPanel` (prefab instance) | `SettingsPanel`, `SettingsListBuilder` | Панель настроек |
| `VideoBackground` | `VideoPlayer` + `RawImage` → `MenuVideoRT` | Видео-фон (`Art/Videos/24e0aa0c-….mp4`) |
| `MenuCamera` | `Camera` | Камера (в сцене только UI) |
| `EventSystem` | `EventSystem` + `InputSystemUIInputModule` | UI-события |

**«Мозг» сцены:** `MainMenuController` (`Assets/Scripts/MainMenu/MainMenuController.cs`).

**Скрипты:** `MainMenuController`, `SimpleUIScreen`, `MissionSelectScreen` (+ `MissionDetailsPanel` внутри префаба), `MissionCardView` (внутри `MissionCard.prefab`), `SettingsPanel`, `SettingsListBuilder`.

**Ассеты:** `Resources/MissionRegistry.asset`, `Prefabs/UI/MissionSelectPanel.prefab`, `Prefabs/UI/SettingsPanel.prefab`, `Materials/DefaultUI.mat`, `Art/Videos/MenuVideoRT.renderTexture`, `HUF Pack.png`.

**Вход:** из `Boot` (`SceneLoader.LoadAsync("MainMenu")`).
**Выход:** `Play` → `Bunker(main)`; `Exit` → `Application.Quit()` (в редакторе — остановка Play).

**Заметки (важно):**

* `MainMenuController.OnOpenMissions()` (`MainMenuController.cs:59`) **не открывает** панель миссий — он сразу грузит бункер. Поле `missionSelect` и инстанс `MissionSelectPanel` в меню фактически **мёртвый UI**: они только скрываются в `ShowMainPanel()`.
* Подробная диагностика: `verboseLogging = true`, `DumpFields()`, `OnValidate()` с проверкой наличия `CanvasGroup`.
* `Start()` принудительно ставит `Time.timeScale = 1` и разблокирует курсор — защита от возврата из паузы/миссии.

---

## 3. `Bunker(main).unity` (~3.3 МБ)

**Назначение:** «хаб» между миссиями. Полноценная 3D-сцена от первого лица: игрок ходит по бункеру, садится за стол оператора, заходит в «ОС» компьютера и запускает миссии с рабочего стола.

Сцена тяжёлая из-за запечённого освещения и интерьера (`Scenes/Bunker(main)/LightingData.asset`, `Models/Buildings/Base_v1/source/Bunker07Anim.fbx`).

**Ключевые объекты:**

| Объект | Компоненты | Роль |
|---|---|---|
| `Player` | `PlayerController`, `PlayerInteractionDetector`, `BunkerStateRestorer`, `CharacterController`, `Animator` | Игрок + восстановление позы после миссии |
| `Main Camera` | `Camera`, `CameraFollowHead` | Камера следует за костью головы (`Swat.fbx`) |
| `Chair_Blocker` | `ChairInteractable` (+ `Collider`) | Сесть/встать, телепорт на `sitPosition` / `standPosition` (вставание — **справа** от стула) |
| `Interact_OperatorTerminal` | `ComputerInteractable` | Вход в компьютер (только сидя) |
| `ComputerOS` | `ComputerUIController`, `Image` (пластина фона) (+ `CanvasGroup`, `RectTransform`) | Анимированное окно «рабочего стола»; фон — `Textures/DesktopBackground/DesktopBackground_plate.png` |
| `DesktopSweep` | `Image` (лепесток радара), `DesktopRadarSweep` | Дочерний слой фона (индекс 0): вращающийся сектор радара, крутится и при `Time.timeScale = 0` |
| `Btn_Missions` | `DesktopIcon` + `Button` | Иконка «Миссии» на рабочем столе (в свободной левой зоне фона) |
| `MissionSelectPanel` (prefab instance) | `MissionSelectScreen`, `MissionMenuController` | Меню выбора миссий внутри ОС |
| `InteractionCanvas` | `InteractionUIController` | Подсказки взаимодействия (компактные надписи на 75 % высоты и у нижнего края, без плашек), окна «магазин» и «карта миссий» |
| `PauseManager` | `BunkerPauseController` | Пауза по `Esc`, настройки, выход в главное меню |
| `SettingsPanel` (prefab instance) | `SettingsPanel` | Настройки в паузе |
| `Canvas`, `EventSystem` | — | UI |

**«Мозг» сцены:** единого нет, сцена управляется связкой:

* `BunkerPauseController` — состояние паузы, блокировка ввода, выход в меню;
* `BunkerStateRestorer` — сценарий возврата из миссии;
* `MissionMenuController` — выбор и запуск миссии;
* `ComputerUIController` — открытие/закрытие ОС и таймскейл.

**Скрипты (14 привязок):** `PlayerController`, `PlayerInteractionDetector`, `CameraFollowHead`, `ChairInteractable`, `ComputerInteractable`, `ComputerUIController`, `DesktopIcon`, `DesktopRadarSweep`, `MissionMenuController`, `MissionSelectScreen`, `InteractionUIController`, `BunkerPauseController`, `BunkerStateRestorer`, `SettingsPanel`.

**Ассеты:** `Bunker(main)/LightingData.asset`, `Models/Player/Swat.fbx`, `Models/Player/PlayerAnimator.controller` (параметры `Speed`, `Direction`, `Strafe`, `IsGrounded`, `IsSitting`, `IsTyping`, состояние `SitIdle`), `Materials/Soldier_body1.mat`, `Soldier_head6.mat`, `Textures/DesktopBackground/DesktopBackground_plate.png` (статичная пластина рабочего стола), `Textures/DesktopBackground/DesktopBackground_sweep.png` (вращаемый лепесток радара).

**Вход:** из `MainMenu` (Play) и из `Debrief` («Вернуться на базу»).
**Выход:** `MissionMenuController.StartMission` → сцена миссии; `BunkerPauseController.ExitToMainMenu` → `MainMenu`.

**Заметки:**

* Здесь **нет** `DroneManager` → `SettingsScope.IsMission == false` → применяются «бункерные» настройки графики.
* `MissionMenuController.Awake()` содержит костыль: если FSM в состоянии `Boot`, он переводит в `MainMenu` (иначе `OpenMissionMenu` не сможет перейти в `MissionSelect`).
* `MissionMenuController.FindBackButton()` ищет кнопку по имени `BackButton` среди всех `Button` в детях, иначе берёт первую попавшуюся — хрупкая привязка.
* `BunkerPauseController` держит **10** сериализованных ссылок и вручную выключает `PlayerInteractionDetector` и `ChairInteractable` (`BlockInput()` / `ReleaseInput()`).
* `BunkerStateRestorer` — единственное место, где «сцена» знает о возврате из миссии; `SceneLoader` её явно ищет и **ждёт** до 3 секунд (`restoreWaitTimeout`).

---

## 4. `PreparingDemo.unity` (~1.78 МБ)

**Назначение:** обучающая миссия из трёх глав (мотор/взлёт/оптика → точность в доме → бронированная техника «мангал»).

**Ключевые объекты:**

| Объект | Компоненты | Роль |
|---|---|---|
| `GameSystems` | `DroneManager`, `TrainingManager` | Пул дронов + «мозг» обучения |
| `MissionManager` | `MissionController` (`autoComplete: 0`), `DebriefController` | Формальное завершение миссии и дебриф |
| `UIManager` | `MissionUIManager` | Пауза (`Esc`), Crash-панель, скрытие HUD |
| `ScreenFader` | `ScreenFader` | Затемнение между главами (титр + 2 с удержания) |
| `InstructionText` | `TMP_Text` | Тексты инструктора из `TrainingManager` |
| `CrashPanel`, `PausePanel`, `DebriefPanel` | UI | Панели состояний |
| `DebriefCamera`, `BackgroundCamera` | `Camera` | Камера дебрифа и фоновая |
| 3 × `TrainingGate` | `TrainingGate` | Кольца полосы препятствий, событие `OnGatePassed` |
| Учебные цели | `TargetObject` + `VehicleHealth` + `VehicleModule` ×N | Дом со складом БК, техника с модулями |
| `Global Volume` | `Volume` (`Scenes/PreparingDemo/Global Volume Profile.asset`) | Постобработка сцены |
| Дроны | `GimbalDrone Variant.prefab`, `StrikeDrone 1.prefab` | Разведчик и ударный FPV |

**«Мозг» сцены:** `TrainingManager` (`Assets/Scripts/Managers/TrainingManager.cs`) — конечный автомат шагов и глав. `MissionController` здесь **не** завершает миссию сам (`autoComplete: 0` в сцене), его вызывает `TrainingManager` в конце.

**Скрипты:** `TrainingManager`, `MissionController`, `DebriefController`, `MissionUIManager`, `ScreenFader`, `DroneManager`, `TrainingGate`, `TargetObject`, `VehicleHealth`, `VehicleModule` + скрипты из префабов дронов (`DroneFlightController`, `DroneCollision`, `DroneMotors`, `DroneCameraModule`, `FPVZoom`, `ReconHUD`, `StrikeHUD`, `TargetDetector`, `TargetLockManager`, `EWReceiver`, `NightVisionController`, `FPVCameraShake`).

**Ассеты:** `House.prefab`, `gateway_arch.glb`, `t-72b3_main_battle_tank.glb`, `generator.glb`, `Warehouse.fbx`, `potujnojet.fbx`, `WildFire.prefab`, `VFX_Fire_01_Medium_Smoke.prefab`, `Sky_Forest.mat`, `DroneConfigs/MavicReconConfig.asset`, `DroneConfigs/SmallFpvHeatConfig.asset`.

**Вход:** `Mission1.asset` (`id: training_mission`, `sceneName: PreparingDemo`).
**Выход:** `Debrief` → «Вернуться на базу» → `Bunker(main)`; либо пауза → «В главное меню».

**Заметки:**

* Главы переключаются вручную: `envChapter1/2/3` — корневые объекты окружения, `spawnPosChapter2/3` — точки респавна ударного дрона (`DroneManager.RespawnStrikeDroneAt`).
* При переходе между главами `TrainingManager` **удаляет все `ParticleSystem` вне Canvas** и останавливает незацикленные `AudioSource` (`TrainingManager.cs:185-195`) — грубая очистка VFX.
* Завершение обучения: `totalModulesToDestroy = 3`, затем `MissionController.CompleteMission(Success, 3)`.
* Модули техники (`VehicleModule`) и расчёт пробития брони — механика третьей главы.

---

## 5. `FirstMission(Forest).unity` (~1.5 МБ)

**Назначение:** боевая миссия-засада в лесу: сперва титры-наблюдение, затем бой. Уничтожить БТР прикрытия → не дать РСЗО («Himers») уйти из района.

Сцена содержит 6 тайлов террейна, запечённое освещение и стороннюю систему `Polyzero/ElectricCables` (много объектов `AutoCable_*`, `ElectricPoleChainCableGenerator`) — она не связана с геймплеем, только визуал.

**Ключевые объекты:**

| Объект | Компоненты | Роль |
|---|---|---|
| `DroneManager` | `DroneManager` | Пул дронов (MavicRecon + SmallFpvHeat) |
| `MissionManager` | `MissionController` (`autoComplete: 1`), `DebriefController` | Завершение миссии + дебриф |
| `AmbushMissionDirector` | `AmbushMissionDirector`, `TargetPingSystem` | **«Мозг» сценария** (титры, фазы, победа/поражение) |
| `UIManager` | `MissionUIManager` | Пауза, crash-панель, HUD |
| `Himers` (объект РСЗО) | `VehicleHealth`, `VehiclePatrol`, `HimarsStrike`, `EWEmitter` (`EWEmiter.cs`), `TargetObject`, `VehicleDust`, `WheelRotator`, `VehicleModule` ×4 | Цель №2 + источник РЭБ-помех |
| БТР (`VehiclePatrol` + `VehicleSmartAI`) | `VehicleHealth`, `VehiclePatrol`, `VehicleSmartAI`, `VehicleDust`, `WheelRotator`, `TargetObject`, `VehicleModule` ×N | Цель №1: стреляет по дронам, ставит дымовую завесу |
| `DroneFlightController` (в сцене) | `DroneFlightController` | Разведывательный дрон, с которого игрок начинает |
| `Terrain` ×6 | `Terrain`, `TerrainDistanceBoost` | Ландшафт (detail distance = 800) |
| `Global_Volume` | `Volume` (`Scenes/FirstMission(Forest)/Global_Volume Profile.asset`) | Постобработка |
| `Canvas` / `DebriefPanel` / `PausePanel` | UI | Интерфейс |

**«Мозг» сцены:** `AmbushMissionDirector` (`Assets/Scripts/Managers/AmbushMissionDirector.cs`).
В `Start()` он **отключает автозавершение** у `MissionController` (`autoComplete = false`) и ведёт миссию сам, а в конце передаёт результат через `MissionController.CompleteMission(...)`.

**Скрипты:** `AmbushMissionDirector`, `MissionController`, `DebriefController`, `MissionUIManager`, `DroneManager`, `VehicleHealth`, `VehicleModule`, `VehiclePatrol`, `VehicleSmartAI`, `VehicleDust`, `WheelRotator`, `HimarsStrike`, `TargetPingSystem`, `TargetObject`, `EWEmitter`, `TerrainDistanceBoost`, `DroneFlightController` + скрипты из префабов дронов и техники.

**Ассеты:** `Scenes/FirstMission(Forest)/Global_Volume Profile.asset`, `New Terrain*.asset` ×6, `CloudySky.mat`, `asphalt_mat.mat`, `btrWithCopeCage.prefab`, `HIMRAS.FBX`, `Eletric_pole.prefab`, `BigExplosion.prefab`, `WildFire.prefab`, `SmokeEffect.prefab`, `DroneConfigs/MavicReconConfig.asset`, `SmallFpvHeatConfig.asset`, `NewLightningSettingsAsset.lighting`.

**Вход:** `Mission2.asset` (`id: mission_1`, `sceneName: FirstMission(Forest)`).
**Выход:** `Debrief` → `Bunker(main)`; либо пауза → «В главное меню».

**Сценарий `AmbushMissionDirector` (по коду):**

1. `reconDrone.isInputLocked = true`, включаются патрули (`VehiclePatrol.enabled = true`), `VehicleSmartAI` и `HimarsStrike` выключаются.
2. Два титра по 4 с («Обнаружена техника…», «РСЗО готовится к залпу…»).
3. Ввод разблокируется, включаются `HimarsStrike` и `btrAI`, титр-задача на 5 с.
4. Ожидание `btrHealth.currentHealth <= 0` → титр + принудительный `himarsPatrol.StartRetreat(40)`.
5. Погоня: успех — `himarsHealth.currentHealth <= 0`; провал — РСЗО подошла к `himarsEscapePoint` ближе 15 м.
6. Через 3 с — `ShowDebrief()` → `MissionController.CompleteMission(...)`, где `targetsDestroyed` = 2 (успех) или 1 (провал).

**Заметки:**

* `MissionController.LateUpdate()` в этой сцене всё ещё может завершить миссию провалом, если у игрока не осталось живых дронов (директор это не отключает).
* `HimarsStrike` запускает залп только при `himarsPatrol.IsFinished == true`, но директор вызывает `StartRetreat` — сценарий частично дублируется в двух местах.
* В сцене стоит `autoComplete: 1`, а директор гасит его в `Start()`. Корректность держится только на порядке вызовов (`Update` после всех `Start`).
* Объект РСЗО в сцене называется `Himers` (опечатка в имени, влияет только на читаемость иерархии).

---

## 6. Сводка: кто чем владеет

| Сцена | «Мозг» | Завершение миссии | Пауза | Дебриф |
|---|---|---|---|---|
| `Boot` | `Bootstrapper` | — | — | — |
| `MainMenu` | `MainMenuController` | — | — | — |
| `Bunker(main)` | связка `BunkerPauseController` / `MissionMenuController` / `ComputerUIController` | — | `BunkerPauseController` | — |
| `PreparingDemo` | `TrainingManager` | `TrainingManager → MissionController.CompleteMission` | `MissionUIManager` | `DebriefController` |
| `FirstMission(Forest)` | `AmbushMissionDirector` | `AmbushMissionDirector → MissionController.CompleteMission` | `MissionUIManager` | `DebriefController` |



