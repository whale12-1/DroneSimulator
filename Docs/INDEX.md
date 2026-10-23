Индекс проекта DroneSimulator
Этот файл — часть документации проекта DroneSimulator.
Остальные файлы: CONTEXT.md, ARCHITECTURE.md, INDEX.md.

Быстрый поиск «где что искать»
Персонаж и игрок
Скрипт игрока → Assets/Scripts/Player/PlayerController.cs → класс PlayerController

Объект игрока → Assets/Scenes/player.unity → Player

CharacterController → Assets/Scenes/player.unity → Player

FPS-камера → Assets/Scenes/player.unity → Player/CameraPivot/Main Camera

Модель персонажа → Assets/Models/Player/Swat.fbx

Анимации персонажа → Assets/Models/Player/Animations/

Animator Controller → Assets/Models/Player/PlayerAnimator.controller

Параметры Animator → Speed, Direction, IsGrounded → PlayerAnimator.controller

Взаимодействие и UI
Скрипт взаимодействия → Assets/Scripts/Interaction/PlayerInteractionDetector.cs
Интерфейс взаимодействия → Assets/Scripts/Interaction/IInteractable.cs
UI взаимодействия (магазин/карта) → Assets/Scripts/Interaction/InteractionUIController.cs
Интерактивный объект «Магазин» → Assets/Scripts/Interaction/HangarInteractable.cs
Интерактивный объект «Карта миссий» → Assets/Scripts/Interaction/MissionBoardInteractable.cs

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

Камера дрона → Assets/Scripts/Camera/DroneCameraModule.cs

Зум дрона → Assets/Scripts/Camera/FPVZoom.cs

Тряска FPV-камеры → Assets/Scripts/Camera/FPVCameraShake.cs

Ночное видение → Assets/Scripts/Camera/NIghtVisionController.cs

Поиск целей → Assets/Scripts/Camera/TargetDetector.cs

Захват цели → Assets/Scripts/Camera/TargetLockManager.cs

Миссии и сохранения
Описания миссий → Assets/Resources/Missions/

Реестр миссий → Assets/Resources/MissionRegistry.asset

Класс описания миссии → Assets/Scripts/Data/MissionDefinition.cs → MissionDefinition

Контроллер миссии → Assets/Scripts/Data/MissionController.cs

Контекст выбранной миссии → Assets/Scripts/Data/MissionContext.cs

Сохранение прогресса → Assets/Scripts/Data/ProgressService.cs

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

Core / сервисы
Общие события → Assets/Scripts/Core/GameEvents.cs

Машина состояний → Assets/Scripts/Core/GameStateMachine.cs

Загрузка сцен → Assets/Scripts/Core/SceneLoader.cs

Стартовый Bootstrapper → Assets/Scripts/Core/Bootstrapper.cs

Общий контракт урона → Assets/Scripts/Core/DamageSystem.cs

Взрывы и splash damage → Assets/Scripts/Core/ExplosionService.cs

VFX и звуки → Assets/Scripts/Core/EffectService.cs

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

Тестовая миссионная сцена → Assets/Scenes/SampleScene.unity

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

Чего нет (по концепции)
Палатки → явный путь не найден

Фургон → явный путь не найден

Система посадки в транспорт → явный класс/путь не найден

Кат-сцена перехода → явный игровой путь не найден

См. также: CONTEXT.md, ARCHITECTURE.md.