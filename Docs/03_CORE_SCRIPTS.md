# 03 — Ключевые скрипты

Каталог всех скриптов, задействованных в 5 сценах (напрямую или через префабы из этих сцен).
Формат: **скрипт** → зона ответственности → зависимости.

> Обозначения: `SO` — ScriptableObject, `static` — статический класс, `singleton` — MonoBehaviour-одиночка с `DontDestroyOnLoad`.

---

## 1. Ядро и инфраструктура (`Assets/Scripts/Core`)

| Скрипт | Ответственность | Зависит от |
|---|---|---|
| `Bootstrapper` | Точка входа: создаёт `SceneLoader` и `SettingsService`, переводит FSM в `MainMenu`, грузит `MainMenu` | `SceneLoader`, `SettingsService`, `GameStateMachine` |
| `SceneLoader` (singleton) | Асинхронная загрузка сцены, экран загрузки (`CanvasGroup`), проверка Build Settings, задержка `postLoadDelay` (1.5 с), ожидание `BunkerStateRestorer` (до 3 с) | `BunkerStateRestorer` *(протечка слоя)* |
| `GameStateMachine` (static) | FSM: `Boot/MainMenu/MissionSelect/Loading/Gameplay/Paused/Debrief`, матрица допустимых переходов, событие `StateChanged` | — |
| `GameEvents` (static) | Шина: `ExplosionOccurred`, `DroneExploded`, `DamageApplied`, `VehicleDestroyed`, `ModuleDestroyed`, `DroneSwitched` + `Raise*` + `ClearAll()` | `VehicleHealth`, `VehicleModule`, `DamageData` |
| `DamageSystem` | Структуры `DamageData` / `ExplosionEventData` + интерфейс `IDamageable` | `WarheadType`, `ModuleType` |
| `ExplosionService` (static) | Единый расчёт урона по площади: `OverlapSphere`, затухание `1 - dist/radius`, отсечение источника | `IDamageable`, `DamageData` |
| `EffectService` (static) | `Instantiate` эффектов и пул из 16 `AudioSource` (`DontDestroyOnLoad`) для одноразовых звуков | `AudioBus.Sfx` |
| `EWService` (static) | Реестр `EWEmitter`, `SampleJamAt(pos)`, `BoundaryJamIntensity` | `EWEmitter` |

**Особенности:**

* `SceneLoader` не выставляет FSM-состояние — это делают вызывающие.
* `EffectService.EnsurePool()` создаёт объект `EffectAudioPool` в `DontDestroyOnLoad`; пул живёт до конца приложения (утечки нет, но и пересоздания нет).
* `EWService.BoundaryJamIntensity` — глобальная изменяемая переменная, её каждый кадр двигает `DroneManager.CalculateBoundaryJam()`.

---

## 2. Данные, прогресс, миссии (`Assets/Scripts/Data`)

| Скрипт | Ответственность | Зависит от |
|---|---|---|
| `MissionDefinition` (SO) | Описание миссии: `id`, тексты, `sceneName`, `objectiveType`, `targetCount`, `timeLimit`, `defaultDrone`, `availableDrones`, `prerequisites`, `rewardPoints` | `DroneConfig` |
| `MissionRegistry` (SO) | Массив всех миссий; ассет в `Resources/MissionRegistry.asset` | `MissionDefinition` |
| `MissionContext` (static) | Текущая миссия и дрон, результат последней сессии (`LastResult`, `LastTimeElapsed`, `LastTargetsDestroyed`), флаг `ReturnedFromMission` | `MissionDefinition`, `DroneConfig` |
| `MissionController` | «Исполнитель» миссии: таймер, подсчёт уничтоженных целей, условия победы (`DestroyAllTargets` / `SurviveTime` / `ReachPoint`), провал при потере всех дронов, запись результата, переход в `Debrief` | `DroneManager`, `MissionContext`, `ProgressService`, `GameStateMachine`, `GameEvents`, `MissionUIManager` |
| `ProgressService` (static) | `progress.json`: записи по миссиям, очки, разблокировки, `ComputeScore`, `RecordResult`, `ResetProgress` | `MissionDefinition` |
| `SettingsData` | Модель настроек: `GraphicsSettings`, `AudioSettings`, `ControlSettings`, `GameplaySettings`, `KeyBinding` | — |
| `SettingsApplier` (static) | Применение: `ApplyInstant` (звук) и `ApplyHeavy` (экран, качество, URP, постобработка, камеры) | `AudioApplier`, `GraphicsApplier`, `CameraApplier`, `PostFxRouter` |
| `DebriefController` | Панель результата по событию `Debrief`, подсчёт очков, возврат на базу | `GameStateMachine`, `MissionContext`, `ProgressService`, `SceneLoader` |
| `TerrainDistanceBoost` | Одноразово ставит `Terrain.detailObjectDistance` в `Start()` | `Terrain` |

**Особенности:**

* `MissionController` в `Awake()` сам переводит FSM `Loading → Gameplay` — то есть именно он «закрывает» загрузку миссии.
* `MissionController.OnDroneDestroyed()` содержит жёсткую привязку к сцене: `mission.sceneName.Contains("PreparingDemo")` (`MissionController.cs:114`).
* `ProgressService` — статический, без событий: UI читает его напрямую (`MissionSelectScreen`, `MissionDetailsPanel`).
* `MissionController` умеет 3 типа цели, но `ReachPoint` в текущих миссиях не используется (`reachPointTarget` = `null`).

---

## 3. UI и меню (`MainMenu/`, `UI/`)

| Скрипт | Ответственность | Зависит от |
|---|---|---|
| `UIScreen` (abstract) | Базовый «экран»: `Show()`/`Hide()` через `CanvasGroup` (alpha, interactable, blocksRaycasts) | `CanvasGroup` |
| `SimpleUIScreen` | Пустая реализация `UIScreen` для главной панели | `UIScreen` |
| `MainMenuController` | Навигация меню: главная панель / настройки / (мертвая) панель миссий, запуск бункера, выход, диагностика | `UIScreen`, `MissionSelectScreen`, `SettingsPanel`, `MissionRegistry`, `SceneLoader`, `GameStateMachine` |
| `MissionSelectScreen` | Заполнение списка миссий карточками, выбор карточки, открытие деталей, запуск миссии | `MissionCardView`, `MissionDetailsPanel`, `MissionRegistry`, `ProgressService` |
| `MissionCardView` | Карточка миссии: превью, имя, метаданные, оверлей блокировки, бейдж выполнения, рамка выбора | `MissionDefinition`, `MissionText` |
| `MissionDetailsPanel` | Правая панель деталей: баннер, статус, награда, описание, статистика, цели, техника, кнопка «Запустить» | `MissionDefinition`, `ProgressService`, `MissionText` |
| `MissionText` (static) | Склонения/форматирование: «N целей», длительность `mm:ss`, роль дрона | `MissionDefinition`, `DroneConfig` |
| `SettingsPanel` | Панель настроек: 4 вкладки (Графика/Управление/Аудио/Геймплей), сброс, нормализация Z | `SettingsService`, `SettingsListBuilder`, `SettingsApplier` |
| `SettingsListBuilder` | Процедурная генерация строк настроек (слайдер/тумблер/дропдаун), `SliderReleaseNotifier` | `SettingDescriptor`, `SliderReleaseNotifier` |
| `SliderReleaseNotifier` | Сообщает «отпустил ползунок» для настроек, применяемых по отпусканию | `Slider` |
| `ScreenFader` | Затемнение экрана с текстом (титры глав обучения) | `CanvasGroup`, `TMP_Text` |
| `FpsCounterHud` | Автосоздаваемый `OnGUI`-счётчик FPS | `SettingsService`, `HudStyleService`, `GameStateMachine`, `SceneLoader` |

**Особенности:**

* `SettingsPanel.Awake()` вызывает `NormalizeZ()` — принудительно сбрасывает `localPosition.z` в 0 с подробным комментарием: у Overlay-канваса `GraphicRaycaster` не попадает в UI, смещённый по Z, и клики перестают работать (`SettingsPanel.cs:79-92`). Это костыль вокруг «кривого» префаба/сцены.
* `SettingsPanel.Start()` вызывает `SettingsService.EnsureInstance()` — панель может сама создать сервис, если открыта не из `Boot`.
* `MissionDetailsPanel` содержит 20+ сериализованных UI-ссылок и много логики форматирования; это самый «толстый» UI-скрипт.

---

## 4. Бункер и взаимодействия (`Interaction/`, `Player/`)

| Скрипт | Ответственность | Зависит от |
|---|---|---|
| `IInteractable` | Контракт: `GetInteractionPrompt()`, `CanInteract()`, `Interact()` | — |
| `PlayerController` | FPS-движение (WASD), мышь-обзор, гравитация, аниматор, режим «сидит», применение настроек мыши | `CharacterController`, `Animator`, `SettingsService` |
| `CameraFollowHead` | Позиционирует камеру у кости головы с офсетом | `Transform headBone` |
| `PlayerInteractionDetector` | Raycast из камеры (3 м, слой), подсказка, `E` — взаимодействие | `IInteractable`, `InteractionUIController` |
| `ChairInteractable` | Сесть/встать: телепорт, отключение `CharacterController`/коллайдера, анимация сидения, `Space` — встать; подсказка/посадка только при приближении на `maxInteractDistance` (0.8 м по горизонтали до ближайшей точки коллайдера) | `PlayerController`, `InteractionUIController` |
| `ComputerInteractable` | Вход в компьютер (только сидя); дергает `ComputerUIController.ToggleDesktop()` | `PlayerController`, `ComputerUIController` |
| `ComputerUIController` | Анимация открытия «рабочего стола» (scale 0.1→1, alpha 0→1), `timeScale = 0`, закрытие по `E`/`Esc` | `CanvasGroup`, `PlayerController`, `MissionMenuController` |
| `DesktopIcon` | Иконка «Миссии» → `MissionMenuController.OpenMissionMenu()` | `MissionMenuController` |
| `InteractionUIController` | Подсказки (центр/низ), окна «Магазин» и «Карта миссий», `timeScale`, курсор | `SettingsService` (флаг подсказок) |
| `MissionMenuController` | Открыть/закрыть меню миссий, `Populate` карточек, запуск миссии, кнопка «Назад» | `MissionRegistry`, `MissionSelectScreen`, `MissionContext`, `SceneLoader`, `GameStateMachine` |
| `BunkerPauseController` | Пауза по `Esc`, блокировка ввода, настройки, выход в главное меню | `ComputerUIController`, `MissionMenuController`, `BunkerStateRestorer`, `PlayerInteractionDetector`, `ChairInteractable`, `InteractionUIController`, `PlayerController`, `SettingsPanel` |
| `BunkerStateRestorer` | Восстановление позы после миссии: посадка, открытие ОС, меню миссий | `ChairInteractable`, `ComputerUIController`, `MissionMenuController`, `MissionContext`, `GameStateMachine` |
| `MissionUIManager` | Пауза в миссии, скрытие HUD (`IsHudHidden`), crash-панель, рестарт, возврат в бункер | `GameStateMachine`, `SceneLoader`, `MissionContext` |

**Особенности:**

* `PlayerInteractionDetector` работает **каждый кадр** (raycast + поиск `IInteractable` в родителях) — без троттлинга.
* `BunkerPauseController` — фактически «god object» бункера: держит ссылки на 10 систем и вручную управляет их `enabled`.
* `MissionUIManager.RestartMission()` **жёстко** грузит `PreparingDemo` (`MissionUIManager.cs:87`), а не текущую миссию — рестарт корректно работает только в обучении.
* `ChairInteractable` пишет 5 отладочных `Debug.Log` на каждую посадку/вставание.

---

## 5. Дроны (`DroneTypes/`, `Physics/`)

| Скрипт | Ответственность | Зависит от |
|---|---|---|
| `DroneConfig` (SO) | Профиль дрона: боевая часть (`WarheadType`, `directDamage`, `splash*`), детонация, recon-префаб, бомбы, автозахват, ПНВ, тряска, HUD-цвета, РЭБ-устойчивость, тип камеры/подвеса/зума | — |
| `DroneManager` | Пул дронов сцены (`DroneSetup{droneObject, prefab, config}`), построение `activeSlots` по `mission.availableDrones`, переключение по `1..9`, респавн (`RespawnStrikeDrone`, `RespawnStrikeDroneAt`), расчёт граничных помех | `MissionContext`, `DroneConfig`, `EWService`, `GameEvents` |
| `DroneFlightController` | Физика полёта: PID по pitch/roll/yaw, Acro (StrikeFPV) и Angle/Hover (ReconDrop), газ с кривой, аэродинамика/драг, влияние РЭБ (дрейф и провалы), армминг мотора (`W`), 4 с неуязвимости после старта | `Rigidbody`, `DroneMotors`, `EWReceiver`, `PIDController`, `SettingsService` |
| `DroneMotors` | Применение тяги 4 моторов через `AddForceAtPosition`, центрирование `centerOfMass` | `Rigidbody` |
| `DroneCollision` | Детонация: ручная (`detonateKey`), по коллизии (порог скорости/теги), прямой урон через `IDamageable`, сплэш, спавн recon-дрона после подрыва, события | `DroneConfig`, `ExplosionService`, `EffectService`, `GameEvents`, `DroneFlightController` |
| `PIDController` | Переиспользуемый PID-регулятор | — |
| `DroneHealth` (класс `EnemyDroneHealth`) | HP вражеского дрона-«Шахеда», падение/дым/взрыв при таране | `ShahedFlight`, `Rigidbody` |
| `DroneFlightScript` (класс `ShahedFlight`) | Простой полёт камикадзе к цели по прямой | `Rigidbody` |

**Особенности:**

* Вся физика полёта — «ручная»: PID считается по угловым скоростям, тяга распределяется на 4 точки; никакого `Rigidbody.AddForce`-хака с массой не осталось (`DroneMotors` применяет чистую тягу).
* `DroneFlightController` использует **старый** `Input` (`Input.GetKey`, `Input.GetAxis`) — при выключенном «Both» в Project Settings дрон перестанет управляться.
* `DroneManager.SelectStrike()` и `RespawnStrikeDrone*()` ищут слот по строке: `config.droneName.Contains("FPV")` (`DroneManager.cs:243`, `:280`).
* `DroneCollision.Explode()` сразу после подрыва спавнит **recon-дрон** (`config.reconDronePrefab`) — так реализован переход «ударный → разведчик» после потери борта.
* `EnemyDroneHealth`/`ShahedFlight` объявлены в файлах с другими именами (`DroneHealth.cs`, `DroneFlightScript.cs`) — Unity-конвенция «имя файла = имя класса» нарушена; в 5 сценах эти классы не используются.

---

## 6. Цели, техника, урон (`TargetObjectScripts/`, `EW/`, `Buildings/`)

| Скрипт | Ответственность | Зависит от |
|---|---|---|
| `TargetObject` | Маркер цели: `targetName`, `isHostile`, `TargetPosition` (центр + 0.5 м вверх) | — |
| `VehicleHealth` | `totalHealth`, приём урона (`IDamageable`), потеря хода (отключение патруля/AI, `isKinematic`), взрыв, выгорание материалов, отрыв башни при детонации БК, событие `VehicleDestroyed` | `VehicleModule`, `EWEmitter`, `GameEvents` |
| `VehicleModule` | Модуль техники: `ModuleType` (Engine/Tracks/AmmoRack/Turret/Optics/Armor/MainHull), броня `armorThickness` с учётом угла (рикошет при `dot < 0.25`), эффекты уничтожения, отрыв детали, событие `ModuleDestroyed` | `VehicleHealth`, `DamageData`, `GameEvents` |
| `VehiclePatrol` | Движение по вейпоинтам: `SmoothDamp`, ограничение скорости поворота, look-ahead, привязка к земле двумя лучами (высота + наклон), `IsFinished`, `StartRetreat(speed)` (разворот маршрута) | `Rigidbody`, `LayerMask groundLayer` |
| `VehicleSmartAI` | Выбор наиболее «опасного» дрона (расстояние + скорость сближения), поворот башни (yaw + pitch с ограничениями), стрельба частицами с баллистическим упреждением, дымовая завеса при дистанции < 35 м | `ParticleSystem`, `AudioSource`, `Rigidbody` |
| `VehicleDust` | Пыль из-под гусениц по скорости | — |
| `WheelRotator` | Вращение колёс/катков по скорости | — |
| `HimarsStrike` | Залп 4 ракет (`rocketVFX`) после завершения маршрута, затем отступление | `VehiclePatrol`, `VehicleHealth` |
| `TargetPingSystem` | Ставит маркер последней позиции цели при переключении на ударный дрон (по `TargetLockManager` или рейкасту центра), удаляет маркер при приближении < 200 м | `DroneManager`, `TargetLockManager`, `GameEvents` |
| `EWEmitter` (файл `EWEmiter.cs`) | Источник помех: `jamRadius`, `isActive`, регистрация в `EWService` | `EWService` |
| `EWReceiver` | Приём помех: `jamIntensity = max(raw, boundary) * (1 - ewResistance)`, постэффекты (chromatic/vignette/grain), джиттер кадра и FOV, процедурный «снег» и разрывы в `OnGUI` | `EWService`, `PostFxRouter`, `FPVZoom`, `SettingsService`, `MissionUIManager` |
| `EWAudio` | Шум РЭБ: громкость/питч от `jamIntensity` | `EWReceiver`, `AudioBus` |
| `DestructibleWallZone` | Пробиваемая стена: HP, включение `DepthMask`-маски, отключение коллайдера, пыль/обломки | `Collider` |
| `TrainingGate` | Кольцо полосы препятствий, статическое событие `OnGatePassed`, перекраска материала | `DroneFlightController` |

**Особенности:**

* Урон сходится в один контракт `IDamageable.ApplyDamage(in DamageData)`. `VehicleModule` дополнительно поддерживает старый `TakeDamage(raw, point, normal, dir)` «для совместимости».
* `VehicleHealth.currentHealth` — это **остаток здоровья** (`=> totalHealth`), хотя имя читается как «текущее/максимальное». `AmbushMissionDirector` и `HimarsStrike` проверяют `currentHealth <= 0`, что работает только благодаря этому совпадению (`VehicleHealth.cs:19`).
* `VehicleSmartAI` использует `Debug.DrawRay` каждый кадр (рисуется только в редакторе Scene view, но код выполняется всегда).
* `TargetPingSystem` вызывает `FindFirstObjectByType<TargetLockManager>()`, т.е. находит **любой** менеджер захвата в сцене, а не тот, что у активного дрона.

---

## 7. Камеры, HUD, оптика (`Camera/`)

Все HUD-скрипты рисуются через **`OnGUI`** (IMGUI), а не через uGUI/UI Toolkit. Единый стиль задаёт `HudStyleService` (масштаб, прозрачность, толщина линий, размер прицела, видимость рамки захвата), а «скрытость» — `MissionUIManager.IsHudHidden` + `SceneLoader.Instance.IsLoading`.

| Скрипт | Ответственность | Зависит от |
|---|---|---|
| `DroneCameraModule` | Тип камеры из `DroneConfig.cameraType`: `ReconGimbal` (2-осевой подвес с гиростабилизацией, `R/F`, `Q/E`, `C`) или `StrikeFixed` (фиксированный угол) | `DroneConfig`, `SettingsService` |
| `FPVZoom` | Плавный зум по уровням `zoomLevels`, нормализация под `graphics.droneFov`, `SetFovOffset` для джиттера РЭБ | `DroneConfig`, `SettingsService` |
| `TargetDetector` | Поиск `TargetObject` в радиусе и конусе обзора, проверка видимости рейкастом; `detectedTargets` | `DroneConfig`, `LayerMask obstacleMask` |
| `TargetLockManager` | Захват/сброс цели по клавише из конфига, выбор ближайшей к центру экрана цели в пределах `maxLockScreenRadius` | `TargetDetector`, `TargetObject`, `DroneConfig` |
| `ReconHUD` | HUD разведчика: лазерный дальномер (LRF), сетка, адаптивная рамка цели по `Renderer.bounds`, дистанция | `TargetLockManager`, `DroneCameraModule`, `FPVZoom`, `HudStyleService` |
| `StrikeHUD` | HUD ударного: перекрестие, рамка захвата, `[DRONE]` / `ARMED / warhead`, предупреждение о РЭБ | `DroneCameraModule`, `EWReceiver`, `HudStyleService` |
| `NightVisionController` | Включение `ScriptableRendererFeature` «Night Vision Feature» + ИК-спотлайт, `N` | `DroneConfig`, URP (рефлексия) |
| `FPVCameraShake` | Тряска камеры от моторов/газа (частота, амплитуды из конфига + настройки) | `DroneConfig`, `DroneMotors`, `SettingsService` |
| `DropHUD`, `IntersectorHUD` | HUD сброса бомб и перехватчика — **не используются в 5 сценах** | — |

**Особенности:**

* `ReconHUD`/`StrikeHUD` рисуют HUD через `GUI.DrawTexture` текстурой 1×1 px, созданной в `Awake()`.
* HUD-скрипты **не** проверяют паузу (`Time.timeScale`) — при `timeScale = 0` `OnGUI` продолжает рисоваться (скрытие только через `MissionUIManager.IsHudHidden`).
* `NightVisionController` критичен к имени фичи в рендерере: при переименовании/отсутствии пишет `LogError` и не работает. Плюс читает приватное поле URP (`m_RendererDataList`) — ломается при обновлении пакета.
* `DroneCameraModule`, `FPVZoom`, `TargetLockManager`, `NightVisionController` используют старый `Input`.

---

## 8. Настройки (`Settings/`)

| Скрипт | Ответственность |
|---|---|
| `SettingsService` (singleton) | Загрузка/миграция/сохранение `settings.json`, `MarkDirty()` (дебаунс 0.5 с), `ApplyAll/ApplyInstant/ApplyHeavy`, `ResetAll`, `SetMasterVolume/SetSfxVolume/SetQuality/SetFullscreen`, повторное применение при `sceneLoaded` |
| `AudioBus` (static) | `Resources.Load<AudioMixer>("Audio/MainMixer")`, группы `Master/Music/SFX/DroneMotors/EW/UI`, `SetVolume(param, linear)` с переводом в дБ |
| `AudioApplier` (static) | Применение громкостей; fallback на `AudioListener.volume` при отсутствии микшера |
| `GraphicsApplier` (static) | Runtime-клон URP-ассета: `renderScale`, тени, MSAA, soft shadows; `SettingsScope` выбирает «бункерный»/«дроновый» shadow distance |
| `CameraApplier` (static) | `farClipPlane`, `renderPostProcessing`, `renderShadows`, `antialiasing` у FPS- и дрон-камер; подписка на `GameEvents.DroneSwitched` |
| `PostFxRouter` (singleton) | Три runtime-Volume (`PostFX_EW` / `PostFX_NV` / `PostFX_Settings`), раздача эффектов, `ApplyMotionBlur` |
| `HudStyleService` (static) | Стиль HUD: `Scale`, `Transparency`, `ApplyAlpha`, `CrosshairSize`, `LineThickness`, `TargetFrameVisible`, `GUI.matrix`-масштабирование |
| `SettingsScope` (static) | `IsMission` — по наличию `DroneManager` в сцене |

**Особенности:**

* `SettingsService.SchemaVersion = 3`, а `SettingsData.version` по умолчанию `2` (`SettingsData.cs:124`) — свежесозданный объект сразу считается «старым»; миграция безвредна, но переписывает `graphics.windowMode` из `fullscreen`.
* `GraphicsApplier` использует рефлексию к `m_SoftShadowsSupported` — при обновлении URP поле может исчезнуть, и soft shadows молча перестанут применяться.
* `PostFxRouter.ApplyMotionBlur(bool enabled)` ставит `settingsMotionBlur.active = !enabled` (`PostFxRouter.cs:49`) — при включённом в настройках Motion Blur эффект **выключается**. Похоже на инверсию.

---

## 9. Аудио

| Скрипт | Ответственность |
|---|---|
| `DroneMotorAudio` | Процедурный луп моторов (4 гармоники 96 Гц + «wobble»), громкость/питч от `DroneMotors.CurrentThrust01`, `AudioBus.Motors`; клип генерируется один раз (static cache) |
| `EWAudio` | Процедурный фильтрованный белый шум, громкость/питч от `EWReceiver.jamIntensity`, `AudioBus.EW` |

---

## 10. Editor-утилиты (`Assets/Scripts/Editor`)

| Скрипт | Меню | Что делает |
|---|---|---|
| `MissionSelectPanelBuilder` | `Tools/DroneSimulator/Mission UI/Rebuild Prefabs`, `.../Apply All` | Генерирует/обновляет `MissionSelectPanel.prefab` и `MissionCard.prefab` |
| `MissionContentBuilder` | `Tools/DroneSimulator/Mission UI/Apply Mission Content` | Заполняет префаб контентом/структурой |
| `SettingsPanelPrefabBuilder` | `Tools/DroneSimulator/Create SettingsPanel Prefab` | Создаёт `SettingsPanel.prefab` |

Эти билдеры — причина, по которой сложный UI-префаб существует в «генерируемом» виде: ручные правки префаба могут быть перезаписаны повторным запуском билдера.

---

## 11. Матрица «кто кого вызывает» (важные связи)

```
Bootstrapper ──► SceneLoader, SettingsService
MainMenuController ──► SceneLoader, MissionSelectScreen, SettingsPanel, MissionRegistry
MissionMenuController ──► MissionSelectScreen ──► MissionCardView / MissionDetailsPanel ──► ProgressService
MissionMenuController ──► MissionContext.Begin ──► SceneLoader
MissionController ◄── GameEvents(VehicleDestroyed, DroneExploded)
MissionController ──► MissionContext.MarkResult ──► ProgressService.RecordResult ──► GameStateMachine.Debrief
DebriefController ◄── GameStateMachine.StateChanged ──► SceneLoader("Bunker(main)")
TrainingManager ◄── GameEvents(VehicleDestroyed, ModuleDestroyed, DroneSwitched, DroneExploded), TrainingGate.OnGatePassed
TrainingManager ──► DroneManager.RespawnStrikeDrone(At), ScreenFader, MissionController.CompleteMission
AmbushMissionDirector ──► VehiclePatrol.StartRetreat, MissionController.CompleteMission
DroneManager ──► GameEvents.RaiseDroneSwitched ──► CameraApplier, TargetPingSystem, TrainingManager
DroneCollision ──► ExplosionService, EffectService, GameEvents.RaiseExplosion / RaiseDroneExploded
                   ──► DroneManager, MissionController, TrainingManager
VehicleModule ──► VehicleHealth.OnModuleHit + GameEvents.RaiseModuleDestroyed
EWReceiver ──► EWService.SampleJamAt, PostFxRouter
```

### Полезные «точки входа» в код

| Хочу… | Смотреть |
|---|---|
| Поменять условие победы/провала | `MissionController.CheckWinCondition()` / `EndMission()` |
| Поменять сценарий обучения | `TrainingManager.Update()` + `NextStep()` + `UpdateUI()` |
| Поменять сценарий засады | `AmbushMissionDirector.MissionSequence()` |
| Поменять физику дрона | `DroneFlightController.ApplyThrustAndRotation()` / `ApplyAerodynamics()` / `ComputeThrottleCurve()` |
| Поменять урон/броню | `DroneCollision.ApplyDirectDamage()`, `VehicleModule.TakeDamage()`, `ExplosionService.ApplySplash()` |
| Поменять прогрессию/очки | `ProgressService.ComputeScore()` / `RecordResult()` |
| Добавить миссию | новый `MissionDefinition` + добавить в `Resources/MissionRegistry.asset` + сцена в Build Settings |
| Поменять HUD | `StrikeHUD.OnGUI()` / `ReconHUD.OnGUI()` + `HudStyleService` |
| Поменять вид «рабочего стола» в бункере | `ComputerUIController` + префаб `MissionSelectPanel` |




