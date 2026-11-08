
=== ФАЙЛ 2: ARCHITECTURE.md ===

```markdown
# Архитектура и карта проекта DroneSimulator

Этот файл — часть документации проекта DroneSimulator.  
Остальные файлы: CONTEXT.md, ARCHITECTURE.md, INDEX.md, SETTINGS_DESIGN.md.

## 1. Общая структура папок

```text
Assets/
├── Art/ Видео и визуальные материалы
├── Input/ Input Actions нового Input System
├── Materials/ Основные материалы проекта
├── Models/ Модели персонажа, дронов, техники и базы
├── Prefabs/ Игровые префабы и префабы техники/UI
├── Resources/ MissionRegistry и MissionDefinition
├── Scenes/ Основные сцены проекта
├── Scripts/ Собственные игровые C#-скрипты
├── Settings/ DroneConfig и Volume Profiles
├── Shaders/ Шейдеры проекта
├── Textures/ Основные текстуры
├── _Recovery/ Резервные копии сцен
│
├── ALP_Assets/ Сторонний набор домов
├── S-Mine 35/ Сторонний ассет мины
├── Strategic Warfare UI./ Сторонний UI Starter Pack
├── TerrainSampleAssets/ Terrain demo assets
├── TerrainDemoScene_URP/ Terrain demo project
├── UnityTechnologies/ Unity Particle Pack
├── Volklight/ Сторонний BTR
├── ZIL130_MilitaryTruck/ Сторонний грузовик
├── HDRPDefaultResources/ HDRP-ресурсы
├── URPDefaultResources/ URP-ресурсы
└── TutorialInfo/ Стандартная tutorial-информация Unity
Собственные каталоги
Основные рабочие каталоги:

Assets/Scripts
Assets/Scenes
Assets/Prefabs
Assets/Models
Assets/Settings
Assets/Resources
Assets/Input
Assets/Materials
Assets/Textures
Assets/Art
Сторонние, demo и recovery-каталоги
Сторонними или демонстрационными считаются:

Assets/ALP_Assets
Assets/S-Mine 35
Assets/Strategic Warfare UI Starter Pack
Assets/TerrainSampleAssets
Assets/TerrainDemoScene_URP
Assets/UnityTechnologies
Assets/Volklight
Assets/ZIL130_MilitaryTruck
Assets/TutorialInfo
Assets/_Recovery
2. Архитектура систем
Проект состоит из двух пока слабо связанных подсистем.

2.1 FPS Base
player.unity
└── Player
    ├── CharacterController
    ├── PlayerController
    ├── Visual/SWAT
    │ └── Animator → PlayerAnimator
    └── CameraPivot/Main Camera
Связанные объекты базы:

military base;
Directional Light;
невидимые границы Bounds_*.

Bunker(main) — новая FPS-локация (физика настроена)
Bunker(main)
├── Directional Light
├── Bunker07Anim          (префаб-инстанс FBX, Scale 10; в ассете коллайдеров нет — физика добавлена вручную)
├── Collision_SHELL       (физическая оболочка комнаты, все BoxCollider Is Trigger = off)
│   ├── COL_Floor         (пол)
│   ├── COL_Walls
│   │   ├── COL_Wall_North
│   │   ├── COL_Wall_South
│   │   ├── COL_Wall_East
│   │   └── COL_Wall_West
│   └── COL_Ceiling       (потолок)
├── Blockers_Doors        (коллайдеры дверей: Door_Blocker, Door1_Blocker)
├── Blockers_Safe         (коллайдеры сейфов: Safe_Blocker, Safe1_Blocker, Safe2_Blocker)
├── Blockers_Console      (пульт ControlPanel_Blocker + ComputerNCamGEo_Blocker)
├── Blockers_ACUnits      (ACUnit_Blocker, ACUnit1_Blocker)
├── Blockers_Furniture    (Chair_Blocker, Chair1/2_Blocker, CupBoard_Blocker ×5)
├── Interact_OperatorTerminal (layer Interactable, BoxCollider Is Trigger = off, ComputerInteractable → ОС ComputerOS: открытие по E, закрытие по E/Esc глобально)
├── InteractionCanvas     (Canvas + InteractionUIController + Hint/ShopWindow/ComputerOS + MissionSelectPanel (префаб из MainMenu, выбор миссий))
├── EventSystem           (перенесён из player.unity: EventSystem + InputSystemUIInputModule)
└── Player
    ├── CharacterController
    ├── PlayerController
    ├── PlayerInteractionDetector   (uiController → InteractionCanvas/InteractionUIController)
    ├── BunkerStateRestorer         (восстановление состояния после возврата с миссии)
    ├── Visual/SWAT (Animator → PlayerAnimator)
    └── CameraPivot/Main Camera     (единственный AudioListener в сцене)

В player.unity не обнаружены полноценные:

MissionController;
DroneManager;
tent interaction;
van interaction;
mission UI;
cutscene flow.
2.2 Drone Combat
Дроновая подсистема включает:

дроновые префабы;
DroneConfig;
физику полёта;
оружие;
взрывы;
камеры;
HUD;
захват целей;
РЭБ;
AI техники;
миссионный контроллер.
Она используется преимущественно в отдельных прототипных сценах и префабах.

2.3 Настройки (Settings pipeline)
Единый конвейер: UI → SettingsData → SettingsService → апплеры → игровые системы.

SettingsPanel (4 вкладки) собирает строки SettingDescriptor (Slider / Toggle / Dropdown).
Любое изменение: поле SettingsData обновлено → SettingsService.ApplyInstant() (звук) →
(если настройка не «тяжёлая» либо слайдер отпущен) SettingsService.ApplyHeavy() → MarkDirty()
(запись settings.json через 0.5 с).

ApplyHeavy по порядку:
  1) SettingsApplier.ApplyScreen      — windowMode / resolutionIndex → Screen (с кэшем «уже применено»)
  2) SettingsApplier.ApplyQualityState — QualitySettings: уровень → VSync ПОСЛЕ уровня → лимит FPS → LOD → mipmap
  3) GraphicsApplier.Apply            — рантайм-клон URP-ассета: renderScale, shadowDistance, каскады,
                                        MSAA, мягкие тени, shadowmap resolution
  4) PostFxRouter.ApplyMotionBlur     — Motion Blur через глобальный «Настройки»-Volume
  5) CameraApplier.Refresh            — камеры бункера и дронов (far clip, тени, AA, постобработка)

SettingsService.Awake(): AudioBus.Reset(), SettingsApplier.ResetScreenCache(), SettingsScope.Refresh(),
ApplyAll(). Кэши сбрасываются, потому что статика переживает сессию при отключённом domain reload.
SettingsService.OnSceneLoaded(): SettingsScope.Refresh() → ApplyAll().

Кто читает настройки напрямую (pull-модель, без апплеров):
  PlayerController        — fpsMouseSensitivity, fpsInvertY
  DroneFlightController   — droneStickSpeed, droneCenteringSpeed, throttleSpeed, throttleMode, droneInvertY
  DroneCameraModule       — gimbalSpeed, gimbalInvertY
  FPVZoom                 — droneFov (масштабирует лестницу зума)
  EWReceiver              — ewVisualDistortion, ewPostFxQuality, ewNoiseResolution, reduceDangerousEffects
  NightVisionController   — nightVisionColor, nightVisionGain
  FPVCameraShake          — cameraShakeIntensity
  HudStyleService / ReconHUD / StrikeHUD / FpsCounterHud — HUD-стиль и счётчик FPS
  AmbushMissionDirector   — subtitlesEnabled, subtitleFontSize
  InteractionUIController — showInteractionHints

3. Глобальные сервисы и данные
GameEvents
Файл:
Assets/Scripts/Core/GameEvents.cs

События:

ExplosionOccurred;
DroneExploded;
DamageApplied;
VehicleDestroyed;
ModuleDestroyed;
DroneSwitched.
Используется для связи боевых объектов без прямых ссылок.

GameStateMachine

Файл:

Состояния:

Boot;
MainMenu;
MissionSelect;
Loading;
Gameplay;
Paused;
Debrief.
Переходы проверяются словарём разрешённых переходов.

MissionContext

Файл:

Assets/Scripts/Data/MissionContext.cs

Статический реестр источников помех EWEmitter. Возвращает максимальный уровень РЭБ в заданной точке.

Содержит также флаг ReturnedFromMission: DebriefController.ReturnToMenu() ставит его в true перед
загрузкой сцены Bunker(main), а BunkerStateRestorer сбрасывает в false после восстановления
состояния бункера.

ProgressService

Файл:

Assets/Scripts/Data/ProgressService.cs

Сохраняет:

завершённые миссии;
разблокированные миссии;
общий счёт.
Файл сохранения:

Application.persistentDataPath/progress.json

SettingsService

Файл:

Assets/Scripts/MainMenu/SettingsService.cs

Назначение: singleton настроек. Загрузка/сохранение settings.json (SchemaVersion = 3, миграция:
windowMode наследуется из старого fullscreen), EnsureInstance(), ApplyAll(), ApplyInstant(),
ApplyHeavy(), ResetAll(), MarkDirty(). Запись атомарная (tmp + File.Replace + .bak), debounce 0.5 с.
Файл сохранения: Application.persistentDataPath/settings.json.
Создаётся Bootstrapper (Boot.unity) или лениво SettingsPanel.Start().

PostFxRouter

Файл:

Assets/Scripts/Settings/PostFxRouter.cs

Назначение: единственный владелец Volume для эффектов. [DefaultExecutionOrder(-100)].
Создаёт DontDestroyOnLoad-объект PostFxRouter с тремя глобальными Volume и рантайм-профилями:
PostFX_EW (priority 10: ChromaticAberration + Vignette + FilmGrain), PostFX_NV (20: ColorAdjustments),
PostFX_Settings (30: MotionBlur). Раздаёт ссылки через Bind(EWReceiver) и Bind(NightVisionController),
переключает Motion Blur через ApplyMotionBlur(bool). Авто-поиск Volume у потребителей удалён.

AudioBus

Файл:

Assets/Scripts/Settings/AudioBus.cs

Назначение: загрузка Assets/Resources/Audio/MainMixer.mixer (Resources.Load("Audio/MainMixer")),
раздача групп Master/Music/SFX/DroneMotors/EW/UI, SetVolume(parameter, linear) в дБ (0 → −80 дБ).
Если микшера нет, Ready == false и AudioApplier работает через AudioListener.volume.

SettingsScope

Файл:

Assets/Scripts/Settings/SettingsScope.cs

Назначение: IsMission (в сцене есть DroneManager). Обновляется на sceneLoaded; от него зависит
дистанция теней (бункер/миссия) в GraphicsApplier.

4. Схемы связей
4.1 Mission flow

Boot
  ↓
Bootstrapper
  ├── создаёт SceneLoader
  ├── создаёт SettingsService
  ├── переводит GameStateMachine в MainMenu
  └── загружает MainMenu
          ↓
MainMenuController
  ├── кнопка «Играть» → OnOpenMissions()
  │     ├── MissionContext.ReturnedFromMission = false
  │     ├── GameStateMachine → Loading → Gameplay
  │     └── SceneLoader.LoadAsync("Bunker(main)")
  └── SettingsScreen
          ↓
Bunker(main) — игрок появляется стоя на точке спавна
          ↓
ходьба по бункеру → ChairInteractable (посадка по E) → ComputerInteractable (по E, сидя)
          ↓
ComputerUIController.OpenDesktop() → ComputerOS (рабочий стол)
          ↓
DesktopIcon «Миссии» → MissionMenuController.OpenMissionMenu()
          ↓
MissionSelectScreen.Populate(.)
          ↓
ProgressService.IsUnlocked(.) / IsCompleted(.) / GetRecord(.)
          ↓
MissionCardView.Bind(.)
          ↓
клик по карточке
          ↓
MissionSelectScreen.OnMissionClicked(.)
  ├── MissionCardView.SetSelected(true)
  └── MissionDetailsPanel.Show(mission, unlocked, completed, record)
          ↓
кнопка «ЗАПУСТИТЬ МИССИЮ»
          ↓
MissionDetailsPanel.LaunchRequested → MissionSelectScreen.OnLaunchRequested(.)
          ↓
MissionMenuController.StartMission(.)
  ├── MissionContext.Begin(.)
  ├── GameStateMachine → Loading
  └── SceneLoader.LoadAsync(mission.sceneName)
          ↓
миссионная сцена
          ↓
MissionController.Awake()
  ├── Loading → Gameplay (если сцена загружена через меню)
  └── читает MissionContext.Current
          ↓
MissionController.Start()
  └── DroneManager.SelectByConfig(mission.defaultDrone)
          ↓
FirstMission(Forest): AmbushMissionDirector
  ├── отключает MissionController.autoComplete = false
  ├── ведёт сценарий «наблюдение → БТР → погоня за РСЗО»
  └── MissionController.CompleteMission(result, targets)
          ↓
MissionController.EndMission(.)
  ├── MissionContext.MarkResult(.)
  ├── ProgressService.CompleteMission(.) — при успехе
  └── GameStateMachine → Debrief
          ↓
DebriefController
  ├── ShowDebrief() — результаты миссии
  └── ReturnToMenu()
        ├── MissionContext.ReturnedFromMission = true
        ├── GameStateMachine → Loading → Gameplay
        └── SceneLoader.LoadAsync("Bunker(main)")
              (ReturnToMenu() и LoadAsync() защищены от повторного вызова флагами
               isReturning и isLoading — двойная загрузка сцены исключена)
              LoadAsync: ShowLoading() → LoadSceneAsync (allowSceneActivation = false)
              → progress ≥ 0.9 → allowSceneActivation = true → op.isDone
               → задержка postLoadDelay (1.5 с) → ожидание BunkerStateRestorer.IsRestoring
               → HideLoading()
                  ↓
              Bunker(main)
                  ↓
              BunkerStateRestorer (на Player, только при ReturnedFromMission == true)
        ├── ChairInteractable.Interact(.) → посадка на стул
        ├── ComputerUIController.OpenDesktop() → ComputerOS
        ├── MissionMenuController.OpenMissionMenu() → MissionSelectPanel
        └── ReturnedFromMission = false

Текущий фактический статус:

Mission1 («Тренировочная миссия», id = training_mission) указывает PreparingDemo;
Mission2 («Первая миссия», id = mission_1) указывает FirstMission(Forest);
Mission3 исключена из реестра MissionRegistry.allMissions (осталась в папке Missions, не используется);
player.unity отсутствует в Build Settings;
сцена базы Bunker(main) добавлена в Build Settings;
сцены PreparingDemo и FirstMission(Forest) добавлены в Build Settings;
кнопка «Играть» в MainMenu загружает Bunker(main) (ReturnedFromMission = false, игрок стоит на спавне);
выбор миссии — только через компьютер в бункере (MissionMenuController);
debrief UI — базовая панель DebriefController; возврат с миссии ведёт в Bunker(main);
двойная загрузка сцены при возврате устранена — ReturnToMenu() и LoadAsync()
защищены флагами isReturning и isLoading
4.2 Drone flow

MissionDefinition.defaultDrone
          ↓
MissionContext.SelectedDrone
          ↓
MissionController
          ↓
DroneManager.SelectByConfig(.)
          ↓
сопоставление DroneConfig со слотами
          ↓
SetActive(.)
          ↓
активируется один из GameObject:
  ├── reconDrone
  ├── strikeDrone
  ├── strikeDrone_2
  ├── dropDrone
  └── intersectorDrone

Активный префаб обычно содержит:

DroneFlightController
DroneMotors
DroneCollision / InterceptorCollision
DroneCameraModule
EWReceiver
FPVZoom
HUD
TargetDetector
TargetLockManager

Конкретный набор компонентов зависит от префаба.

4.3 EW flow

EWEmitter.OnEnable
        ↓
EWService.Register(emitter)
        ↓
EWReceiver.Update
        ↓
EWService.SampleJamAt(position)
        ↓
jamIntensity
  ├── постобработка
  ├── chromatic aberration
  ├── vignette
  ├── film grain
  ├── FOV jitter
  ├── camera rect jitter
  └── OnGUI noise/tearing
        ↓
DroneFlightController
        └── drift, шум управления и нестабильность тяги

DroneConfig.ewResistance снижает итоговый уровень помех.

4.4 Vehicle AI flow

VehicleSmartAI.Update
        ↓
Physics.OverlapSphere(detectionRadius, droneLayerMask)
        ↓
оценка дистанции, скорости и опасности
        ↓
выбор currentTarget
        ↓
CalculateLeadPosition(.)
        ↓
RotateTurretTowardsTarget(.)
        ↓
TryShootAtTarget(.)
        ↓
ParticleSystem + AudioSource
        ↓
BulletParticleCollision
        ↓
EnemyDroneHealth.TakeDamage(.)


Тактическая защита:


currentTarget ближе порога
        ↓
EvaluateTacticalDefense
        ↓
DeploySmokeScreen
        ↓
smokeGrenadePrefab

4.5 Settings flow

SettingsPanel.SelectTab(index)
        ↓
SettingDescriptor (Slider / Toggle / Dropdown)
        ↓ изменение
SettingsData.<поле> = value
        ↓
SettingsService.ApplyInstant() → AudioApplier.Apply(audio) → AudioBus.SetVolume(...)
        ↓ (не «тяжёлая» или слайдер отпущен)
SettingsService.ApplyHeavy()
        ├── SettingsApplier.ApplyScreen       (Screen.fullScreenMode / Screen.SetResolution)
        ├── SettingsApplier.ApplyQualityState (уровень → VSync → targetFrameRate → LOD → mipmap)
        ├── GraphicsApplier.Apply             (клон URP: renderScale / shadowDistance / каскады / MSAA / мягкие тени)
        ├── PostFxRouter.ApplyMotionBlur      (override MotionBlur)
        └── CameraApplier.Refresh             (FPS-камера бункера и камеры дронов)
        ↓
SettingsService.MarkDirty() → settings.json (debounce 0.5 с, атомарная запись)

Отдельно: SettingsService.OnSceneLoaded → SettingsScope.Refresh() → ApplyAll() (всё применяется заново).

4.6 Post FX flow (РЭБ и ПНВ)

PostFxRouter.Awake (первый Ensure)
        ↓
CreateInstance<VolumeProfile> × 3 → Volume (global, DontDestroyOnLoad)
        ↓
EWReceiver.Awake  → PostFxRouter.Ensure().Bind(this)   → SetEffects(CA, Vignette, Grain)
NightVisionController.Awake → PostFxRouter.Ensure().Bind(this) → SetColorAdjustments(ColorAdjustments)
        ↓
EWReceiver.Update: jamIntensity → VisualScale (ewVisualDistortion × SetVisualScale)
        → CA/Vignette/Grain intensity (active = false, когда помех или искажений нет)
        → ApplyFrameJitter → cam.rect + FPVZoom.SetFovOffset(offset) (аддитивно, не сброс FOV)
NightVisionController.Update: nightVisionColor + nvPostExposure × nightVisionGain → ColorAdjustments

5. Скрипты по категориям
5.1 Player
Assets/Scripts/Player/PlayerController.cs

Класс: PlayerController

Назначение: FPS-движение, мышь, гравитация, Animator

Зависимости: CharacterController, Animator, новый низкоуровневый Input System

Статус: подключён в player.unity

Поля: speed, gravity, mouseSensitivity, cameraTransform, animator, isSitting (при посадке
блокируются движение и гравитация; вращение камеры мышью остаётся доступным)

Методы: Start, Update, SetSitting, SetTyping, ForceSitIdle (сбрасывает Animator в SitIdle)

Настройки: LookSensitivity и InvertLookY читают controls.fpsMouseSensitivity и controls.fpsInvertY
из SettingsService (без сервиса работает старое mouseSensitivity × 1/60). Клампы вертикали пока
зашиты (−70…90), поля controls.verticalClampMin/Max не используются.

5.2 Camera / HUD
Camera/DroneCameraModule.cs

Класс: DroneCameraModule

Назначение: гимбал и фиксированная камера

Зависимости: старый Input.GetKey, DroneConfig

Статус: дроновый прототип

Camera/DropHUD.cs

Класс: DropHUD

Назначение: HUD сброса бомб и CCIP

Зависимости: DroneDropMechanism, Rigidbody, UI

Статус: дроновый прототип

Camera/FPVCameraShake.cs

Класс: FPVCameraShake

Назначение: тряска FPV-камеры

Зависимости: старый Input.GetAxis, DroneConfig, SettingsService (cameraShakeIntensity)

Статус: дроновый прототип; амплитуды умножаются на gameplay.cameraShakeIntensity, при 0 камера
возвращается в исходную позу (даже если DroneConfig.enableCameraShake == true).

Camera/FPVZoom.cs

Класс: FPVZoom

Назначение: переключение FOV; единственный владелец Camera.fieldOfView дрона

Зависимости: старый Input.GetKeyDown, DroneConfig, SettingsService (droneFov), EWReceiver (SetFovOffset)

Статус: дроновый прототип; лестница зума масштабируется от базового FOV из настроек
(zoomLevels[i] × droneFov / zoomLevels[0]), собственный currentFov сглаживается, а внешнее дрожание
РЭБ добавляется как fovOffset — зум больше не «сбрасывается» при помехах.

Camera/IntersectorHUD.cs

Класс: InterceptorHUD

Назначение: HUD перехватчика и упреждение

Зависимости: Rigidbody, DroneCameraModule

Статус: дроновый прототип

Camera/NIghtVisionController.cs

Класс: NightVisionController

Назначение: ПНВ, IR Light, ColorAdjustments

Зависимости: PostFxRouter (Bind), DroneConfig, SettingsService

Статус: дроновый прототип; Volume больше не ищется. Цвет из graphics.nightVisionColor
(0 зелёный / 1 белый / 2 янтарный), яркость = DroneConfig.nvPostExposure × nightVisionGain,
«активность» компонента ColorAdjustments живёт в рантайм-профиле PostFX_NV.

Camera/ReconHUD.cs

Класс: ReconHUD

Назначение: LRF, рамка цели, разведывательный HUD

Зависимости: TargetLockManager, TargetObject, HudStyleService

Статус: дроновый прототип; цвет/прозрачность/масштаб (GUI.matrix), толщина линий, размер перекрестия
и показ рамки захвата берутся из HudStyleService (gameplay.hudTransparency, hudScale, hudLineThickness,
crosshairSize, showTargetFrame).

Camera/StrikeHUD.cs

Класс: StrikeHUD

Назначение: ударный HUD и предупреждение РЭБ

Зависимости: DroneCameraModule, EWReceiver, HudStyleService

Статус: дроновый прототип; прицел/толщина линий/рамка захвата и прозрачность — из HudStyleService.
Предупреждение [WARNING: EW JAMMING] показывается всегда (геймплейная информация).

Camera/TargetDetector.cs

Класс: TargetDetector

Назначение: поиск целей в радиусе и FOV

Зависимости: DroneConfig, TargetObject

Статус: дроновый прототип

Camera/TargetLockManager.cs

Класс: TargetLockManager

Назначение: захват цели около центра экрана

Зависимости: старый Input.GetKeyDown, TargetDetector

Статус: дроновый прототип

5.3 Core
Core/Bootstrapper.cs

Класс: Bootstrapper

Назначение: старт сервисов и загрузка MainMenu

Зависимости: SceneLoader, SettingsService, FSM

Статус: Boot.unity

Core/DamageSystem.cs

Классы: DamageData, IDamageable, ExplosionEventData

Назначение: общий контракт урона

Зависимости: WarheadType, ModuleType

Статус: используется боевыми системами

Core/EffectService.cs

Класс: EffectService

Назначение: spawn VFX и звуки

Зависимости: Unity Instantiate, AudioBus (группа SFX), пул AudioSource

Статус: используется; PlaySound работает через пул «EffectAudioPool» (DontDestroyOnLoad, 16 источников,
outputAudioMixerGroup = AudioBus.Sfx) вместо PlayClipAtPoint, поэтому попадает под ползунок «Эффекты».

Core/EWService.cs

Класс: EWService

Назначение: реестр источников РЭБ

Зависимости: EWEmitter

Статус: используется EW

Core/ExplosionService.cs

Класс: ExplosionService

Назначение: splash-урон

Зависимости: IDamageable, Physics

Статус: используется взрывами

Core/GameEvents.cs

Класс: GameEvents

Назначение: глобальная шина событий

Зависимости: Vehicle/Drone типы

Статус: используется

Core/GameStateMachine.cs

Класс: GameStateMachine

Назначение: состояния игры

Зависимости: GameState

Статус: используется меню/миссиями

Core/SceneLoader.cs

Класс: SceneLoader

Назначение: асинхронная загрузка сцен с экраном загрузки «LOADING...»

Зависимости: SceneManager, CanvasGroup (поле loadingScreen), BunkerStateRestorer (IsRestoring)

Статус: MainMenu/Bootstrapper; LoadAsync() защищён от повторного входа флагом
isLoading (сбрасывается после завершения загрузки) — повторный вызов игнорируется,
двойная загрузка одной и той же сцены исключена. Перед LoadSceneAsync вызывается
ShowLoading() (alpha = 1, blocksRaycasts = true). После завершения активации новой
сцены (op.isDone) экран остаётся непрозрачным ещё postLoadDelay (1.5 с, таймер на
Time.unscaledDeltaTime), а при возврате в бункер — до завершения
BunkerStateRestorer.IsRestoring (таймаут restoreWaitTimeout = 3 с). Затем вызывается
HideLoading() (alpha = 0, blocksRaycasts = false) и сбрасывается isLoading. Если сцена
не найдена (op == null), экран скрывается и isLoading сбрасывается

5.4 Data / Missions
Data/MissionContext.cs

Класс: MissionContext

Назначение: статический контекст миссии

Зависимости: MissionDefinition, DroneConfig

Статус: используется меню/миссиями

Data/MissionController.cs

Класс: MissionController

Назначение: выбор дефолтного дрона (SelectByConfig в Start), цели, таймер, завершение миссии.
Публичный API: CompleteMission(result, targetsDestroyed) — единая точка завершения миссии;
флаг autoComplete (public) — если false, MissionController не завершает миссию по своим условиям
(им управляет внешний директор, например AmbushMissionDirector).

Зависимости: DroneManager, GameEvents, ProgressService, GameStateMachine, MissionContext, SceneLoader

Окно крушения в тренировочной миссии: MissionController подписывается на GameEvents.DroneExploded
(OnDroneDestroyed в OnEnable/OnDisable) и при уничтожении ЛЮБОГО дрона в PreparingDemo
(mission.sceneName.Contains("PreparingDemo")) запускает корутину DelayedCrashPanel, которая через 0.5 с
реального времени (WaitForSecondsRealtime) проверяет isEnded и, если миссия уже завершена, не показывает
окно; иначе находит MissionUIManager через FindAnyObjectByType<MissionUIManager>() и вызывает
ShowCrashPanel() (окно «Вы разбились»). Кнопка
«Перезапустить» (RestartMission) перезагружает PreparingDemo через SceneLoader.Instance.LoadAsync(...),
кнопка «Выйти в бункер» (ReturnToBunker) загружает Bunker(main) — бесконечный респавн, даже если в
запасе остались другие дроны. Потеря всех дронов (LateUpdate, GetAliveDronesCount() <= 0) в остальных
миссиях по-прежнему вызывает EndMission(MissionResult.Failed).

Статус: подключён в PreparingDemo (объект MissionManager, ссылка droneManager → GameSystems/DroneManager) и в FirstMission(Forest) (объект MissionManager, ссылка droneManager → корневой объект DroneManager); в player.unity отсутствует

Managers/AmbushMissionDirector.cs

Класс: AmbushMissionDirector

Назначение: сценарный директор первой миссии (FirstMission(Forest)). Ведёт корутину
«наблюдение → уничтожить БТР → погоня за отступающей РСЗО»; в Start() находит MissionController
(FindFirstObjectByType при пустой ссылке), выставляет missionController.autoComplete = false и по
завершении сценария вызывает missionController.CompleteMission(result, targetsDestroyed), чтобы
прогресс и переход в Debrief выполнил MissionController. Показывает титры (subtitlePanel/subtitleText).

Зависимости: MissionController, DroneFlightController, VehicleHealth, VehiclePatrol, VehicleSmartAI,
HimarsStrike, GameStateMachine/MissionContext (fallback), SettingsService (subtitlesEnabled,
subtitleFontSize — гейт стоит только на видимость и размер шрифта, тайминги сценария не меняются)

Статус: подключён в FirstMission(Forest) (объект AmbushMissionDirector)

Data/MissionDefinition.cs

Классы: MissionDefinition, MissionRegistry

Назначение: данные миссий

Зависимости: DroneConfig, ScriptableObject

Статус: assets в Resources

Data/ProgressService.cs

Классы: ProgressService, SaveData, MissionRecord

Назначение: JSON-сохранение прогресса и статистики прохождений

Зависимости: JsonUtility, IO

Поля сохранения: completedMissionIds, unlockedMissionIds, totalScore, records

MissionRecord: id, completed, bestTime, lastTime, bestScore, lastScore, completions, attempts, bestTargets

API: IsUnlocked, IsCompleted, GetRecord(mission) / GetRecord(id), RecordResult(mission, result, seconds,
targetsDestroyed), ComputeScore(mission, result, seconds, targetsDestroyed), CompleteMission, ResetProgress

Счёт: при успехе rewardPoints + 25 за каждую уничтоженную цель + до 50 очков за скорость при наличии
timeLimit; при провале 0. Формула живёт только в ComputeScore, поэтому дебрифинг и панель деталей
показывают одинаковое значение.

Статус: MissionController.EndMission вызывает RecordResult для любого исхода (успех и провал),
DebriefController и MissionDetailsPanel читают счёт через ComputeScore. Старые progress.json без
records читаются без миграции (Sanitize подставляет пустые списки).

Data/DebriefController.cs

Класс: DebriefController

Назначение: панель результатов миссии и возврат с миссии на базу

Зависимости: GameStateMachine, SceneLoader, MissionContext, TMP_Text

Статус: по кнопке дебрифинга переводит состояние Debrief → Loading → Gameplay
и загружает сцену Bunker(main); перед загрузкой ставит MissionContext.ReturnedFromMission = true;
метод ReturnToMenu() защищён от повторного вызова флагом isReturning. Подключён в PreparingDemo и
FirstMission(Forest): объект MissionManager, ссылки panel/titleText/statsText/returnToMenuButton/
debriefCamera назначены на UI дебрифинга и камеру-заглушку DebriefCamera

5.5 DroneTypes
DroneTypes/DroneConfig.cs

Классы: DroneConfig, WarheadType

Назначение: конфигурация дрона

Зависимости: KeyCode-поля, ScriptableObject

Статус: используется префабами

DroneTypes/DroneFlightScript.cs

Класс: ShahedFlight

Назначение: автополёт к цели

Зависимости: Rigidbody

Статус: вражеский drone prototype

DroneTypes/DroneHealth.cs

Класс: EnemyDroneHealth

Назначение: здоровье и падение вражеского дрона

Зависимости: ShahedFlight, Rigidbody

Статус: вражеский drone prototype

DroneTypes/DroneManager.cs

Класс: DroneManager

Назначение: управление пулом дронов сцены. InitializeDrones() вызывается в Awake() и строит
activeSlots из mission.availableDrones (или из всего пула при тестовом запуске без миссии).
Дрон НЕ включается автоматически, если задана миссия с defaultDrone — выбор делает MissionController
через SelectByConfig(mission.defaultDrone) в Start(). Автовыбор первого слота остаётся только для
тестового запуска без миссии (MissionContext.Current == null).

OnDroneDestroyed (подписка на GameEvents.DroneExploded): обнуляет слот уничтоженного дрона и, если
активный борт потерян, а GetAliveDronesCount() > 0, автоматически вызывает SelectDroneByIndex для первого
живого борта (с записью в лог). Если живых бортов не осталось, currentActiveIndex = −1 — тогда
MissionController в LateUpdate завершает миссию провалом. Ручное переключение — клавиши 1..9 в Update().

Зависимости: старый Input.GetKeyDown, DroneConfig, MissionContext, GameEvents

Статус: дроновые сцены

5.6 EW
EW/EWEmiter.cs

Класс: EWEmitter

Назначение: источник помех

Зависимости: EWService

Статус: дроновые сцены

EW/EWReceiver.cs

Класс: EWReceiver

Назначение: расчёт и визуализация помех

Зависимости: PostFxRouter (Bind), FPVZoom (SetFovOffset), DroneConfig, SettingsService

Статус: дроновые сцены. Авто-поиск Volume удалён, компоненты выдаёт PostFxRouter.
Публичный API: jamIntensity (читают DroneFlightController, StrikeHUD, EWAudio), VisualScale,
SetVisualScale(0..1), SetEffects(ChromaticAberration, Vignette, FilmGrain).
CA/Vignette/Grain масштабируются произведением ewVisualDistortion и SetVisualScale; компоненты
выключаются (active = false), когда помех или искажений нет. Дрожание кадра — cam.rect (сбрасывается)
и FPVZoom.SetFovOffset (аддитивно). reduceDangerousEffects отключает IMGUI-«снег» и полосы разрыва.
Разрешение шума берётся из ewNoiseResolution (иначе DroneConfig.noiseTextureResolution).

5.7 MainMenu / UI
MainMenu/GameSession.cs

Класс: GameSession

Назначение: singleton выбранной конфигурации

Зависимости: DroneConfig

Статус: заготовка / частично используется

MainMenu/MainMenuController.cs

Класс: MainMenuController

Назначение: навигация меню; кнопка «Играть» (OnOpenMissions) ставит
MissionContext.ReturnedFromMission = false и загружает сцену Bunker(main)

Зависимости: FSM, MissionContext, SceneLoader

Статус: MainMenu.unity

MainMenu/MissionCardView.cs

Класс: MissionCardView

Назначение: карточка миссии

Поля: previewImage, nameLabel, metaText, lockedOverlay, completedBadge, selectedFrame, button

Методы: Bind(mission, unlocked, completed, onClick), SetSelected(bool), свойство Mission

Зависимости: UI, TMP, MissionDefinition, MissionText

Статус: Mission UI. Карточка больше не запускает миссию: клик передаёт миссию в MissionSelectScreen.
Button.interactable всегда true (заблокированная карточка открывает детали с причиной блокировки).
Файл пересохранён в UTF-8 (был в Windows-1251).

MainMenu/MissionSelectScreen.cs

Класс: MissionSelectScreen (наследник UIScreen)

Назначение: генерация карточек и управление выбором миссии (master-detail)

Поля: cardPrefab, cardsContainer, detailsPanel, headerSubtitleLabel, totalScoreLabel, verboseLogging

Методы: Populate(registry, onPlay), CloseDetails(), свойство IsDetailsOpen

Поведение: Populate создаёт карточки и обновляет шапку (число операций и общий счёт); клик по карточке
выделяет её и вызывает MissionDetailsPanel.Show; миссия запускается только по событию
LaunchRequested панели деталей, которое пробрасывается в onPlay (MissionMenuController.StartMission).

Зависимости: ProgressService, MissionRegistry, MissionDetailsPanel, MissionCardView

Статус: Mission UI

MainMenu/MissionDetailsPanel.cs

Класс: MissionDetailsPanel

Назначение: правая панель деталей миссии (баннер, статус, название, описание, статистика, цели,
техника, подсказка/причина блокировки, кнопка запуска)

Поля инспектора: group, emptyState, contentRoot, bannerImage, bannerCodeText, statusIcon, statusLabel,
rewardLabel, titleLabel, descriptionLabel, timeValue, scoreValue, completionsValue, targetsValue,
noteText, goalLines[3], goalMarkers[3], techLine, launchButton, launchLabel, completedColor,
pendingColor, lockedColor, revealDuration

Методы: Show(mission, unlocked, completed, record), ShowEmpty(), Hide(), событие LaunchRequested,
свойство IsOpen

Поведение: состояние «не пройдена» показывает прочерки, условия выполнения и награду; «пройдена» —
лучшее время, счёт, число прохождений и целей из MissionRecord; «заблокирована» — причину по
prerequisites и недоступную кнопку. Появление с fade на unscaled-времени (в меню Time.timeScale = 0).

Зависимости: MissionDefinition, MissionRecord, ProgressService, MissionText, TMP, UI

Статус: Mission UI, встроена в префаб MissionSelectPanel (дочерний объект MissionDetailsPanel)

MainMenu/MissionText.cs

Класс: MissionText (static)

Назначение: форматирование текстов миссий

Методы: Targets(count), Objective(mission), TimeLimit(seconds), Duration(seconds), DroneRole(config)

Зависимости: MissionDefinition, DroneConfig, CameraType

Статус: используется MissionCardView и MissionDetailsPanel

MainMenu/SettingsScreen.cs

Класс: SettingsScreen

Назначение: экран настроек

Зависимости: SettingsService, UI

Статус: MainMenu

MainMenu/SettingsService.cs

Класс: SettingsService

Назначение: сохранение настроек

Зависимости: JSON, QualitySettings

Статус: MainMenu/Bootstrapper

MainMenu/SimpleUIScreen.cs

Класс: SimpleUIScreen

Назначение: пустой UI-наследник

Зависимости: UIScreen

Статус: заготовка

MainMenu/UIScreen.cs

Класс: UIScreen

Назначение: Show/Hide через CanvasGroup

Зависимости: CanvasGroup

Статус: базовый UI-класс

5.8 Physics
Physics/DroneCollision.cs

Класс: DroneCollision

Назначение: столкновения, урон, взрыв

Зависимости: старый Input.GetKeyDown, ExplosionService

Статус: дроны

Physics/DroneFlightController.cs

Класс: DroneFlightController

Назначение: физический полёт квадрокоптера

Зависимости: старый Input.GetKey, DroneMotors, EWReceiver, PID, SettingsService (controls.*)

Статус: дроны.
Ввод читается в ReadInput(), в начале кадра вызывается RefreshInputSettings():
  droneStickSpeed     — скорость отклонения стика (было const 4.0f),
  droneCenteringSpeed — скорость возврата стика в центр (было const 8.0f),
  throttleSpeed       — скорость изменения газа (было поле throttleSpeed = 1.8f),
  throttleMode        — 0: самовозврат газа к 0.5 при отпущенных W/S, 1: фиксация (поведение по умолчанию),
  droneInvertY        — инверсия pitch (стрелки ↑/↓).
Настройки применяются на входе, до дрейфа РЭБ и до PID. Физика, PID-коэффициенты, аэродинамика,
maxPitchRate/maxRollRate и ApplyDroneProfile() настройками не затрагиваются.

Physics/DroneMotors.cs

Класс: DroneMotors

Назначение: тяга четырёх моторов

Зависимости: Rigidbody

Статус: дроны; публикует CurrentThrust01 (максимум из четырёх clamped-значений тяги) —
его читает DroneMotorAudio для громкости и питча гула. Физика тяги не изменена.

Physics/DropDroneMechanism.cs

Класс: DroneDropMechanism

Назначение: сброс бомб

Зависимости: старый Input.GetKeyDown

Статус: дроны

Physics/DroppedBomb.cs

Класс: DroppedBomb

Назначение: взрыв бомбы и force

Зависимости: ExplosionService, EffectService

Статус: дроны

Physics/IntersectorCollision.cs

Класс: InterceptorCollision

Назначение: proximity fuse и взрыв

Зависимости: DroneConfig, ExplosionService

Статус: дроны

Physics/PIDController.cs

Класс: PIDController

Назначение: PID-регулятор

Зависимости: математика

Статус: используется FlightController

Physics/TargetObject.cs

Класс: TargetObject

Назначение: имя, враждебность, точка цели

Зависимости: Transform

Статус: HUD/TargetDetector

5.9 TargetObjectScripts
TargetObjectScripts/BulletParticleCollision.cs

Класс: BulletParticleCollision

Назначение: попадание ParticleSystem по дрону

Зависимости: ParticleSystem, Tag Drone

Статус: AI техники

TargetObjectScripts/VehicleHealth.cs

Класс: VehicleHealth

Назначение: здоровье и уничтожение техники

Зависимости: IDamageable, GameEvents, WheelCollider

Статус: BTR/техника

TargetObjectScripts/VehicleModule.cs

Класс: VehicleModule

Назначение: здоровье модулей и броня

Зависимости: VehicleHealth, IDamageable

Статус: BTR/техника

TargetObjectScripts/VehiclePatrol.cs

Класс: VehiclePatrol

Назначение: маршрут и terrain alignment

Зависимости: Rigidbody, LayerMask

Статус: техника

TargetObjectScripts/VehicleSmartAI.cs

Класс: VehicleSmartAI

Назначение: поиск, наведение, стрельба, дым

Зависимости: Physics, ParticleSystem, AudioSource

Статус: техника

5.10 Buildings
Buildings/DestructibleWallZone.cs

Класс: DestructibleWallZone

Назначение: пролом стены от дронов/бомб/частиц

Зависимости: Collider, Tags, ParticleCollision

Статус: отдельная заготовка

5.11 Interaction
Каталог: Assets/Scripts/Interaction/

IInteractable.cs — интерфейс: GetInteractionPrompt / CanInteract / Interact.

PlayerInteractionDetector.cs — висит на Player: луч из камеры (Physics.Raycast, дистанция 3,
маска слоя Interactable, QueryTriggerInteraction.Ignore → триггеры НЕ работают), клавиша E,
GetComponentInParent<IInteractable>. Требует назначенное поле uiController (иначе return).

InteractionUIController.cs — окна «Магазин» (Shop) и «Карта миссий» (MissionBoard) через
CanvasGroup + подсказка; разблокировка курсора, timeScale = 0 при открытии, закрытие по E/Esc.
ShowHint уважает gameplay.showInteractionHints: при выключенных подсказках текст «…[E]» не
показывается вовсе; подсказка «Встать [Пробел]» (ShowSittingHint) намеренно не гейтится.

HangarInteractable.cs — IInteractable, открывает магазин, промпт «Магазин [E]».

MissionBoardInteractable.cs — IInteractable, открывает карту миссий, промпт «Карта миссий [E]».

ChairInteractable.cs — IInteractable, посадка за компьютер (телепорт в sitPosition,
отключение коллайдера стула chairCollider и полное отключение CharacterController на время
сидения, SetSitting(true) + блокировка движения через isSitting), вставание по Пробелу (в Update()) или
вызовом публичного StandUp() с телепортом в standPosition и обратным включением коллайдера и
CharacterController; StandUp() защищён проверкой isSitting (не срабатывает, если игрок не сидит).

ComputerInteractable.cs — IInteractable, терминал оператора Interact_OperatorTerminal: открытие по E
(если игрок сидит, playerController.isSitting, прицел на терминале) — вызывает
ComputerUIController.OpenDesktop(); закрытие при взгляде на терминал также доступно, но глобально
окно закрывается через ComputerUIController.Update() по E/Esc.

ComputerUIController.cs — висит на панели ComputerOS (InteractionCanvas): свойство IsOpen (false по
умолчанию). OpenDesktop() ставит IsOpen = true и запускает корутину AnimateOpen() — плавное появление
CanvasGroup.alpha (0→1) и зум RectTransform.localScale (0.1→1) за 0.3 с через Mathf.Lerp,
разблокировка курсора, Time.timeScale = 0. Update() при IsOpen закрывает окно по E/Esc
(eKey/escapeKey.wasPressedThisFrame) независимо от направления взгляда, с защитой от повторного
нажатия (lastOpenTime, 0.2 с). Если открыто меню миссий (MissionMenuController.IsOpen) или оно было
закрыто менее 0.2 с назад (LastClosedTime), Update() не закрывает рабочий стол, поэтому Esc закрывает
окна по очереди независимо от порядка выполнения Update(): первое нажатие — меню миссий, второе —
сам рабочий стол. CloseDesktop() ставит IsOpen = false, alpha = 0, Time.timeScale = 1,
блокирует курсор и НЕ вызывает StandUp() — игрок остаётся сидеть. Вместо этого после возврата
Time.timeScale в 1 вызывается PlayerController.ForceSitIdle(): он ставит IsSitting = true и через
animator.Play("SitIdle", 0, 1f) принудительно сбрасывает Animator в финальную позу сидения, из-за
чего застывшая при Time.timeScale = 0 анимация посадки (Stand To Sit) не доигрывается после закрытия
рабочего стола. Вставание со стула происходит только по Пробелу в ChairInteractable.Update().

DesktopIcon.cs — висит на кнопке-иконке «Миссии» внутри ComputerOS: OnIconClicked() вызывает
MissionMenuController.OpenMissionMenu(), который открывает панель выбора миссий (MissionSelectPanel
из MainMenu) поверх рабочего стола ComputerOS — рабочий стол остаётся видимым на фоне.

MissionMenuController.cs — контроллер панели выбора миссий внутри бункера. OpenMissionMenu()
заполняет MissionSelectScreen миссиями из MissionRegistry и показывает панель поверх рабочего стола
ComputerOS (рабочий стол не выключается); CloseMissionMenu() (кнопка «Назад» или Esc в Update(), с защитой lastOpenTime 0.2 с) скрывает панель —
рабочий стол остаётся на месте — и фиксирует LastClosedTime; StartMission() запускает миссию через MissionContext.Begin() и
SceneLoader.LoadAsync(). Старый MissionBoardWindow удалён.

MissionUIManager.cs — висит на объекте UIManager в миссионных сценах PreparingDemo и
FirstMission(Forest). Поля pausePanel (GameObject), crashPanel (GameObject) и fallbackCamera
(GameObject, заглушка чёрного фона). Update() по Esc
(Keyboard.current.escapeKey.wasPressedThisFrame), пока crashPanel не активен И игра не в состоянии
Debrief, переключает pausePanel: при открытии Time.timeScale = 0 и курсор разблокируется, при закрытии
Time.timeScale = 1 и курсор блокируется. ShowCrashPanel() также делает ранний return, если
GameStateMachine.Current == GameState.Debrief, иначе включает crashPanel, выключает pausePanel,
включает fallbackCamera (заглушка чёрного фона, чтобы не было надписи «No cameras rendering» после
уничтожения камеры дрона), Time.timeScale = 0, курсор разблокируется. Тем самым PausePanel и
CrashPanel взаимоисключающие с DebriefPanel. Дополнительно скрипт в OnEnable/OnDisable подписывается на GameStateMachine.StateChanged;
метод OnStateChanged(GameState prev, GameState next) при переходе в GameState.Debrief принудительно
сбрасывает Time.timeScale = 1 и выключает pausePanel и crashPanel, поэтому CrashPanel не накладывается
на DebriefPanel, даже если крушение дрона произошло одновременно с выполнением цели. ResumeGame()
закрывает pausePanel, выключает fallbackCamera (Time.timeScale = 1, курсор блокируется).
RestartMission() (Time.timeScale = 1) перезагружает PreparingDemo. ReturnToBunker() (Time.timeScale = 1)
ставит MissionContext.ReturnedFromMission = true, при возможности переводит FSM в Loading и загружает
Bunker(main) через SceneLoader.Instance.LoadAsync — как и DebriefController.ReturnToMenu(), поэтому игрок
возвращается в бункер сидя за компьютером (BunkerStateRestorer). Кнопки PausePanel и CrashPanel в
PreparingDemo стилизованы единообразно с DebriefPanel/ReturnButton: серый фон Image (0.25/0.25/0.25),
белый текст.

Скрытие HUD при открытии панелей: MissionUIManager содержит статический флаг IsHudHidden и метод
SetHudVisible(bool). Поле instructionText (GameObject) ссылается на Canvas/InstructionText (uGUI,
скрывается через SetActive; у объекта выключен Raycast Target). IMGUI-HUD дрона — ReconHUD, StrikeHUD,
EWReceiver, IntersectorHUD — в начале OnGUI() делает ранний return, если MissionUIManager.IsHudHidden
== true. SetHudVisible(false) вызывается при открытии паузы (TogglePause), показе CrashPanel
(ShowCrashPanel) и переходе в Debrief (OnStateChanged); SetHudVisible(true) — в ResumeGame() и при
закрытии паузы. В OnEnable() флаг сбрасывается в false, поэтому HUD видим при старте и рестарте сцены.
Дополнительно те же IMGUI-HUD-скрипты в начале OnGUI() делают ранний return при
SceneLoader.Instance != null && SceneLoader.Instance.IsLoading, поэтому во время загрузки сцены
(чёрный экран «LOADING...») HUD не рисуется.

BunkerStateRestorer.cs — висит на Player в сцене Bunker(main). Публичное свойство IsRestoring
выставляется в true в Awake() при MissionContext.ReturnedFromMission == true и сбрасывается в
false в конце восстановления. При ReturnedFromMission == true в Start() через корутину с задержкой
в один кадр (чтобы отработали все Start сцены) восстанавливает состояние возврата с миссии:
ChairInteractable.Interact() (посадка на стул), ComputerUIController.OpenDesktop() (ComputerOS)
и MissionMenuController.OpenMissionMenu() (MissionSelectPanel с актуальным прогрессом), затем
сбрасывает флаг ReturnedFromMission = false. SceneLoader ждёт IsRestoring == false, чтобы не
снять экран «LOADING...» раньше времени.

5.12 Settings / Audio / HUD
Каталог: Assets/Scripts/Settings/ (подсистема настроек) и Assets/Scripts/Audio/ (процедурный звук).

Settings/SettingsScope.cs — IsMission (в сцене есть DroneManager). Обновляется на sceneLoaded;
используется для выбора дистанции теней.

Settings/GraphicsApplier.cs — рантайм-клон активного URP-ассета (Object.Instantiate, суффикс
«(Runtime)»), назначается в QualitySettings.renderPipeline текущего уровня и возвращается назад по
Application.quitting. Применяет renderScale, shadowDistance (SettingsScope.IsMission ?
droneShadowDistance : bunkerShadowDistance), shadowCascadeCount (bunkerShadowQuality, 1..4),
msaaSampleCount (bunkerMsaa), mainLightShadowmapResolution и мягкие тени (рефлексия в
m_SoftShadowsSupported — публичного сеттера в URP нет). Оригинальный PC_RPAsset.asset не правится,
GraphicsSettings.defaultRenderPipeline не переключается.

Settings/CameraApplier.cs — Refresh() перебирает все камеры: если в родителях CharacterController —
FPS-камера (bunkerFarClip, bunkerPostProcessing), иначе если есть FPVZoom/DroneCameraModule — камера
дрона (droneFarClip, renderShadows по droneShadowDistance > 0, antialiasing по droneAntiAliasing).
Переприменяется на sceneLoaded и на GameEvents.DroneSwitched.

Settings/AudioBus.cs — AudioMixer из Resources/Audio/MainMixer, группы Master/Music/SFX/DroneMotors/EW/UI,
SetVolume(parameter, linear) в дБ. Ассет микшера существует (Assets/Resources/Audio/MainMixer.mixer,
guid 8f63ca1855520d5438cde486b7dbf6ac, создан скриптом AudioMixerBuilder): Master → { Music,
SFX → { DroneMotors, EW }, UI, Voice }, Expose-параметры MasterVolume / MusicVolume / SfxVolume /
MotorsVolume / EwVolume / UiVolume / VoiceVolume, громкости групп 0 дБ в стартовом снапшоте, вид микшера
содержит все 7 групп.

Settings/AudioApplier.cs — 7 ползунков в AudioMixer; без микшера — fallback на AudioListener.volume.

Audio/DroneMotorAudio.cs — гул моторов: процедурный бесшовный луп (96 Гц + гармоники), громкость и питч
от DroneMotors.CurrentThrust01 и DroneFlightController.isMotorStarted. Вешается на корень префаба дрона.

Audio/EWAudio.cs — треск помех: отфильтрованный шум, громкость от EWReceiver.jamIntensity.
Вешается на камеру дрона.

Settings/PostFxRouter.cs — см. разделы 3 и 4.6.

UI/SettingsPanel.cs — панель настроек (4 вкладки), кнопка «Сброс» → ResetSettings() → ResetAll().

UI/SettingsListBuilder.cs — конструктор строк Slider / Toggle / Dropdown (раскрывается списком внутри
ScrollRect); для «тяжёлых» строк выставляет RequiresRelease.

UI/SliderReleaseNotifier.cs — OnEndDrag / PointerUp → ApplyHeavy().

UI/FpsCounterHud.cs — IMGUI-счётчик FPS; объект «FpsCounterHUD» создаётся автоматически
(RuntimeInitializeOnLoadMethod + DontDestroyOnLoad), рисуется только в состоянии Gameplay.

Settings/HudStyleService.cs — стиль HUD: Scale, Transparency, ApplyAlpha, Opaque, CrosshairSize,
LineThickness, TargetFrameVisible, Begin()/End() (GUI.matrix вокруг центра экрана).

Data/SettingsApplier.cs — точка входа применения: Apply / ApplyInstant / ApplyHeavy / ApplyScreen,
GetResolutionOptions(), GetUniqueResolutions(), WindowModeLabels, ResetScreenCache().

6. Сцены
Всего в Assets найдено 27 файлов .unity: 8 собственных в Assets/Scenes (Boot, MainMenu, Bunker(main),
PreparingDemo, FirstMission(Forest), FirstMission(SpizjenyiVarik), player, SampleScene, TerrainDemoScene,
FunctionalUI-Demo), 3 резервных в Assets/_Recovery и 16 сторонних demo-сцен в пакетах ассетов
(ALP_Assets, Polyzero, R3DWorks, Realistic Tree, S-Mine 35, TerrainTexturesPackFree, UnityTechnologies,
Vefects ×2, Volklight, ZIL130_MilitaryTruck). В Build Settings входят 5 сцен.

Assets/Scenes/Boot.unity

Назначение: старт и Bootstrapper

Статус: основная цепочка

Камера-заглушка: BootCamera (Camera: Clear Flags = Solid Color, чёрный фон,
Culling Mask = Nothing, Depth = -100) — рисует чёрный фон позади UI, чтобы в окне Game
не появлялась надпись «No cameras rendering» при запуске игры.

Assets/Scenes/MainMenu.unity

Назначение: меню, миссии, настройки

Статус: основной UI-прототип

Assets/Scenes/SampleScene.unity

Назначение: старая тестовая миссия

Статус: не используется миссиями (заменена на PreparingDemo)

Assets/Scenes/PreparingDemo.unity

Назначение: тренировочная миссия

Статус: Mission1 («Тренировочная миссия», id = training_mission); в Build Settings.
Настроены контроллеры: объект MissionManager (MissionController + DebriefController), UI дебрифинга
Canvas/DebriefPanel (DebriefTitle, DebriefStats, ReturnButton), камера-заглушка DebriefCamera и объект
UIManager (MissionUIManager: меню паузы PausePanel по Esc и окно крушения CrashPanel).
В DroneManager (GameSystems) заполнен allDronesInScene: GimbalDrone Variant → MavicReconConfig
(разведчик) и StrikeDrone 1 → SmallFpvHeatConfig (ударный FPV). Миссия завершается по уничтожению
цели potujnojet, после чего DebriefController показывает результат, а кнопка возврата ведёт
в Bunker(main). При уничтожении любого дрона (событие GameEvents.DroneExploded) вместо провала
показывается окно «Вы разбились» (CrashPanel): «Перезапустить» перезагружает сцену (бесконечный
респавн), «Выйти в бункер» загружает Bunker(main).

Assets/Scenes/FirstMission(Forest).unity

Назначение: первая миссия

Статус: Mission2 («Первая миссия», id = mission_1); в Build Settings.
Настроены контроллеры: объект MissionManager (MissionController + DebriefController), UI дебрифинга
Canvas/DebriefPanel (DebriefTitle, DebriefStats, ReturnButton), камера-заглушка DebriefCamera,
EventSystem, объект AmbushMissionDirector (сторонний сценарный директор миссии) и объект UIManager
(MissionUIManager: меню паузы PausePanel по Esc).
Меню паузы: панель Canvas/PausePanel (по умолчанию выключена) с кнопками continue (ResumeGame) и
exit (ReturnToBunker); поля Fallback Camera и Crash Panel у MissionUIManager пустые (в этой миссии
потеря всех дронов ведёт к обычному провалу, без окна крушения).
В DroneManager (корневой объект DroneManager) заполнен allDronesInScene:
GimbalDrone Variant → MavicReconConfig (разведчик) и StrikeDrone 1 / (1) / (2) / (3) →
SmallFpvHeatConfig (ударный FPV). Миссия завершается по уничтожению двух враждебных целей
(БТР и РСЗО): AmbushMissionDirector ведёт сценарий и по завершении вызывает
MissionController.CompleteMission(), который переводит FSM в Debrief, после чего DebriefController
показывает результат, а кнопка возврата ведёт в Bunker(main).

Assets/Scenes/FirstMission(SpizjenyiVarik).unity

Назначение: альтернативная карта первой миссии

Статус: не привязана к миссии (заготовка)

Assets/Scenes/player.unity

Назначение: FPS-база

Статус: основная рабочая база, НЕ в Build Settings

Assets/Scenes/Bunker(main).unity

Назначение: новая FPS-локация (военный бункер Bunker07Anim)

Статус: в настройке — физика, коллайдеры, интерактивный терминал; в Build Settings;
сюда возвращает DebriefController после завершения миссии.
InteractionCanvas/PausePanel/settings — инстанс префаба SettingsPanel (единственная точка входа
в настройки вне главного меню), кнопка «Сброс» в шапке. Кнопки «Настройки» в паузе миссии нет.

Assets/Scenes/FunctionalUI-Demo.unity

Назначение: UI demo

Статус: демонстрационная

Assets/Scenes/TerrainDemoScene.unity

Назначение: terrain и drone demo

Статус: прототип

Камера-заглушка: DebriefCamera (Camera: Clear Flags = Solid Color, чёрный фон,
Culling Mask = Nothing), по умолчанию выключена. Включается DebriefController в ShowDebrief()
при показе результатов миссии и выключается в ReturnToMenu() при возврате на базу, чтобы
в окне Game не появлялась надпись «No cameras rendering», когда камера дрона отключается.

Assets/_Recovery/0.unity — резерв terrain-сцены, recovery.
Assets/_Recovery/0 (1).unity — резерв terrain-сцены, recovery.
Assets/_Recovery/0 (2).unity — резерв terrain-сцены, recovery.

ALP_Assets/.../DemoScene.unity — demo дома, сторонняя.
S-Mine 35/Scenes/Demo.unity — demo мины, сторонняя.
UnityTechnologies/.../Main Scene with Legacy Particles.unity — demo эффектов, сторонняя.
Volklight/BTR/DemoScene.unity — demo БТР, сторонняя.
ZIL130_MilitaryTruck/Scenes/SampleScene.unity — demo грузовика, сторонняя.

Build Settings
Включены (5 сцен, ProjectSettings/EditorBuildSettings.asset):

Assets/Scenes/Boot.unity

Assets/Scenes/MainMenu.unity

Assets/Scenes/Bunker(main).unity

Assets/Scenes/PreparingDemo.unity

Assets/Scenes/FirstMission(Forest).unity

Удалены из Build Settings (остались в проекте как demo/старьё): Assets/Scenes/SampleScene.unity,
Assets/Scenes/TerrainDemoScene.unity. Unity не сохраняет EditorBuildSettings.asset автоматически,
поэтому чистка выполняется скриптом ProjectStabilizer.CleanBuildSettings(), который после правки
в памяти перезаписывает файл ProjectSettings/EditorBuildSettings.asset напрямую.

Не включены:

Assets/Scenes/player.unity

7. Игровые префабы
Дроны
Assets/Prefabs/Drones/GimbalDrone.prefab

Assets/Prefabs/Drones/ReconDrone.prefab

Assets/Prefabs/Drones/StrikeDrone.prefab

Содержат различные комбинации: Rigidbody, DroneFlightController, DroneMotors, DroneCollision, DroneCameraModule, EWReceiver, FPVZoom, HUD, Target Detector/Lock, Drop Mechanism, Interceptor Collision.

Звук на префабах (сделано, ранее было только в документации): на корне дрона — Audio Source
(outputAudioMixerGroup = DroneMotors, Spatial Blend = 1) + DroneMotorAudio; на камере дрона —
Audio Source (Spatial Blend = 0, outputAudioMixerGroup = EW) + EWAudio. Ссылки DroneMotors /
DroneFlightController / EWReceiver назначены автоматически скриптом DroneAudioBinder
(меню Tools/DroneSimulator/Impact/2). Расставлено на всех 5 префабах:
GimbalDrone, GimbalDrone Variant, ReconDrone, StrikeDrone, StrikeDrone 1.
Примечание: у ReconDrone в префабе нет EWReceiver, поэтому EWAudio там молчит (receiver = null).

Ещё на префабах не создан DroneDropMechanism: поле bombPrefab в DroneConfig назначено на
Assets/Prefabs/Weapons/Bomb.prefab, но сбрасывать бомбы пока нечем — механизм сброса ни на одном
префабе не висит (это следующий шаг, а не регресс).

VFX и боеприпасы
Assets/Prefabs/VFX/Explosion.prefab — процедурный префаб взрыва (корень Explosion, дети Flash и Smoke,
каждый — ParticleSystem с ParticleSystemRenderer; материалы Assets/Art/VFX/ParticleFlash.mat и
ParticleSmoke.mat, текстуры SoftGlow.png и SmokePuff.png; системы не зациклены, по окончании
уничтожают себя через ParticleSystemStopAction.Destroy).

Assets/Prefabs/Weapons/Bomb.prefab — цилиндр (Scale 0.14 / 0.22 / 0.14) с CapsuleCollider, Rigidbody
(mass 8, ContinuousDynamic) и скриптом DroppedBomb; поле explosionVFX назначено на Explosion.prefab.

Звук взрыва: Assets/Audio/SFX/Explosion.wav (процедурный синтез, моно 44.1 кГц, Vorbis).
Назначен вместе с explosionPrefab и bombPrefab во все три конфига дронов
(MavicReconConfig, SmallFpvHeatConfig, IntersectorConfig) скриптом ImpactVfxBuilder.
Генератор: Tools/DroneSimulator/Impact/1. Build Explosion And Bomb.

Техника
Assets/Prefabs/Vehicles/Btr.prefab

Содержит компоненты: VehicleHealth, VehicleModule, VehiclePatrol, VehicleSmartAI, Rigidbody, колёса, башня, ParticleSystem.

Здание и UI
Assets/Prefabs/Buildings/House.prefab

Assets/Prefabs/UI/MissionCard.prefab

Не найдено
Явных игровых префабов или объектов с названиями Tent, Van, MilitaryVan не обнаружено.

8. Модели, материалы и анимации
Персонаж
Assets/Models/Player/Swat.fbx

Assets/Models/Player/Animations/Breathing Idle.fbx

Assets/Models/Player/Animations/Walking.fbx

Assets/Models/Player/Animations/Slow Run.fbx

Assets/Models/Player/Animations/Walking Backwards.fbx

Assets/Models/Player/Animations/Stand To Sit.fbx

Assets/Models/Player/Animations/Sitting.fbx

Assets/Models/Player/Animations/Sit To Type.fbx

Assets/Models/Player/Animations/Left Strafe Walking.fbx

Assets/Models/Player/Animations/Right Strafe Walking.fbx

Дроны
Assets/Models/Drones/fpv_drone.glb

Assets/Models/Drones/DJI-Mavic_3.glb

Assets/Models/Drones/iranian_shahed-136_military_drone.glb

Техника
Assets/Models/Vehicles/patria_amv.glb

Assets/Models/Vehicles/simple_tank.glb

Assets/Volklight/BTR/Model/BTR.fbx

Assets/ZIL130_MilitaryTruck/Meshes/ZIL130.fbx

База и дома
Assets/Models/Buildings/military base.fbx

Assets/Models/Buildings/scene.gltf

Assets/ALP_Assets/country house01/Models/House.fbx

Материалы
Основные собственные материалы находятся в Assets/Materials/ и Assets/Models/Player/Materials/.

В проекте присутствуют URP, HDRP, legacy и сторонние материалы. Явных текстовых ссылок на массовое использование shader Standard не найдено.

Animator
Основной controller: Assets/Models/Player/PlayerAnimator.controller.

Параметры: Speed, Direction, Strafe, IsGrounded, IsSitting (Bool), IsTyping (Bool).

Состояния: Idle, Walk, WalkBackward, StrafeLeft, StrafeRight, SitDown, SitIdle, SitTyping (состояние Run удалено — бег отключён из-за малых размеров бункера). Стрейф включается параметром Strafe только при чисто горизонтальном вводе A/D; диагональ (W+A/W+D) остаётся обычной ходьбой Walk.

Отдельных .anim файлов не найдено — клипы импортируются из FBX.

9. ProjectSettings и пакеты
Packages
Ключевые пакеты:

com.gamelovers.mcp-unity;

com.unity.inputsystem;

com.unity.ai.navigation;

com.unity.timeline;

com.unity.ugui;

com.unity.postprocessing;

URP;

HDRP;

Shader Graph;

GLTFast;

Test Framework.

Tags
Пользовательский Tag: Drone.

Layers
Пользовательские Layers: Drone, Water, UI, PlayerBody, Interactable (индекс 7 — интерактивные объекты).

Physics
Файл: ProjectSettings/DynamicsManager.asset.

Основные значения:

gravity: (0, -9.81, 0);

solver iterations: 6;

solver velocity iterations: 1;

queriesHitTriggers: true;

autoSyncTransforms: false.

Графика (URP) и QualitySettings
Файлы: ProjectSettings/QualitySettings.asset, Assets/Settings/PC_RPAsset.asset, Assets/Settings/Mobile_RPAsset.asset.

6 уровней качества; собственный URP-ассет назначен только уровню Ultra, остальные уровни идут через
GraphicsSettings.defaultRenderPipeline = PC_RPAsset. Поэтому смена уровня качества в URP почти не меняет
картинку — реальные рычаги (renderScale, тени, MSAA) применяет GraphicsApplier через рантайм-клон.

Настройки графики в рантайме
Активный URP-ассет подменяется рантайм-клоном через QualitySettings.renderPipeline (см. 5.12), поэтому
renderScale/тени/MSAA не портят Assets/Settings/PC_RPAsset.asset. При выходе из Play Mode ссылка
восстанавливается (Application.quitting). Если в консоли появилось «[GraphicsApplier] URP-ассет не
найден» — проверь Project Settings → Graphics и Quality.

Звук (обязательный ручной ассет)
Assets/Resources/Audio/MainMixer.mixer в репозитории отсутствует и создаётся вручную: группы
Master → { Music, SFX → { DroneMotors, EW }, UI }, exposed-параметры MasterVolume, MusicVolume,
SfxVolume, MotorsVolume, EwVolume, UiVolume (опционально VoiceVolume). Без микшера AudioBus.Ready == false,
и работает только общий уровень через AudioListener.volume (один warning в консоли).

10. Известные проблемы
Исправлено в итерации стабилизации (см. раздел 12)
- Зелёный TMP-шрифт в PreparingDemo: запечённый инстанс материала «LiberationSans SDF Material (Instance)»
  с зелёным _FaceColor был у Canvas/InstructionText; сброшен на дефолтный материал шрифта, осиротевший
  инстанс материала удалён из сцены (ProjectStabilizer, шаг 1).
- Кислотный цвет дебрифинга: DebriefController больше не использует Color.green / Color.red, а берёт
  UIColorPalette.Success (#73CC6B) и UIColorPalette.Failure (#E67373).
- Опечатка «Вы рызбились» в CrashPanel PreparingDemo исправлена на «Вы разбились» (шаг 2).
- SampleScene и TerrainDemoScene убраны из Build Settings; файл EditorBuildSettings.asset перезаписан
  явно, потому что Unity держит его в памяти и не сохраняет сама (шаг 4).
- Автопереключение на живой борт: DroneManager.OnDroneDestroyed при потере активного дрона сам выбирает
  первый живой борт (SelectDroneByIndex), если GetAliveDronesCount() > 0; при отсутствии бортов
  currentActiveIndex сбрасывается в −1 и миссия завершается обычным провалом.
- Звук появился: создан MainMixer.mixer с группами и Expose-параметрами, на префабах дронов есть
  AudioSource + DroneMotorAudio / EWAudio, взрывам назначены префаб и звук.
- VFX появились: Assets/Prefabs/VFX/Explosion.prefab и Assets/Prefabs/Weapons/Bomb.prefab созданы и
  назначены в конфиги дронов.
- UI приведён к одной палитре (UIColorPalette + UIThemeApplicator): кислотных цветов в префабах
  SettingsPanel / MissionSelectPanel / MissionCard и сценах Bunker(main) / PreparingDemo /
  FirstMission(Forest) не осталось.
- Глобальный зелёный цвет шрифта исправлен (TmpColorFixer, шаг 9). Причина была не в компонентах, а в
  дефолтном материале шрифта: у саб-ассета «LiberationSans SDF Material» в LiberationSans SDF.asset
  параметр _FaceColor был r0.387 g0.925 b0.052 (кислотный зелёный), и все TMP-тексты проекта наследовали
  его через шейдер (faceColor = _FaceColor × цвет вершины). Теперь _FaceColor = Color.white, а
  fontSharedMaterial / m_fontColor / m_fontColor32 / m_faceColor / подчёркивание, зачёркивание и градиент
  у всех TMP_Text и legacy Text в префабах Assets/Prefabs и сценах Bunker(main), MainMenu, PreparingDemo,
  FirstMission(Forest), Boot приведены к белому или к цветам UIColorPalette (белый/серый/акцент/
  Success/Failure). Текст в меню, бункере, миссиях, паузе и дебрифинге больше не зелёный. Отчёт:
  Logs/tmp_color_fix_report.txt.
- Поток красных ошибок «type is not a supported string value» из генераторов устранён. Причина —
  AudioMixerBuilder.VerifyMixer читал поле guid у ExposedAudioParameter через SerializedProperty.stringValue,
  а это не строка, а структура GUID (SerializedPropertyType.GUID). Стек в Editor.log указывал точно:
  AudioMixerBuilder:ReadGuidString → VerifyMixer → BuildMainMixer (28 ошибок за прогоны). Теперь чтение
  идёт через type-aware DescribeProperty (ветка на каждый SerializedPropertyType: stringValue только для
  String, objectReferenceValue для ObjectReference, intValue/floatValue/enumValueIndex/colorValue для чисел,
  enum и цвета, имя типа для структур), а точные значения guid/name печатаются из самого файла микшера
  (LogExposedParametersFromFile). MissionContentBuilder.SetString дополнительно проверяет, что поле
  действительно String, и пишет понятную ошибку вместо падения Unity. Подробности — раздел 12.
- Проверено: 0 новых ошибок после исправления (все 28 старых записей в Editor.log датированы прогонами
  до фикса), полный прогон «Run Full Stabilization» проходит без ошибок.

Остаётся открытым
- Миссия 2 проверила ссылки на дронов: в текущем ассете массив availableDrones содержит только
  DroneConfig (MavicReconConfig + 4 × SmallFpvHeatConfig), битой ссылки на Mission1.asset нет;
  ссылка на Mission1 лежит в prerequisites, что корректно. Инвариант теперь проверяется скриптом.
- SerializedProperty не умеет отдавать содержимое GUID-структуры строкой: guid Expose-параметров
  читается из YAML микшера, а не из ассета через API.
- DroneDropMechanism не добавлен ни на один префаб дрона, поэтому bombPrefab в конфигах пока не используется.
- ReconDrone без EWReceiver: EWAudio на его камере не звучит.
- AudioMixer: привязка Expose-параметров проверяется только в Play Mode (в Edit Mode SetFloat возвращает
  false, потому что микшер не инстанцирован).
- Остальное из списка ниже (мультивыбор дрона, гейтинг, экономика, HUD миссии) не входило в эту итерацию.

Критичные
player.unity не входит в Build Settings

Где: ProjectSettings/EditorBuildSettings.asset

Приоритет: Высокий

Mission2 не имеет sceneName — ИСПРАВЛЕНО: Mission2 указывает FirstMission(Forest).

Где: Assets/Resources/Missions/Mission2.asset

Приоритет: Высокий (решено)

Mission3 не имеет sceneName и defaultDrone — Mission3 исключена из MissionRegistry.allMissions.

Где: Assets/Resources/Missions/Mission3.asset

Приоритет: Низкий (ассет остался в папке, но не используется)

База не соединена с миссионным flow

Где: player.unity и mission scripts

Приоритет: Высокий

Нет палаток и системы взаимодействия

Где: Assets/Scripts / player.unity

Приоритет: Высокий

Нет фургона, посадки и перехода

Где: Assets/Scripts, Assets/Scenes

Приоритет: Высокий

Средние
Смешан новый и старый Input System

Где: PlayerController, drone scripts, InputManager

MissionController.startTime не инициализируется — ИСПРАВЛЕНО: startTime = Time.time задаётся в Awake()

Где: MissionController.cs

MissionController фактически завершает только DestroyAllTargets

Где: MissionController.cs

unlockedMissionIds не заполняется

Где: ProgressService.cs

Смешение URP/HDRP/legacy assets

Где: Packages, Settings, сторонние каталоги

SettingsService не защищает чтение JSON от исключения — ИСПРАВЛЕНО: try/catch, дефолты, версия схемы,
атомарная запись (tmp + File.Replace + .bak), debounce 0.5 с.

Где: SettingsService.cs

Настройки были «пустышками» (сохранялись, но не применялись) — ИСПРАВЛЕНО поэтапно: HUD/FPS/тряска/субтитры,
звук (AudioMixer + пул AudioSource), графика URP (рантайм-клон), постобработка и РЭБ (PostFxRouter),
управление дроном (стики/газ/режим газа/гимбал), UI (режим окна и разрешение, удаление пустышек).
Что осталось не подключённым — см. CONTEXT.md, раздел 7.8.

Где: SettingsApplier.cs, Settings/*, Camera/EWReceiver.cs, Physics/DroneFlightController.cs

Идентификаторы миссий приведены к уникальным значениям

Где: Mission1 (training_mission), Mission2 (mission_1)

Низкие
Recovery-дубликаты сцен — Assets/_Recovery.

Дубли текстур персонажа с суффиксом 1 — Assets/Models/Player/Textures.

Большое количество demo-ассетов — TerrainSampleAssets, UnityTechnologies и др.

Bootstrapper.missionRegistry не используется — Bootstrapper.cs.

Отдельные заготовки не подключены к player.unity — drone/building scripts.

Старый Input.GetKey
Используется в:

Assets/Scripts/DroneTypes/DroneManager.cs

Assets/Scripts/Physics/DroneCollision.cs

Assets/Scripts/Physics/DroneFlightController.cs

Assets/Scripts/Physics/DropDroneMechanism.cs

Assets/Scripts/Camera/DroneCameraModule.cs

Assets/Scripts/Camera/FPVZoom.cs

Assets/Scripts/Camera/FPVCameraShake.cs

Assets/Scripts/Camera/NIghtVisionController.cs

Assets/Scripts/Camera/TargetLockManager.cs

PlayerController использует UnityEngine.InputSystem, но не action asset, а Keyboard.current и Mouse.current

Бункер (Bunker(main)) — текущий статус
Bunker07Anim не имеет собственных коллайдеров (импорт addColliders = 0). Физическая оболочка
Collision_SHELL (COL_Floor / COL_Walls / COL_Ceiling) и коллайдеры мебели/оборудования созданы —
игрок больше не проваливается. Мелкий мусор (Paper*, Cassete*, Cables, Keyboard*, мыши) намеренно
оставлен без коллайдеров.

Интерактивный терминал Interact_OperatorTerminal создан на мониторе (layer Interactable,
MissionBoardInteractable). InteractionCanvas и EventSystem перенесены из player.unity,
ссылки uiController назначены — взаимодействие работает.

Осталось: в сцене нет невидимых границ Bounds_* (они есть только в player.unity) — периметр
снаружи бункера пока не ограничен. Коллайдеры пульта (ControlPanel_Blocker) и серверной станции
(ComputerNCamGEo_Blocker) собраны по AABB объединённых мешей и могут потребовать ручного дробления
через Edit Collider.

11. Мёртвый код и неподключённые системы
Не обязательно мёртвый код
Могут использоваться в demo-сценах или префабах:

drone physics;

drone HUD;

VehicleSmartAI;

VehiclePatrol;

DestructibleWallZone;

EW;

ShahedFlight;

InterceptorCollision.

Точно не подключено к основной FPS-базе
В player.unity не обнаружены:

DroneManager;

MissionController;

EWReceiver;

drone HUD;

MissionSelectScreen;

tent interaction;

van interaction.

Эти системы корректнее считать отдельным прототипом или неподключёнными подсистемами.

12. Инструменты стабилизации и единая палитра (Editor)
Каталог: Assets/Scripts/Editor/. Все инструменты запускаются из меню Tools/DroneSimulator,
не требуют ручной работы в инспекторе и пишут подробные логи в консоль (и в Logs/Editor.log).

StabilizationMaster.cs — «Run Full Stabilization»: одна кнопка, последовательно вызывает шаги 1–9
(см. ниже). Порядок важен: сначала данные и ассеты, затем палитра UI, в конце — глобальный белый
цвет шрифта TMP (шаг 9), чтобы палитра и починка шрифта не перетирали друг друга.

ProjectStabilizer.cs — четыре пункта меню Stabilization/:
1) Reset TMP Font Materials — проходит по сценам PreparingDemo, FirstMission(Forest), Bunker(main),
   у каждого TMP_Text сравнивает fontSharedMaterial с font.material и, если отличается, ставит дефолтный
   материал шрифта, обнуляет m_fontMaterial и массивы m_fontSharedMaterials / m_fontMaterials
   (делается через SerializedObject, чтобы сцена осталась «чистой»), затем удаляет из сцены осиротевший
   инстанс материала (проверяя, что на него больше нет ссылок);
2) Fix Crash Panel Typo — заменяет «рызбились» на «разбились» во всех TMP_Text и legacy Text сцен;
3) Validate Mission Drone Links — проверяет массивы availableDrones и поле defaultDrone у Mission1/
   Mission2/Mission3: оставляет только ссылки на DroneConfig, чинит defaultDrone при необходимости;
4) Clean Build Settings — убирает SampleScene и TerrainDemoScene и явно перезаписывает
   ProjectSettings/EditorBuildSettings.asset (сохраняя прочие поля файла).

ImpactVfxBuilder.cs — «Impact/1. Build Explosion And Bomb»: создаёт Assets/Art/VFX (материалы частиц
на Sprites/Default и две процедурные PNG-текстуры), Assets/Prefabs/VFX/Explosion.prefab,
Assets/Prefabs/Weapons/Bomb.prefab, Assets/Audio/SFX/Explosion.wav (WAV пишется вручную, затем
AudioImporter с forceToMono и Vorbis) и назначает explosionPrefab / bombPrefab / explosionSound во все
конфиги дронов; попутно назначает explosionVFX у DroppedBomb в префабах.

AudioMixerBuilder.cs — «Impact/3. Build Audio Mixer» и «Impact/4. Verify Audio Mixer». Микшер создаётся
через внутренний API Unity (UnityEditor.Audio.AudioMixerController, доступ по рефлексии):
CreateMixerControllerAtPath → CreateNewGroup + AddChildToParent → SetValueForVolume в TargetSnapshot →
exposedParameters (GUID берётся из AudioMixerGroupController.GetGUIDForVolume для каждой группы) →
правка блока m_AudioMixerGroupViews в YAML микшера. Verify проверяет загрузку Resources/Audio/MainMixer,
наличие 7 групп и записанных Expose-параметров (SetFloat в Edit Mode не проверяется: параметры
привязываются только при инстанцировании микшера в Play Mode).

Диагностика SerializedProperty (исправлено). Раньше VerifyMixer печатал guid каждого Expose-параметра
через SerializedProperty.stringValue, но поле guid у ExposedAudioParameter — это структура GUID
(SerializedPropertyType.GUID), поэтому Unity писала в консоль красную ошибку
«type is not a supported string value» (28 записей за прогоны генераторов). Сейчас:
- DescribeProperty(SerializedProperty) — единая type-aware замена stringValue: String → stringValue,
  ObjectReference → objectReferenceValue.name, Integer / ArraySize / LayerMask / Character → intValue,
  Float → floatValue, Boolean → boolValue, Color → colorValue, Enum → enumValueIndex,
  Vector2/3/4, Rect, Bounds → соответствующие поля, Generic → «struct:<type>», прочее → имя типа
  (для GUID выводится «GUID»); ни одна ветка не вызывает неподдерживаемый геттер;
- LogExposedParametersFromFile() — читает блок m_ExposedParameters прямо из
  Assets/Resources/Audio/MainMixer.mixer, поэтому фактические guid и имена видны в логе без обращений
  к SerializedProperty по структуре.
Правило для всех будущих Editor-скриптов проекта: перед чтением/записью значения SerializedProperty
проверять property.propertyType (или пользоваться DescribeProperty), а stringValue применять только к
SerializedPropertyType.String. MissionContentBuilder.SetString теперь явно проверяет тип поля.
Тот же набор исправлений подтверждён прогоном «Run Full Stabilization»: новых ошибок в консоли нет.

DroneAudioBinder.cs — «Impact/2. Attach Drone Audio Sources»: для всех префабов в Assets/Prefabs/Drones/
добавляет на корень AudioSource (loop, spatialBlend 1, DroneMotors) + DroneMotorAudio, на первую камеру —
AudioSource (spatialBlend 0, EW) + EWAudio, назначает ссылки motors / flightController / receiver.

UIThemeApplicator.cs — «UI/1. Apply Terminal Palette» и «UI/2. Open Palette Report»: применяет палитру
UIColorPalette к префабам SettingsPanel / MissionSelectPanel / MissionCard и сценам Bunker(main) /
PreparingDemo / FirstMission(Forest). Правила: затемнение (overlay/scrim/completed) → Overlay; шапка →
PanelHeader; панель/карточка/окно → PanelBackground (вложенная → PanelSurface); плашка кнопки
(Button.targetGraphic) → Accent + ColorBlock; арт (preview/check/icon/lock/image/knob) → белый;
разделитель → Divider или SemanticiseSurface; прочие кислотные цвета → SemanticiseSurface.
Дополнительно правит сериализованные цвета MissionDetailsPanel, SettingsPanel, SettingsListBuilder.
Полный отчёт по каждому элементу пишется в Logs/ui_theme_report.txt.

TmpColorFixer.cs — «Text/1. Fix TMP Green Text» и «Text/2. Open TMP Color Report»: глобальная починка
цвета текста, из-за которой раньше весь текст в игре (меню, бункер, миссии, пауза, дебрифинг) выглядел
ядовито-зелёным. Причина была не в компонентах, а в дефолтном материале шрифта: у встроенного
саб-ассета «LiberationSans SDF Material» (файл Assets/UnityTechnologies/ParticlePack/TextMesh Pro/
Resources/Fonts & Materials/LiberationSans SDF.asset, guid 8f586378b4e144a9851e7b34d9b748ee,
fileID 2180264) параметр _FaceColor был равен r0.387 g0.925 b0.052, а шейдер TMP_SDF считает
faceColor = _FaceColor × цвет вершины, поэтому белый цвет вершины давал кислотно-зелёный текст.
Скрипт:
1) обходит все TMP_FontAsset (включая встроенные саб-ассеты материалов через LoadAllAssetsAtPath) и все
   отдельные материалы TMP-шейдеров и принудительно ставит _FaceColor = Color.white (проверка —
   material.HasProperty("_FaceColor"), поэтому попадают в том числе инстансы и пресеты Outline/Drop Shadow);
2) проходит по всем префабам Assets/Prefabs и сценам Bunker(main), MainMenu, PreparingDemo,
   FirstMission(Forest), Boot (открытие/сохранение через EditorSceneManager, префабы — через
   LoadPrefabContents / SaveAsPrefabAsset) и у каждого TMP_Text / TextMeshPro / TextMeshProUGUI
   проверяет fontSharedMaterial (зелёный _FaceColor → белый) и сериализованные поля цвета;
3) поля m_fontColor, m_fontColor32, m_faceColor, m_underlineColor, m_strikethroughColor и четыре угла
   m_fontColorGradient правятся через SerializedObject: кислотный зелёный (UIColorPalette.IsAcidic + зелёный
   оттенок) заменяется на UIColorPalette.TextPrimary, для семантических имён/текстов (success/completed/
   «выполн»/«успех») — на UIColorPalette.Success, для fail/locked — на UIColorPalette.Failure; альфа
   исходного цвета сохраняется, цвета палитры (Success/Failure/Accent/TextPrimary/TextSecondary) считаются
   целевыми и повторно не перекрашиваются, поэтому повторный запуск ничего не меняет;
4) m_fontColor32 синхронизируется из m_fontColor: это кэш, который TMP пересчитывает при генерации меша
   (TextMeshProUGUI.GenerateTextMesh: m_fontColor32 = m_fontColor), поэтому в сценах оставались устаревшие
   значения (например, тёмно-серый 0xFF323232 у «ЗАКРЫТЬ» в бункере и кислотный m_faceColor у
   Canvas/InstructionText в PreparingDemo) — после правки сериализованные данные совпадают с тем, что
   реально рисуется;
5) сохраняет только изменённые префабы/сцены, пишет отчёт Logs/tmp_color_fix_report.txt (MAT-FIX/MAT-KEEP
   по материалам, TMP-MAT/TMP-COL/TMP-FLD/TMP-GRD/UI-COL по компонентам, GREEN-KEEP для зелёных оттенков
   вне кислотного диапазона) и возвращает редактор в ранее открытую сцену.
Play Mode скрипт не трогает: при активном Play Mode он пишет warning и выходит, чтобы не сохранять
сцены во время игры. Скрипты палитры (UIThemeApplicator, UIColorPalette) не изменялись.

UIColorPalette.cs (Assets/Scripts/UI/) — единственный источник цветов интерфейса. Значения:
PanelBackground #1E2024 (0.90), PanelSurface #2A2D33 (0.90), PanelHeader #17191C (0.95),
Overlay #000000 (0.62), Accent #D94A4A, AccentHover #E46363, AccentPressed #A93737,
Success #73CC6B, Failure #E67373, TextPrimary #FFFFFF, TextSecondary #A0A5B0, Divider #FFFFFF (0.16).
Хелперы: FromHex, WithAlpha, Lighten, Darken, IsAcidic, SemanticiseSurface, SemanticiseText.
Потребители: DebriefController (цвет заголовка результата), все перечисленные префабы и сцены.

См. также: CONTEXT.md, INDEX.md.