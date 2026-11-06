Индекс проекта DroneSimulator
Этот файл — часть документации проекта DroneSimulator.
Остальные файлы: CONTEXT.md, ARCHITECTURE.md, INDEX.md, SETTINGS_DESIGN.md.

Быстрый поиск «где что искать»
Персонаж и игрок
Скрипт игрока → Assets/Scripts/Player/PlayerController.cs → класс PlayerController

Объект игрока → Assets/Scenes/player.unity → Player

CharacterController → Assets/Scenes/player.unity → Player

FPS-камера → Assets/Scenes/player.unity → Player/CameraPivot/Main Camera

Модель персонажа → Assets/Models/Player/Swat.fbx

Анимации персонажа → Assets/Models/Player/Animations/

Animator Controller → Assets/Models/Player/PlayerAnimator.controller

Параметры Animator → Speed, Direction, IsGrounded, IsSitting, IsTyping → PlayerAnimator.controller

Взаимодействие и UI
Скрипт взаимодействия → Assets/Scripts/Interaction/PlayerInteractionDetector.cs
Интерфейс взаимодействия → Assets/Scripts/Interaction/IInteractable.cs
UI взаимодействия (магазин/карта) → Assets/Scripts/Interaction/InteractionUIController.cs
Контроллер выбора миссий на компьютере → Assets/Scripts/Interaction/MissionMenuController.cs
Контроллер рабочего стола ComputerOS (закрытие оставляет игрока сидеть; вставание по Пробелу) → Assets/Scripts/Interaction/ComputerUIController.cs
Интерактивный объект «Магазин» → Assets/Scripts/Interaction/HangarInteractable.cs
Интерактивный объект «Карта миссий» → Assets/Scripts/Interaction/MissionBoardInteractable.cs
Интерактивный объект «Стул» (сесть за компьютер по E, встать по Пробелу, публичный StandUp) → Assets/Scripts/Interaction/ChairInteractable.cs
Восстановление состояния бункера после миссии → Assets/Scripts/Interaction/BunkerStateRestorer.cs
Меню паузы и окно крушения в миссиях → Assets/Scripts/Interaction/MissionUIManager.cs (объект UIManager в сценах PreparingDemo и FirstMission(Forest))

Заготовка Input-взаимодействия → Assets/Input/InputSystem_Actions.inputactions → Player/Interact

UI главного меню → Assets/Scenes/MainMenu.unity

Контроллер главного меню → Assets/Scripts/MainMenu/MainMenuController.cs

UI выбора миссии → Assets/Scripts/MainMenu/MissionSelectScreen.cs

Карточка миссии → Assets/Prefabs/UI/MissionCard.prefab

HUD дронов
HUD разведывательного дрона → Assets/Scripts/Camera/ReconHUD.cs

HUD ударного дрона → Assets/Scripts/Camera/StrikeHUD.cs

HUD сброса бомб → Assets/Scripts/Camera/DropHUD.cs

HUD перехватчика → Assets/Scripts/Camera/IntersectorHUD.cs

Стиль HUD (масштаб, прозрачность, прицел, линии, рамка захвата) → Assets/Scripts/Settings/HudStyleService.cs

Счётчик FPS → Assets/Scripts/UI/FpsCounterHud.cs

Камера дрона → Assets/Scripts/Camera/DroneCameraModule.cs

Зум дрона → Assets/Scripts/Camera/FPVZoom.cs

Тряска FPV-камеры → Assets/Scripts/Camera/FPVCameraShake.cs

Ночное видение → Assets/Scripts/Camera/NIghtVisionController.cs

Поиск целей → Assets/Scripts/Camera/TargetDetector.cs

Захват цели → Assets/Scripts/Camera/TargetLockManager.cs

Миссии и сохранения
Описания миссий → Assets/Resources/Missions/

Тренировочная миссия → Assets/Resources/Missions/Mission1.asset (id: training_mission, сцена PreparingDemo)

Первая миссия → Assets/Resources/Missions/Mission2.asset (id: mission_1, сцена FirstMission(Forest))

Реестр миссий → Assets/Resources/MissionRegistry.asset

Класс описания миссии → Assets/Scripts/Data/MissionDefinition.cs → MissionDefinition

Контроллер миссии → Assets/Scripts/Data/MissionController.cs

Контекст выбранной миссии → Assets/Scripts/Data/MissionContext.cs

Сохранение прогресса → Assets/Scripts/Data/ProgressService.cs

Контроллер дебрифинга → Assets/Scripts/Data/DebriefController.cs

Объект MissionManager (MissionController + DebriefController) → Assets/Scenes/PreparingDemo.unity → MissionManager и Assets/Scenes/FirstMission(Forest).unity → MissionManager

UI дебрифинга → Assets/Scenes/PreparingDemo.unity → Canvas/DebriefPanel (DebriefTitle, DebriefStats, ReturnButton) и Assets/Scenes/FirstMission(Forest).unity → Canvas/DebriefPanel (DebriefTitle, DebriefStats, ReturnButton)

Камера-заглушка дебрифинга → Assets/Scenes/PreparingDemo.unity → DebriefCamera и Assets/Scenes/FirstMission(Forest).unity → DebriefCamera

EventSystem (обработка кликов дебрифинга) → Assets/Scenes/FirstMission(Forest).unity → EventSystem

Дроны
Профили дронов → Assets/Settings/DroneConfigs/

Пример профиля дрона → Assets/Settings/DroneConfigs/SmallFpvHeatConfig.asset

Класс профиля дрона → Assets/Scripts/DroneTypes/DroneConfig.cs

Префабы дронов → Assets/Prefabs/Drones/

Разведывательный дрон → Assets/Prefabs/Drones/ReconDrone.prefab

Дрон с гимбалом → Assets/Prefabs/Drones/GimbalDrone.prefab

Ударный дрон → Assets/Prefabs/Drones/StrikeDrone.prefab

Менеджер дронов → Assets/Scripts/DroneTypes/DroneManager.cs

Физика полёта → Assets/Scripts/Physics/DroneFlightController.cs

Моторы дрона → Assets/Scripts/Physics/DroneMotors.cs

PID-регулятор → Assets/Scripts/Physics/PIDController.cs

Столкновения дрона → Assets/Scripts/Physics/DroneCollision.cs

Перехватчик → Assets/Scripts/Physics/IntersectorCollision.cs

Сброс бомб → Assets/Scripts/Physics/DropDroneMechanism.cs

Сброшенная бомба → Assets/Scripts/Physics/DroppedBomb.cs

Вражеский Shahed → Assets/Scripts/DroneTypes/DroneFlightScript.cs → класс ShahedFlight

Здоровье вражеского дрона → Assets/Scripts/DroneTypes/DroneHealth.cs → класс EnemyDroneHealth

РЭБ (EW)
Источник РЭБ → Assets/Scripts/EW/EWEmiter.cs → класс EWEmitter

Приёмник РЭБ → Assets/Scripts/EW/EWReceiver.cs

Сервис РЭБ → Assets/Scripts/Core/EWService.cs

Владелец Volume и постобработки (РЭБ + ПНВ + Motion Blur) → Assets/Scripts/Settings/PostFxRouter.cs

Звук помех → Assets/Scripts/Audio/EWAudio.cs

Core / сервисы
Общие события → Assets/Scripts/Core/GameEvents.cs

Машина состояний → Assets/Scripts/Core/GameStateMachine.cs

Загрузка сцен → Assets/Scripts/Core/SceneLoader.cs

Стартовый Bootstrapper → Assets/Scripts/Core/Bootstrapper.cs

Общий контракт урона → Assets/Scripts/Core/DamageSystem.cs

Взрывы и splash damage → Assets/Scripts/Core/ExplosionService.cs

VFX и звуки → Assets/Scripts/Core/EffectService.cs (пул AudioSource, группа SFX)

Звук моторов дрона → Assets/Scripts/Audio/DroneMotorAudio.cs

Звук помех → Assets/Scripts/Audio/EWAudio.cs

Настройки (Settings)
Модель настроек → Assets/Scripts/Data/SettingsData.cs

Сервис настроек (settings.json, миграция схемы, Apply) → Assets/Scripts/MainMenu/SettingsService.cs

Точка входа применения (экран, качество, графика, камеры) → Assets/Scripts/Data/SettingsApplier.cs

Панель настроек (4 вкладки, кнопка «Сброс») → Assets/Scripts/UI/SettingsPanel.cs

Конструктор строк панели (Slider / Toggle / Dropdown) → Assets/Scripts/UI/SettingsListBuilder.cs

Применение «тяжёлых» настроек по отпусканию слайдера → Assets/Scripts/UI/SliderReleaseNotifier.cs

Стиль HUD → Assets/Scripts/Settings/HudStyleService.cs

Область применения (бункер/миссия) → Assets/Scripts/Settings/SettingsScope.cs

Графика URP (рантайм-клон ассета) → Assets/Scripts/Settings/GraphicsApplier.cs

Камеры (far clip/тени/AA/постобработка) → Assets/Scripts/Settings/CameraApplier.cs

Постобработка и РЭБ (владелец Volume) → Assets/Scripts/Settings/PostFxRouter.cs

Звуковая шина AudioMixer → Assets/Scripts/Settings/AudioBus.cs

Применение громкостей → Assets/Scripts/Settings/AudioApplier.cs

Файл сохранения настроек → Application.persistentDataPath/settings.json (+ .bak)

Ассет микшера (создаётся вручную) → Assets/Resources/Audio/MainMixer.mixer

Префаб панели настроек → Assets/Prefabs/UI/SettingsPanel.prefab

Панель настроек в главном меню → Assets/Scenes/MainMenu.unity (инстанс префаба SettingsPanel)

Панель настроек в бункере → Assets/Scenes/Bunker(main).unity → InteractionCanvas/PausePanel/settings

Техника и AI
Техника → Assets/Prefabs/Vehicles/Btr.prefab

Здоровье техники → Assets/Scripts/TargetObjectScripts/VehicleHealth.cs

Модули техники → Assets/Scripts/TargetObjectScripts/VehicleModule.cs

AI техники → Assets/Scripts/TargetObjectScripts/VehicleSmartAI.cs

Патруль техники → Assets/Scripts/TargetObjectScripts/VehiclePatrol.cs

Попадания ParticleSystem → Assets/Scripts/TargetObjectScripts/BulletParticleCollision.cs

Цель для HUD → Assets/Scripts/Physics/TargetObject.cs

Разрушаемая стена → Assets/Scripts/Buildings/DestructibleWallZone.cs

База и границы
Карта военной базы → Assets/Scenes/player.unity

Модель военной базы → Assets/Models/Buildings/military base.fbx

Границы базы → Assets/Scenes/player.unity → Bounds_West, Bounds_East, Bounds_South, Bounds_North

Основная сцена базы → Assets/Scenes/player.unity

Новая сцена базы (бункер) → Assets/Scenes/Bunker(main).unity
Модель бункера → Assets/Models/Buildings/Base_v1/source/Bunker07Anim.fbx

Физическая оболочка бункера → Assets/Scenes/Bunker(main).unity → Collision_SHELL
Пол бункера → Assets/Scenes/Bunker(main).unity → Collision_SHELL/COL_Floor
Стены бункера → Assets/Scenes/Bunker(main).unity → Collision_SHELL/COL_Walls (COL_Wall_North/South/East/West)
Потолок бункера → Assets/Scenes/Bunker(main).unity → Collision_SHELL/COL_Ceiling

Коллайдеры дверей → Assets/Scenes/Bunker(main).unity → Blockers_Doors (Door_Blocker, Door1_Blocker)
Коллайдеры сейфов → Assets/Scenes/Bunker(main).unity → Blockers_Safe (Safe_Blocker, Safe1_Blocker, Safe2_Blocker)
Коллайдеры пульта/серверов → Assets/Scenes/Bunker(main).unity → Blockers_Console (ControlPanel_Blocker, ComputerNCamGEo_Blocker)
Коллайдеры кондиционеров → Assets/Scenes/Bunker(main).unity → Blockers_ACUnits (ACUnit_Blocker, ACUnit1_Blocker)
Коллайдеры мебели → Assets/Scenes/Bunker(main).unity → Blockers_Furniture (Chair_Blocker, Chair1_Blocker, Chair2_Blocker, CupBoard_Blocker ×5)

Терминал оператора → Assets/Scenes/Bunker(main).unity → Interact_OperatorTerminal (layer Interactable, MissionBoardInteractable)
UI взаимодействия → Assets/Scenes/Bunker(main).unity → InteractionCanvas + EventSystem (перенесены из player.unity)

Сцены
Boot-сцена → Assets/Scenes/Boot.unity

Main Menu-сцена → Assets/Scenes/MainMenu.unity

Старая тестовая миссионная сцена → Assets/Scenes/SampleScene.unity

Тренировочная миссия (сцена) → Assets/Scenes/PreparingDemo.unity (MissionManager: MissionController + DebriefController; DroneManager на GameSystems с пулом allDronesInScene; UI дебрифинга Canvas/DebriefPanel; UIManager: меню паузы и окно крушения)

Первая миссия (сцена) → Assets/Scenes/FirstMission(Forest).unity (MissionManager: MissionController + DebriefController; DroneManager на корневом объекте DroneManager с пулом allDronesInScene; UI дебрифинга Canvas/DebriefPanel; DebriefCamera; EventSystem; UIManager: меню паузы)

Альтернативная карта первой миссии (не привязана) → Assets/Scenes/FirstMission(SpizjenyiVarik).unity

Drone/Terrain demo → Assets/Scenes/TerrainDemoScene.unity

UI demo → Assets/Scenes/FunctionalUI-Demo.unity

Recovery-сцены → Assets/_Recovery/

Бункер (физика настроена) → Assets/Scenes/Bunker(main).unity

Все основные сцены → Assets/Scenes/

Input и настройки проекта
Input Actions → Assets/Input/InputSystem_Actions.inputactions

Старый Input Manager → ProjectSettings/InputManager.asset

Tags и Layers → ProjectSettings/TagManager.asset

Физика и гравитация → ProjectSettings/DynamicsManager.asset

Render Pipeline → ProjectSettings/GraphicsSettings.asset

URP settings → ProjectSettings/URPProjectSettings.asset

Quality settings → ProjectSettings/QualitySettings.asset

Название продукта → ProjectSettings/ProjectSettings.asset

Build Settings → ProjectSettings/EditorBuildSettings.asset

Пакеты проекта → Packages/manifest.json

Сторонние ассеты
Сторонний UI-пакет → Assets/Strategic Warfare UI Starter Pack/

Unity Particle Pack → Assets/UnityTechnologies/ParticlePack/

Terrain assets → Assets/TerrainSampleAssets/

Terrain URP demo → Assets/TerrainDemoScene_URP/

Дома → Assets/ALP_Assets/

BTR assets → Assets/Volklight/BTR/

ZIL truck assets → Assets/ZIL130_MilitaryTruck/

Mine asset → Assets/S-Mine 35/

Рантайм-объекты (создаются кодом, в сценах их нет)
SettingsService → DontDestroyOnLoad; создаёт Bootstrapper (Boot.unity) или SettingsPanel.Start()

PostFxRouter → DontDestroyOnLoad, «PostFxRouter» с детьми PostFX_EW / PostFX_NV / PostFX_Settings; создаёт PostFxRouter.Ensure()

EffectAudioPool → DontDestroyOnLoad, 16 AudioSource; создаёт EffectService.EnsurePool() при первом PlaySound

FpsCounterHUD → DontDestroyOnLoad; создаёт RuntimeInitializeOnLoadMethod в FpsCounterHud

IR_Spotlight → дочерний объект камеры дрона; создаёт NightVisionController.Awake(), если поле IR Spotlight пустое

URP-клон «PC_RPAsset (Runtime)» → не объект сцены; подставляется в QualitySettings.renderPipeline в Play Mode и снимается при выходе

AudioMixer → Assets/Resources/Audio/MainMixer.mixer; кэшируется AudioBus (ассет создаётся вручную)

Чего нет (по концепции)
Палатки → явный путь не найден

Фургон → явный путь не найден

Система посадки в транспорт → явный класс/путь не найден

Кат-сцена перехода → явный игровой путь не найден

См. также: CONTEXT.md, ARCHITECTURE.md, SETTINGS_DESIGN.md.