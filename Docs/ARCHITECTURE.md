
=== ФАЙЛ 2: ARCHITECTURE.md ===

```markdown
# Архитектура и карта проекта DroneSimulator

Этот файл — часть документации проекта DroneSimulator.  
Остальные файлы: CONTEXT.md, ARCHITECTURE.md, INDEX.md.

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
├── Interact_OperatorTerminal (layer Interactable, BoxCollider Is Trigger = off, MissionBoardInteractable)
├── InteractionCanvas     (перенесён из player.unity: Canvas + InteractionUIController + Hint/ShopWindow/MissionBoardWindow)
├── EventSystem           (перенесён из player.unity: EventSystem + InputSystemUIInputModule)
└── Player
    ├── CharacterController
    ├── PlayerController
    ├── PlayerInteractionDetector   (uiController → InteractionCanvas/InteractionUIController)
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

ProgressService

Файл:

Assets/Scripts/Data/ProgressService.cs

Сохраняет:

завершённые миссии;
разблокированные миссии;
общий счёт.
Файл сохранения:

Application.persistentDataPath/progress.json
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
  ├── MissionRegistry
  ├── MissionSelectScreen
  └── SettingsScreen
          ↓
MissionSelectScreen.Populate(.)
          ↓
ProgressService.IsUnlocked(.)
          ↓
MissionCardView.Bind(.)
          ↓
клик по карточке
          ↓
MainMenuController.StartMission(.)
  ├── MissionContext.Begin(.)
  ├── GameStateMachine → Loading
  └── SceneLoader.LoadAsync(mission.sceneName)
          ↓
миссионная сцена
          ↓
MissionController.Awake()
  └── DroneManager.SelectByConfig(mission.defaultDrone)

Текущий фактический статус:

Mission1 указывает SampleScene;
Mission2 и Mission3 не имеют sceneName;
player.unity отсутствует в Build Settings;
debrief UI не подключён;
возврат на базу не собран.
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

5. Скрипты по категориям
5.1 Player
Assets/Scripts/Player/PlayerController.cs

Класс: PlayerController

Назначение: FPS-движение, мышь, гравитация, Animator

Зависимости: CharacterController, Animator, новый низкоуровневый Input System

Статус: подключён в player.unity

Поля: speed, gravity, mouseSensitivity, cameraTransform, animator, isSitting (при посадке
блокируются движение и гравитация; вращение камеры мышью остаётся доступным)

Методы: Start, Update, SetSitting, SetTyping

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

Зависимости: старый Input.GetAxis, DroneConfig

Статус: дроновый прототип

Camera/FPVZoom.cs

Класс: FPVZoom

Назначение: переключение FOV

Зависимости: старый Input.GetKeyDown, DroneConfig

Статус: дроновый прототип

Camera/IntersectorHUD.cs

Класс: InterceptorHUD

Назначение: HUD перехватчика и упреждение

Зависимости: Rigidbody, DroneCameraModule

Статус: дроновый прототип

Camera/NIghtVisionController.cs

Класс: NightVisionController

Назначение: ПНВ, IR Light, ColorAdjustments

Зависимости: старый Input.GetKeyDown, URP Volume

Статус: дроновый прототип

Camera/ReconHUD.cs

Класс: ReconHUD

Назначение: LRF, рамка цели, разведывательный HUD

Зависимости: TargetLockManager, TargetObject

Статус: дроновый прототип

Camera/StrikeHUD.cs

Класс: StrikeHUD

Назначение: ударный HUD и предупреждение РЭБ

Зависимости: DroneCameraModule, EWReceiver

Статус: дроновый прототип

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

Зависимости: Unity Instantiate/AudioSource

Статус: используется

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

Назначение: асинхронная загрузка сцен

Зависимости: SceneManager

Статус: MainMenu/Bootstrapper

5.4 Data / Missions
Data/MissionContext.cs

Класс: MissionContext

Назначение: статический контекст миссии

Зависимости: MissionDefinition, DroneConfig

Статус: используется меню/миссиями

Data/MissionController.cs

Класс: MissionController

Назначение: цели, таймер, Complete/Fail

Зависимости: DroneManager, GameEvents, ProgressService

Статус: не подключён к player.unity

Data/MissionDefinition.cs

Классы: MissionDefinition, MissionRegistry

Назначение: данные миссий

Зависимости: DroneConfig, ScriptableObject

Статус: assets в Resources

Data/ProgressService.cs

Классы: ProgressService, SaveData

Назначение: JSON-сохранение прогресса

Зависимости: JsonUtility, IO

Статус: вызывается UI/миссиями

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

Назначение: переключение пяти слотов дронов

Зависимости: старый Input.GetKeyDown, DroneConfig

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

Зависимости: URP Volume, DroneConfig

Статус: дроновые сцены

5.7 MainMenu / UI
MainMenu/GameSession.cs

Класс: GameSession

Назначение: singleton выбранной конфигурации

Зависимости: DroneConfig

Статус: заготовка / частично используется

MainMenu/MainMenuController.cs

Класс: MainMenuController

Назначение: навигация меню и запуск миссии

Зависимости: FSM, MissionContext, SceneLoader

Статус: MainMenu.unity

MainMenu/MissionCardView.cs

Класс: MissionCardView

Назначение: карточка миссии

Зависимости: UI, TMP, MissionDefinition

Статус: Mission UI

MainMenu/MissionSelectScreen.cs

Класс: MissionSelectScreen

Назначение: генерация карточек

Зависимости: ProgressService, MissionRegistry

Статус: Mission UI

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

Зависимости: старый Input.GetKey, DroneMotors, EWReceiver, PID

Статус: дроны

Physics/DroneMotors.cs

Класс: DroneMotors

Назначение: тяга четырёх моторов

Зависимости: Rigidbody

Статус: дроны

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

HangarInteractable.cs — IInteractable, открывает магазин, промпт «Магазин [E]».

MissionBoardInteractable.cs — IInteractable, открывает карту миссий, промпт «Карта миссий [E]».

ChairInteractable.cs — IInteractable, посадка за компьютер (телепорт в sitPosition,
отключение коллайдера стула chairCollider и полное отключение CharacterController на время
сидения, SetSitting(true) + блокировка движения через isSitting), вставание по Escape с
телепортом в standPosition и обратным включением коллайдера и CharacterController.

6. Сцены
Всего найдено 15 сцен.

Assets/Scenes/Boot.unity

Назначение: старт и Bootstrapper

Статус: основная цепочка

Assets/Scenes/MainMenu.unity

Назначение: меню, миссии, настройки

Статус: основной UI-прототип

Assets/Scenes/SampleScene.unity

Назначение: тестовая миссия

Статус: используется Mission1

Assets/Scenes/player.unity

Назначение: FPS-база

Статус: основная рабочая база, НЕ в Build Settings

Assets/Scenes/Bunker(main).unity

Назначение: новая FPS-локация (военный бункер Bunker07Anim)

Статус: в настройке — физика, коллайдеры, интерактивный терминал; НЕ в Build Settings

Assets/Scenes/FunctionalUI-Demo.unity

Назначение: UI demo

Статус: демонстрационная

Assets/Scenes/TerrainDemoScene.unity

Назначение: terrain и drone demo

Статус: прототип

Assets/_Recovery/0.unity — резерв terrain-сцены, recovery.
Assets/_Recovery/0 (1).unity — резерв terrain-сцены, recovery.
Assets/_Recovery/0 (2).unity — резерв terrain-сцены, recovery.

ALP_Assets/.../DemoScene.unity — demo дома, сторонняя.
S-Mine 35/Scenes/Demo.unity — demo мины, сторонняя.
UnityTechnologies/.../Main Scene with Legacy Particles.unity — demo эффектов, сторонняя.
Volklight/BTR/DemoScene.unity — demo БТР, сторонняя.
ZIL130_MilitaryTruck/Scenes/SampleScene.unity — demo грузовика, сторонняя.

Build Settings
Включены:

Assets/Scenes/Boot.unity

Assets/Scenes/MainMenu.unity

Assets/Scenes/SampleScene.unity

Не включены:

Assets/Scenes/player.unity

Assets/Scenes/TerrainDemoScene.unity

7. Игровые префабы
Дроны
Assets/Prefabs/Drones/GimbalDrone.prefab

Assets/Prefabs/Drones/ReconDrone.prefab

Assets/Prefabs/Drones/StrikeDrone.prefab

Содержат различные комбинации: Rigidbody, DroneFlightController, DroneMotors, DroneCollision, DroneCameraModule, EWReceiver, FPVZoom, HUD, Target Detector/Lock, Drop Mechanism, Interceptor Collision.

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

Параметры: Speed, Direction, IsGrounded, IsSitting (Bool), IsTyping (Bool).

Состояния: Idle, Walk, WalkBackward, SitDown, SitIdle, SitTyping (состояние Run удалено — бег отключён из-за малых размеров бункера).

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

10. Известные проблемы
Критичные
player.unity не входит в Build Settings

Где: ProjectSettings/EditorBuildSettings.asset

Приоритет: Высокий

Mission2 не имеет sceneName

Где: Assets/Resources/Missions/Mission2.asset

Приоритет: Высокий

Mission3 не имеет sceneName и defaultDrone

Где: Assets/Resources/Missions/Mission3.asset

Приоритет: Высокий

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

SceneLoader.ShowLoading/HideLoading не вызываются

Где: SceneLoader.cs

MissionController.startTime не инициализируется

Где: MissionController.cs

MissionController фактически завершает только DestroyAllTargets

Где: MissionController.cs

unlockedMissionIds не заполняется

Где: ProgressService.cs

Смешение URP/HDRP/legacy assets

Где: Packages, Settings, сторонние каталоги

SettingsService не защищает чтение JSON от исключения

Где: SettingsService.cs

Часть миссий содержит пустые идентификаторы

Где: Mission2/Mission3 assets

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

См. также: CONTEXT.md, INDEX.md.