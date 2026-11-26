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
| `MainMenuController` | Навигация меню: главная панель / настройки, запуск бункера, выход, диагностика. С 22.11.2026: мёртвая панель миссий удалена из сцены и из кода; подписан на `SettingsPanel.Closed` (главная панель возвращается по `X` и `Esc` даже без persistent-вызова); выход двухшаговый («Выход» → «Выйти?» на 3 с, `Esc` отменяет) | `UIScreen`, `SettingsPanel`, `MissionRegistry`, `SceneLoader`, `GameStateMachine` |
| `MissionSelectScreen` | Заполнение списка миссий карточками, выбор карточки, открытие деталей, запуск миссии. С 22.11.2026: `Populate()` автоматически выбирает первую доступную миссию (правая панель сразу показывает брифинг), `ClearCards()` отвязывает карточки от `Content` до `Destroy` (нет дублей при повторном открытии) | `MissionCardView`, `MissionDetailsPanel`, `MissionRegistry`, `ProgressService` |
| `MissionCardView` | Карточка миссии: превью, имя, метаданные, оверлей блокировки, бейдж выполнения, рамка выбора | `MissionDefinition`, `MissionText` |
| `MissionDetailsPanel` | Правая панель деталей: баннер, статус, награда, описание, статистика, цели, техника, кнопка «Запустить». С 22.11.2026: в пустом состоянии кнопка неактивна и подписана «ВЫБЕРИТЕ ОПЕРАЦИЮ»; цель про потерю борта зависит от миссии (`«Борт можно заменить после потери»` для `PreparingDemo`) | `MissionDefinition`, `ProgressService`, `MissionText` |
| `MissionText` (static) | Склонения/форматирование: «N целей», длительность `mm:ss`, роль дрона | `MissionDefinition`, `DroneConfig` |
| `SettingsPanel` | Панель настроек: 4 вкладки (Графика 13 / Управление 10 / Аудио 8 / Геймплей 12), сброс (`ResetSettings` → `SettingsService.ResetAll` + пересборка строк), нормализация Z, fallback-поиск кнопок `CloseButton`/`ResetButton` по имени, пометка «тяжёлых» дескрипторов вкладки «Графика» (`AppliesHeavy`). С 22.11.2026: модальное окно 1400×920, `ResetButton` в префабе, активная вкладка задаётся напрямую (`Image.color` + Bold + красная полоса), при смене вкладки скролл сбрасывается наверх, убран двойной `UISoundService.Attach`. С 22.11.2026: сброс двухшаговый (первый клик — «Подтвердить?» на 3 с, второй — `ResetSettings()`), событие `Closed` для `MainMenuController`, при закрытии сворачивается раскрытый дропдаун и отменяется подтверждение сброса | `SettingsService`, `SettingsListBuilder`, `SettingsApplier`, `UISoundService`, `TacticalUITheme`, `BunkerModalStack` |
| `SettingsListBuilder` | Процедурная генерация строк настроек (слайдер/тумблер/дропдаун), `CollapseDropdown()` (сворачивает раскрытый список при закрытии панели), `SliderReleaseNotifier`; `AppliesHeavy` ограничивает `ApplyHeavy()` только графикой; `Clear()` отвязывает строки от `Content` до `Destroy` (нет дубликатов при пересборке в одном кадре). С 22.11.2026 строки — TMP `LiberationSans SDF` + спрайты HUF Pack, 50 px: подпись / значение (88 px) / контрол (320 px), опции дропдауна выровнены вправо и не растягиваются на всю ширину | `SettingDescriptor`, `SliderReleaseNotifier`, `UISoundService`, `TacticalUITheme` |
| `SliderReleaseNotifier` | Сообщает «отпустил ползунок» для настроек, применяемых по отпусканию | `Slider` |
| `UISoundService` (static) | Клики интерфейса: 2D-`AudioSource` (`UISoundHost`, `DontDestroyOnLoad`) в шине `AudioBus.UI`, идемпотентная подписка кнопок через `Attach()` | `ProceduralClips`, `AudioBus` |
| `ScreenFader` | Затемнение экрана с текстом (титры глав обучения, сон в бункере). С 25.11.2026: `SetMessage(string)` обновляет текст без перезапуска корутины, `FadeOut(message, holdOverride)` задаёт собственную паузу после полного затемнения | `CanvasGroup`, `TMP_Text` |
| `FpsCounterHud` | Автосоздаваемый `OnGUI`-счётчик FPS; рисуется в `MainMenu` и `Gameplay` (скрыт в `Boot`/`Loading`/`Paused`/`Debrief`, при `timeScale = 0` и во время загрузки) | `SettingsService`, `HudStyleService`, `GameStateMachine`, `SceneLoader` |

**Особенности:**

* `SettingsPanel.Awake()` вызывает `NormalizeZ()` — принудительно сбрасывает `localPosition.z` в 0 с подробным комментарием: у Overlay-канваса `GraphicRaycaster` не попадает в UI, смещённый по Z, и клики перестают работать (`SettingsPanel.cs:122-135`). Это костыль вокруг «кривого» префаба/сцены.
* `SettingsPanel.Start()` вызывает `SettingsService.EnsureInstance()` — панель может сама создать сервис, если открыта не из `Boot`.
* Исправлено (22.11.2026): кнопка «Сброс» теперь действительно сбрасывает настройки в обеих сценах. Причины бага: (1) в `SettingsPanel.prefab` поле `resetButton` не было сериализовано, поэтому `Start()` не подписывался на кнопку (с 22.11.2026 кнопка входит в префаб, поле назначено); (2) на сценовых кнопках `ResetButton` висел persistent-вызов `MainMenuController.OnBackFromSettings` — в `MainMenu` он закрывал панель, в `Bunker` имел `m_Target: {fileID: 0}` и ничего не делал. Решение: `ResolveMissingReferences()` ищет `ResetButton`/`CloseButton` по имени (`GetComponentsInChildren<Button>(true)`), `Start()` подписывает `ResetSettings` кодом идемпотентно (`RemoveListener` + `AddListener`); persistent-вызовы удалены из `MainMenu.unity` и `Bunker(main).unity`, сценовые дубликаты кнопки удалены. `ResetSettings()` вызывает `SettingsService.ResetAll()`, затем `SelectTab(currentTab)` — строки пересобираются из нового `Data`, поэтому слайдеры/тумблеры показывают дефолты сразу, а панель остаётся открытой.
* `MissionDetailsPanel` содержит 20+ сериализованных UI-ссылок и много логики форматирования; это самый «толстый» UI-скрипт.
* Исправлено (22.11.2026, аудит настроек): `InteractionUIController.ShowSittingHint()` теперь тоже уважает `gameplay.showInteractionHints` (раньше выключение подсказок скрывало только центральную, а «Встать [Пробел]» оставалась); `FpsCounterHud` рисуется и в `MainMenu`; клики кнопок меню/паузы/настроек озвучены через `UISoundService` (`AudioBus.UI`), посадка/вставание на стул — процедурный SFX через `EffectService` (`AudioBus.SFX`); `SettingsListBuilder.Clear()` отвязывает строки от `Content` перед `Destroy`, поэтому пересборка в одном кадре (сброс настроек, выбор пункта дропдауна) не оставляет дубликатов.

---

## 4. Бункер и взаимодействия (`Interaction/`, `Player/`)

| Скрипт | Ответственность | Зависит от |
|---|---|---|
| `IInteractable` | Контракт: `GetInteractionPrompt()`, `CanInteract()`, `Interact()` | — |
| `PlayerController` | FPS-движение (WASD), мышь-обзор, гравитация, аниматор, режим «сидит», применение настроек мыши; переключение поз — `SetSitting(bool)`, `SetTyping(bool)`, `ForceSitIdle()` (мгновенная посадка: `IsSitting = true` + `Play("SitIdle", 0, 0f)` + `Update(0f)`, клип `SitDown` не проигрывается) и `ForceIdle()` (сбрасывает `IsSitting`/`IsTyping` и жёстко переключает Animator в `Idle`, не дожидаясь конца клипа) | `CharacterController`, `Animator`, `SettingsService` |
| `CameraFollowHead` | Позиционирует камеру у кости головы с офсетом | `Transform headBone` |
| `PlayerInteractionDetector` | Raycast из камеры (3 м, слой), подсказка, `E` — взаимодействие. С 22.11.2026: при открытом любом модальном окне (`BunkerModalStack.AnyOpen`) — ранний выход и `HideHint()` | `IInteractable`, `InteractionUIController`, `BunkerModalStack` |
| `ChairInteractable` | Сесть/встать: телепорт на `sitPosition` `(0, −0.385, 0)` и **мгновенная** посадка без клипа `SitDown` — сразу `PlayerController.ForceSitIdle()` (`SitIdle` с нулевого кадра); отключение `CharacterController`/коллайдера; `Space` — встать: телепорт на `standPosition` **справа** от стула `(1.425, −1.6, 1.893)` + мгновенный `PlayerController.ForceIdle()`; подсказка/посадка только при приближении на `maxInteractDistance` (0.8 м по горизонтали до ближайшей точки коллайдера); **вставание блокируется, пока открыт рабочий стол** (`IsStandUpBlocked` ← `ComputerUIController.IsOpen`) | `PlayerController`, `InteractionUIController`, `ComputerUIController` |
| `ComputerInteractable` | Вход в компьютер (только сидя); дергает `ComputerUIController.ToggleDesktop()` | `PlayerController`, `ComputerUIController` |
| `ComputerUIController` | Анимация открытия «рабочего стола» (scale 0.1→1, alpha 0→1), закрытие по `E`/`Esc`; публичный `IsOpen` — состояние, по которому `ChairInteractable` запрещает вставание. С 22.11.2026: `timeScale`/курсор выставляет `BunkerModalStack.Apply()` (а не само окно), ссылка на `MissionMenuController` кэшируется в `Awake` (`FindAnyObjectByType` убран из `Update`/`ToggleDesktop`). С 26.11.2026: `HideForShop()`/`ShowAfterShop()` — временно прячут/возвращают окно под 3D-витриной магазина (сам `IsOpen` остаётся `true`, поэтому `timeScale` не «размораживается», а вставание из-за стола остаётся заблокированным); в `Update()` ранний выход, пока `DroneShopController.IsOpen` | `CanvasGroup`, `PlayerController`, `MissionMenuController`, `DroneShopController`, `BunkerModalStack` |
| `DesktopIcon` | Иконка «Миссии» → `MissionMenuController.OpenMissionMenu()` | `MissionMenuController` |
| `DesktopShopIcon` | **Новое (26.11.2026).** Иконка «Магазин» на `ComputerOS/Btn_Shop` → `DroneShopController.Open()` — запускает 3D-витрину, а не плоское окно | `DroneShopController` |
| `DroneShopController` | **Новое (26.11.2026).** Витрина магазина: `Open()` (скрыть рабочий стол через `HideForShop`, включить `DroneShopCamera`/`DroneShopLight`, показать HUD, спавн дрона), `CloseShop()` (`Destroy` копии, вернуть камеру и рабочий стол), `Next`/`Previous`/`Select`, `ResetView`; ввод `Esc` / `A`·`D` / `←`·`→` / `R` на unscaled-времени. Витринная копия: инстанс под неактивным родителем → снятие всех компонентов кроме геометрии (скрипты первым проходом и с конца — иначе `RequireComponent` возвращает `Camera`/`Rigidbody`) → нормализация масштаба по `Renderer.bounds` под `displaySize` → иерархия `DroneShopStage → YawPitch → Hover → модель` | `ComputerUIController`, `DroneShopUI`, `BunkerModalStack`, `DroneShopShowcase`, `DroneShopPropellers` |
| `DroneShopUI` | **Новое (26.11.2026).** HUD витрины в стиле `ComputerOS`: собирает собственный Canvas (ScreenSpaceOverlay, order 200, 1920×1080) — верхняя плашка с заголовком и счётчиком, правая панель «ТТХ + Теория и практика применения», нижняя панель с `◄`/`►`, «СБРОСИТЬ РАКУРС», «НАЗАД» и строкой управления; события `PrevRequested`/`NextRequested`/`ResetRequested`/`CloseRequested`, `ShowEntry`, `Show`/`Hide` | `TacticalUITheme`, `UISoundService`, `TMP_FontAsset` |
| `DroneShopShowcase` | **Новое (26.11.2026).** Парение ±0.015 м (синус, unscaled), микростабилизация корпуса (±1.1°) и вращение на 360°: ЛКМ + `Mouse.delta` (вне кнопок UI через `EventSystem.IsPointerOverGameObject`), yaw/pitch без ограничений с экспоненциальным сглаживанием, `ResetView()` — плавный возврат | `Mouse`, `EventSystem` |
| `DroneShopPropellers` | **Новое (26.11.2026).** Вращение винтов: ищет реальные узлы `prop/rotor/blade`, иначе строит 4 процедурных ротора (диск сметания + две лопасти, `Sprites/Default`, unlit, без ZWrite) в якорях `MotorFl/Fr/Bl/Br`; скорость 2200 °/с на unscaled-времени | `Transform`-якоря модели |
| `DroneShopEntry` | **Новое (26.11.2026).** `[System.Serializable]` карточка витрины: `displayName`, `role`, `specs[]`, `theory`, `prefab`, `displaySize`, `rotorRadius`, `modelYawOffset` | — |
| `DesktopRadarSweep` | Анимирует фон рабочего стола: вращает дочерний слой `DesktopSweep` (лепесток радара) вокруг пивота, совпадающего с центром радара. Угол считается от `Time.unscaledDeltaTime` (120°/с по часовой), поэтому вращение не останавливается при `Time.timeScale = 0`. Кадры не тратятся, когда ОС закрыт: проверка `ComputerUIController.IsOpen` (объект `ComputerOS` после `CloseDesktop()` остаётся активным с `CanvasGroup.alpha = 0`) | `ComputerUIController` |
| `InteractionUIController` | Подсказки (75 % высоты / низ экрана), окна «Магазин» и «Карта миссий», `timeScale`, курсор. С 22.11.2026 подсказки — **компактные надписи без плашек**: `InteractionHint` = `CanvasGroup` + TMP-`Label` 20 px (`anchoredPosition.y = −270`, 75 % высоты), `SittingHint` = `CanvasGroup` + TMP-`Label` 18 px (`y = −470`, 52 px от низа), у обоих `Label` компонент `Outline` (0,0,0,0.85 при смещении 1.5,−1.5) для читаемости без подложки; тексты и логика не менялись | `SettingsService` (флаг подсказок) |
| `MissionMenuController` | Открыть/закрыть меню миссий, `Populate` карточек, запуск миссии, кнопка «Назад». С 22.11.2026: guard `if (IsOpen) return;` (нет дублей карточек), один `Esc` закрывает всю панель (как «НАЗАД»), `UISoundService.Attach` для «Назад»/«ЗАПУСТИТЬ»/карточек, состояние FSM доводит `BunkerStateRestorer` (самовольный `Boot → MainMenu` из `Awake` удалён) | `MissionRegistry`, `MissionSelectScreen`, `MissionContext`, `SceneLoader`, `GameStateMachine`, `BunkerModalStack` |
| `BunkerModalStack` (static) | **Новое (22.11.2026).** Единый координатор модальных окон бункера: окна регистрируются в `Awake`, `AnyOpen` опрашивает живые `IsOpen`, `Apply()` держит `Time.timeScale` (0/1) и курсор (`None`+visible / `Locked`+hidden). `NotifySettingsChanged` участвует только при наличии бункерного контекста, поэтому `SettingsPanel` в `MainMenu` не захватывает курсор. С 26.11.2026 учитывает и `DroneShopController` — витрина магазина держит `timeScale = 0` и свободный курсор, но пауза по `Esc` при ней не открывается | `ComputerUIController`, `MissionMenuController`, `BunkerPauseController`, `SettingsPanel`, `DroneShopController` |
| `BunkerPauseController` | Пауза по `Esc`, блокировка ввода, настройки, выход в главное меню. С 22.11.2026: `timeScale`/курсор через `BunkerModalStack`, сам регистрируется в координаторе | `ComputerUIController`, `MissionMenuController`, `BunkerStateRestorer`, `PlayerInteractionDetector`, `ChairInteractable`, `InteractionUIController`, `PlayerController`, `SettingsPanel`, `BunkerModalStack` |
| `BunkerStateRestorer` | Восстановление позы после миссии: посадка, открытие ОС, меню миссий. С 22.11.2026: `EnsureGameplayState()` доводит FSM до `Gameplay` при прямом старте сцены (`Boot → MainMenu → Loading → Gameplay`), поэтому пауза работает и без прохода через `Boot`. Спавн у кровати не перезаписывает: корутина запускается только при `MissionContext.ReturnedFromMission` | `ChairInteractable`, `ComputerUIController`, `MissionMenuController`, `MissionContext`, `GameStateMachine` |
| `ISecondaryInteractable` | **Новое (25.11.2026).** Контракт доп. действия при наведении: `CanInteractSecondary()`, `OnSecondaryInteract(interactor, direction)`; `direction = 0` — клавиша `R`, `±1` — колесо мыши. Расширяет `IInteractable`, не заменяя его | — |
| `BunkerStoryProgress` | **Новое (25.11.2026).** Сюжетный прогресс бункера: `CurrentDay`, `IntroSeen`, `BriefingDone`, события `OnDayPassed(int)`, `MarkIntroSeen()`, `MarkBriefingDone()`, `AdvanceDay()`, `[ContextMenu] ResetStoryProgress()`. По умолчанию `persistProgress = false`: прогресс только в памяти сессии, ключи `BunkerStory.*` при старте удаляются; при включённом флаге — `PlayerPrefs` | `PlayerPrefs` |
| `BunkerStorySubtitles` | **Новое (25.11.2026).** Канал субтитров: очередь реплик `BunkerStoryLine { speaker, text, duration, voice }`, `ShowLine(line, onFinished)`, `Hide()`, `IsPlaying`; плавное появление/скрытие на unscaled-времени, опциональная озвучка в шине `Voice`. С 25.11.2026: `EnsureOnScreenLayout()` в `Start()` — если плашка оказалась за пределами канваса, она возвращается к нижнему краю (`defaultBottomOffset = 64`) с записью в консоль | `CanvasGroup`, `TMP_Text`, `AudioSource`, `RectTransformUtility` |
| `BunkerBedInteractable` | **Новое (25.11.2026).** `IInteractable` кровати: `E — Спать` → блокировка `PlayerController`/`PlayerInteractionDetector` → `ScreenFader.FadeOut` → **прокрутка часов на чёрном экране до 06:00** (`BunkerTimeManager`) → `AdvanceDay()` → `FadeIn`, субтитр «День N.» | `BunkerStoryProgress`, `BunkerTimeManager`, `ScreenFader`, `BunkerStorySubtitles`, `PlayerController`, `PlayerInteractionDetector`, `BunkerModalStack` |
| `BunkerTimeManager` | **Новое (25.11.2026).** Игровое время бункера: старт `06:00`, `minutesPerRealSecond = 1`, тик по `Time.deltaTime` (само останавливается при `timeScale = 0`), `IsSleeping` отключает автoтик на время сна. API: `CurrentHour/CurrentMinute/TotalMinutesFromMidnight/FormattedTime`, `SetTime`, `AddMinutes`, `GetMinutesUntilMorning` (`06:30 → 1410`, `02:15 → 225`, `06:00 → 1440`), `SetSleeping`; события `OnMinuteChanged`/`OnHourChanged`; при естественном переходе через `06:00` один раз вызывает `AdvanceDay()` | `BunkerStoryProgress` |
| `BunkerClockHUD` | **Новое (25.11.2026).** HUD часов `ДЕНЬ N  \|  HH:mm` на `InteractionCanvas/StoryClockHUD`: подписка на `OnMinuteChanged`/`OnDayPassed`, без `Update` и аллокаций на кадр, формат в `dayTimeFormat` | `TMP_Text`, `BunkerTimeManager`, `BunkerStoryProgress` |
| `BunkerWalkieTalkieEvent` | **Новое (25.11.2026).** Сюжет рации: стартовая реплика героя (один раз), звонок (`AudioClip` или процедурный) + пульсация `Light`/эмиссии, `E — Взять рацию` → Mixamo-рука через `Animator`: `Play(WalkieReach)` → захват по фактическому концу клипа (`normalizedTime ≈ 1`) с телепортом рации в `WalkieTalkieHandAnchor` (кость `RightHand`) → `CrossFadeInFixedTime(WalkieHold)` → **удержание `holdDuration = 5` с** под реплику командира → рука снова протягивается к панели (`WalkieReach 0 → 1`), на `t = 1` `ReturnToWall()`, затем опускание пустой руки (`1 → 0` + вес слоя `1 → 0`); событие `OnCommanderBriefingFinished`. Поза рации: выравнивание по центру габаритов (`alignBoundsToGrip`, `gripLocalOffset`), масштаб `handScaleMultiplier`, доворот `handLocalEuler`, `alignUprightToCamera` («антенной вверх, панелью к камере»). Доворот костей — четыре слота (`shoulder`/`upperArm`/`foreArm`/`hand` + `handLocalEulerOffset`), по умолчанию чистый сток: `(0,0,0)`, `(0,0,20)`, `(0,0,0)`, `(0,0,0)`. Скорости — `reachSpeed 0.85`, `lowerSpeed 0.85`, `holdBlendDuration 0.15`, `layerWeightIn/Out 0.2/0.25` в Inspector (см. §4.4) | `BunkerStoryProgress`, `BunkerStorySubtitles`, `BunkerProceduralAudio`, `InteractionUIController`, `Animator` |
| `BunkerRadioController` | **Новое (25.11.2026).** `List<AudioClip> tracks`, `E` — вкл/выкл с плавным фейдом и подсветкой шкалы, `R`/колесо — волна по кругу со щелчком; пустой список → процедурный шум; `RadioSpeaker` выведен в шину `RadioMusic` | `ISecondaryInteractable`, `AudioBus`, `BunkerProceduralAudio` |
| `BunkerProceduralAudio` (static) | **Новое (25.11.2026).** Процедурные клипы-заглушки: `Ring()` (двухтоновый вызов), `RadioStatic()` (эфирный шум), `Click()` (щелчок настройки) | — |
| `MissionUIManager` | Пауза в миссии, скрытие HUD (`IsHudHidden`), crash-панель, рестарт, возврат в бункер | `GameStateMachine`, `SceneLoader`, `MissionContext` |

**Особенности:**

* `PlayerInteractionDetector` работает **каждый кадр** (raycast + поиск `IInteractable` в родителях) — без троттлинга.
* `BunkerPauseController` — фактически «god object» бункера: держит ссылки на 10 систем и вручную управляет их `enabled`.
* `MissionUIManager.RestartMission()` **жёстко** грузит `PreparingDemo` (`MissionUIManager.cs:87`), а не текущую миссию — рестарт корректно работает только в обучении.
* `ChairInteractable` пишет 5 отладочных `Debug.Log` на каждую посадку/вставание.
* Исправлено (21.11.2026): посадка на стул **мгновенная** — клип `SitDown` не проигрывается (`ChairInteractable.SitDown()` → `PlayerController.ForceSitIdle()` ставит `SitIdle` с нулевого кадра), вставание — `PlayerController.ForceIdle()` (сразу `Idle`). Переход `SitDown → Idle` (`IsSitting` = *If Not*, `hasExitTime = false`, 0.2 с) в `PlayerAnimator.controller` оставлен как страховка, но `SitDown` кодом не запускается. Проверка `isSitting` для «не сажать уже вставшего» вынесена в `ComputerUIController.CloseDesktop()`.
* Исправлено (22.11.2026): пока открыт рабочий стол (`ComputerUIController.IsOpen == true`), `ChairInteractable` **не даёт встать** — ранний выход в `Update()` и guard в `StandUp()` (публичное свойство `IsStandUpBlocked`). Причина бага: `Update()` выполняется и при `Time.timeScale == 0`, поэтому `Пробел` поднимал игрока, не закрывая ОС. `Esc`/`E` закрывают ОС, и игрок остаётся сидеть (`CloseDesktop()` → `ForceSitIdle()`); вставание доступно сразу после закрытия. `BunkerStateRestorer` использует `Interact(go, force: true)` → `SitDown()`, поэтому принудительная посадка после миссии не затронута. Ссылка на `ComputerUIController` резолвится в `Awake()` через `FindAnyObjectByType<ComputerUIController>(FindObjectsInactive.Include)`, т.к. объект `ComputerOS` в сцене выключен (`m_IsActive: 0`) — правок сцены не потребовалось.

---

### 4.1 Координатор модальных окон бункера (22.11.2026)

Раньше `Time.timeScale` и `Cursor` правились в каждом окне по отдельности: `ComputerUIController` при закрытии безусловно ставил `timeScale = 1` и захватывал курсор, `MissionMenuController.CloseMissionMenu()` не восстанавливал ничего и предполагал, что за панелью открыт рабочий стол. Из-за этого закрытие панели миссий без ОС оставляло игру «замороженной» (`timeScale = 0`, курсор свободен, поверх ничего нет).

Теперь состояние принадлежит `Assets/Scripts/Interaction/BunkerModalStack.cs`:

| Участник | Что делает |
|---|---|
| `ComputerUIController`, `MissionMenuController`, `BunkerPauseController` | `BunkerModalStack.Register(this)` в `Awake`, `Unregister` в `OnDestroy` |
| `SettingsPanel` | `BunkerModalStack.NotifySettingsChanged(this)` в `Show()`/`Hide()`; координация включается только если в сцене есть бункерные окна |
| `BunkerModalStack.AnyOpen` | `computer.IsOpen \|\| missionMenu.IsOpen \|\| pause.IsOpen \|\| settings.IsOpen` (Unity-`null` у уничтоженных объектов = «закрыто», состояние не протекает между сценами) |
| `BunkerModalStack.Apply()` | открыто хотя бы одно окно → `timeScale = 0`, `Cursor.None` + visible; иначе → `timeScale = 1`, `Cursor.Locked` + hidden |
| `PlayerInteractionDetector` | при `AnyOpen` → `HideHint()` и выход из `Update()` (подсказка не висит поверх терминала, рейкаст не крутится при `timeScale = 0`) |

Проверено в Play Mode 22.11.2026: панель миссий без ОС → закрытие возвращает `timeScale = 1` и захват курсора; панель поверх ОС → после закрытия остаётся `timeScale = 0`; прямой старт `Bunker(main)` → `state = Gameplay`, `Esc` открывает паузу; подсказка «Войти в систему [E]» скрывается при открытии терминала и возвращается после закрытия.

### 4.2 Сюжетная интерактивность бункера (25.11.2026)

Новый слой поверх существующего конвейера взаимодействий, без второго raycast-детектора:

```
PlayerInteractionDetector (каждый кадр)
  └─ IInteractable             → подсказка + E
       └─ ISecondaryInteractable → R / колесо мыши (только радио)
```

| Ключ / событие | Назначение |
|---|---|
| `E` | универсальное взаимодействие: спать, взять рацию, вкл/выкл радио |
| `R`, `Mouse ScrollWheel` | смена волны радио (работает только при взгляде на `Prop_RadioReceiver`) |
| `BunkerStoryProgress.OnDayPassed(int newDay)` | точка расширения сюжета на смену суток |
| `BunkerWalkieTalkieEvent.OnCommanderBriefingFinished` | завершение брифинга: сюда добавляется выдача рации в инвентарь вместо возврата на стену |
| `BunkerWalkieTalkieEvent.OnWalkiePickup` | момент, когда рация взята в руку (SFX/анимация) |

Особенности:

* **Прогресс по умолчанию не сохраняется.** `BunkerStoryProgress.persistProgress = false` → `CurrentDay = 1`, `IntroSeen = false`, `BriefingDone = false` при каждом входе в сцену; в `Awake()` удаляются ключи `BunkerStory.Day/IntroSeen/BriefingDone`, чтобы старые тестовые значения не влияли на прогон. Включение флага переводит класс на `PlayerPrefs` (запись в `AdvanceDay`/`MarkIntroSeen`/`MarkBriefingDone`). `ResetStoryProgress()` доступен из контекстного меню компонента.
* **Спавн.** `Player` стоит в `(-0.55, -1.60, -3.40)`. `BunkerStateRestorer` телепортирует на стул только после миссии, поэтому обычный вход сохраняет эту точку.
* **Слой `Interactable`.** Детектор ищет только слой 7 (`interactableLayer = 128`), поэтому кровать, рация и радио переведены на него; матрица коллизий `Default ↔ Interactable` не отключена — игрок по-прежнему упирается в кровать.
* **Затемнение.** Переиспользован `ScreenFader` (unscaled-время): `FadeOut(message)` → действие → `FadeIn()`, поэтому переход не зависает при `Time.timeScale = 0`.
* **Подсветка.** `GlowLight`/`DialLight` — `Point Light` с пульсацией на `Time.unscaledTime`; эмиссия ставится на **инстанс** материала (`renderer.material`, свойства `emissiveFactor`/`_EmissionColor`), общие материалы GLB-моделей не меняются.
* **Аудио.** В `Resources/Audio/MainMixer.mixer` добавлена группа `RadioMusic` (ребёнок `Master`) и Exposed Parameter `RadioVolume`; `AudioBus.Radio`/`AudioBus.RadioParameter`, `AudioApplier` выставляет радио по громкости музыки с учётом `duckOnMenus`. `AudioBus.FindGroup()` теперь сначала ищет точное имя (иначе `FindMatchingGroups("Music")` матчил и `RadioMusic`).
* **Заглушки без ассетов.** `BunkerProceduralAudio` даёт вызов рации, эфирный шум и щелчок, если `AudioClip` не назначены — консоль остаётся чистой.
* **Точки настройки в Inspector:** тексты/длительности реплик — у `BunkerWalkieTalkieEvent` (`firstRunLine`, `briefingLine`) и `BunkerBedInteractable`; поза рации в руке — `handLocalPosition/Euler/ScaleMultiplier`, `alignBoundsToGrip`/`gripLocalOffset` и позы руки `armRest/Reach/Hold…` (см. §4.4); треки радио — список `tracks` у `BunkerRadioController`; флаг сохранения — `persistProgress` у `BunkerStoryProgress`.

### 4.3 Игровое время и перемотка во сне (25.11.2026)

```
BunkerTimeManager (Update, Time.deltaTime)
  ├─ OnMinuteChanged ──► BunkerClockHUD.Refresh  («ДЕНЬ N  |  HH:mm»)
  └─ утро 06:00 ───────► BunkerStoryProgress.AdvanceDay() → OnDayPassed → BunkerClockHUD

BunkerBedInteractable.SleepRoutine (Time.unscaledDeltaTime)
  ├─ SetSleeping(true) → автотик выключен
  ├─ ScreenFader.FadeOut(message, 0.25) → полностью чёрный экран
  ├─ прокрутка sleepTimelapseDuration (3 с, smoothstep) + ScreenFader.SetMessage
  └─ SetTime(06:00) → AdvanceDay() → «День N — 06:00» (0.4 с) → FadeIn → SetSleeping(false)
```

| Возможность | Реализация |
|---|---|
| Старт и скорость | `startHour = 6`, `startMinute = 0`, `minutesPerRealSecond = 1` (Inspector) |
| Пауза времени в модалках | тик на `Time.deltaTime`; `BunkerModalStack` ставит `timeScale = 0` — время стоит без отдельных проверок |
| Естественная смена суток | индекс утра от `absoluteMinutes`, один `AdvanceDay()` на переход через `morningHour` (`06:00`) |
| Сон | `SetSleeping(true)` → перемотка ведётся кроватью, автопереход через `06:00` подавлен, `AdvanceDay()` вызывает кровать |
| Текст на чёрном экране | `ScreenFader.SetMessage(string)` — без перезапуска корутины; `FadeOut(message, holdOverride)` — своя пауза после затемнения |
| HUD | `BunkerClockHUD` на `InteractionCanvas/StoryClockHUD`, обновление только по событиям, без `Update` |
| Сборка UI | Editor-билдер `Assets/Scripts/Editor/BunkerClockHudBuilder.cs` (`Tools ▸ DroneSimulator ▸ UI ▸ Build Bunker Clock HUD (Bunker)`) |

Особенности:

* `GetMinutesUntilMorning` считает до **ближайшего** утра: `06:30 → 1410`, `02:15 → 225`, ровно `06:00 → 1440` (полные сутки).
* `AddMinutes` корректно проходит полночь (`23:59 + 1 → 00:00`), `SetTime` меняет только время внутри суток и не двигает день.
* Прокрутка сна не вызывает `AddMinutes`: время ставится через `SetTime`, чтобы не сработал автопереход и не было двойной смены суток.
* Прогресс по-прежнему не сохраняется (`persistProgress = false`), время сбрасывается в `Awake()` — `День 1, 06:00` на каждый запуск.

### 4.4 Рация в родной руке персонажа: AvatarMask + слой аниматора (26.11.2026)

Рацию `Prop_WalkieTalkie` держит **родная правая рука** основного персонажа (`Player/Visual/SWAT`): анимация поднимает её отдельным слоем `WalkieArmLayer` в `PlayerAnimator.controller`, а маска `RightArmOnly` не даёт слою трогать ноги, туловище, голову и левую руку. Отдельная viewmodel-рука на камере, её меш и контроллер удалены (примитивная рука из капсул была удалена ещё раньше).

```
Player/Visual/SWAT                       (Animator: SwatAvatar + PlayerAnimator.controller)
 ├─ Geo/Soldier_body                     (родное тело, слой Default — камера его видит)
 ├─ Geo/Soldier_head                     (слой 6 PlayerBody — камера его не видит)
 └─ …/mixamorig:Hips … RightForeArm
      └─ …/RightHand
           └─ WalkieTalkieHandAnchor     (localPos 0.004, 0.085, 0.038)
Main Camera
 └─ FillLight                            (Point 0.3 / 1.0 м, localPos 0, 0.16, 0.26)
```

| Элемент | Реализация |
|---|---|
| Маска | `Assets/Animations/Player/RightArmOnly.mask` — humanoid-маска (`transformCount = 0`), включены только `RightArm` + `RightFingers`; плечо `mixamorig:RightShoulder` входит в группу `RightArm`. Частичных весов humanoid-маска не поддерживает, поэтому «естественное» плечо делается не весом маски, а offset-углами костей (см. ниже) |
| Слой | `PlayerAnimator.controller`, слой **`WalkieArmLayer`** (индекс 1): `Override`, `defaultWeight = 0`, `IK Pass` off, маска `RightArmOnly`; состояния `Empty` (без клипа, `writeDefaultValues = false`, дефолтное), `WalkieReach`, `WalkieHold`. Скорость клипов слоя масштабируется параметром **`WalkieSpeed`**, а не глобальным `Animator.speed` — иначе вместе с рукой замедлялась бы и ходьба |
| Клипы | `Button Pushing.fbx` → **`WalkieReach`** (кадры **0…34** = 1.133 с, без лупа), `Texting.fbx` → **`WalkieHold`** (кадры **29…30** = 0.033 с, `loopTime = true`). Оба FBX: `animationType = Humanoid`, `avatarSetup = CopyFromOther`, `sourceAvatar = SwatAvatar`, обрезка задана через `clipAnimations` в `ModelImporter` |
| Фазы (`BunkerWalkieTalkieEvent`) | 1) `SetLayerWeight(0)` → `Play(WalkieReach, 1, 0)` + `SetFloat(WalkieSpeed, reachSpeed = 0.85)`, вес слоя `0 → 1` за `layerWeightInDuration = 0.2 с`, ждём **фактический конец клипа** (`while (elapsed < clipLength/reachSpeed \|\| GetReachNormalizedTime() < 0.98f)`); 2) конец клипа → `AttachToHand()` + `AlignPropToHand()`, звонок и мигание off, коллайдер off, `OnWalkiePickup`; 3) `CrossFadeInFixedTime(WalkieHold, 0.15, 1)` и после перехода повторный `AlignPropToHand()`, затем **удержание `holdDuration = 5 с`** (`HoldBriefingPose()`: таймер + параллельная реплика командира); 4) `FinishBriefing()` (флаги + событие); 5) `ReturnWalkieRoutine()` — `PlayReachToWall()` (клип вперёд `0 → 1` с рацией в руке, вход из `WalkieHold` через `CrossFade`), на `t = 1` `ReturnToWall()`, затем `PlayLowerAndFade()` (`Play(WalkieReach, 1, 1 − t/duration)` + вес `1 → 0` за `layerWeightOutDuration = 0.25 с` в хвосте, финал — `Play(Empty, 1, 0)`). Ранее `ReturnToWall()` вызывался сразу после опускания руки — рация «уходила» вниз вместе с рукой |
| Коррекция позы (`LateUpdate`) | Чистый сток Mixamo: плечо `mixamorig:RightShoulder (0, 0, 0)` (ключицу не трогаем — иначе ломается куртка), предплечье `(0, 0, 0)`, кисть `mixamorig:RightHand (0, 0, 0)`. Одна аккуратная правка — лёгкий подъём верхней части руки `mixamorig:RightArm (0, 0, 20)`; замерено в Play Mode (`WorldToViewportPoint` анкера, pitch 15°): сток даёт `vp.y = −0.286` (кисть под кадром), `(0, 0, 20)` — `−0.023` и центрирование `vp.x = 0.490`, остальные оси хуже (`X+20 → −0.335`, `Y−20 → 0.695`). Угол умножается на текущий вес слоя, поэтому коррекция плавно вводится и снимается. Защита от накопления: если Animator в кадре кость не перезаписал (`CullUpdateTransforms`, `timeScale = 0`), базой берётся сохранённый «чистый» поворот |
| Почему прокрутка вручную | у незацикленного клипа отрицательная `Animator.speed` упирается в конец состояния (`state.speed = 0`), рука остаётся в позе удержания — поэтому нормализованное время выставляется из корутины |
| Ориентация рации | `alignUprightToCamera` ставит «+Z (антенна) вверх, −Y (лицевая панель) к камере» **один раз** — при захвате и после `CrossFade`; дальше `localRotation` фиксируется, и рация жёстко следует за локальным поворотом кисти, поэтому при опускании руки мировая вертикаль не перезаписывается. `gripLocalOffset` садит в ладонь середину корпуса, а не центр полного AABB |
| Масштаб рации | мировой `0.0264 × 0.66 = 0.017424` делится на `lossyScale` анкера (`1.2` — скелет `SWAT` отмасштабирован) → локальный масштаб `0.0145`; рация в руке остаётся того же размера, что и на стене |
| Подсветка | `FillLight` (Point **0.3 / 1.0 м**, без теней) переехал с удаляемой руки на `Main Camera` — `localPos (0, 0.16, 0.26)` |
| Время | ожидания — по `Time.deltaTime`; последовательность запускается только при `timeScale = 1` (`CanInteract` запрещён при `BunkerModalStack.AnyOpen`) |
| Сборка | `Tools ▸ DroneSimulator ▸ Bunker ▸ Build Walkie Arm Assets` (`Scripts/Editor/PlayerWalkieAssetBuilder.cs`: клипы, маска, слой) и `… ▸ Attach Walkie To Player Hand` (`Scripts/Editor/BunkerWalkieHandBuilder.cs`: сцена, анкер, `FillLight`, привязка полей, удаление `PlayerRightArm.asset` и `PlayerArmViewmodel.controller`) |

Проверка (Play Mode, 26.11.2026, игрок в 1.3 м от панели, взгляд вниз 20°): после `E` вес `0 → 1` за 0.2 с, `WalkieReach`, захват на `nt = 0.987` (раньше — `0.753`, то есть рука не успевала вытянуться); в `WalkieHold` анкер кисти `vp = (0.489, 0.035, 0.280)`, `d = 0.338 м` — кисть с рацией в кадре у нижней кромки; удержание **5.41 с** (таймер 5.0 + бленд входа 0.15 + фейды субтитра 0.25); возврат: `PlayReachToWall` до `nt = 0.996`, `ReturnToWall()`, опускание с весом `1 → 0`; финал — `Empty`, `weight = 0.000`, рация в `BunkerStoryProps` (`localPos 2.804, −0.492, 1.097`, `scale 0.0264`), `BriefingDone = true`. 4 кадра сняты напрямую с `Main Camera` (`RenderTexture` → `ReadPixels` → PNG), временные файлы удалены. Консоль — **0 ошибок и 0 предупреждений** (после компиляции, после Play-прогона и после выхода из Play Mode).


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
| `EWReceiver` | Приём помех: `jamIntensity = max(raw, boundary) * (1 - ewResistance)`, постэффекты (chromatic/vignette/grain), джиттер кадра и FOV, процедурный «снег» и разрывы в `OnGUI` (рисуются всегда) | `EWService`, `PostFxRouter`, `FPVZoom`, `SettingsService` (только `ewPostFxQuality`/`ewNoiseResolution`), `MissionUIManager` |
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
| `SettingsService` (singleton) | Загрузка/миграция/сохранение `settings.json` (`SchemaVersion = 4`), `MarkDirty()` (дебаунс 0.5 с), `ApplyAll/ApplyInstant/ApplyHeavy`, `ResetAll`, `SetMasterVolume/SetSfxVolume/SetQuality/SetFullscreen`, повторное применение при `sceneLoaded`, подписка на `GameStateMachine.StateChanged` (пересчёт duck-музыки) |
| `AudioBus` (static) | `Resources.Load<AudioMixer>("Audio/MainMixer")`, группы `Master/Music/SFX/DroneMotors/EW/UI`, `SetVolume(param, linear)` с переводом в дБ; Exposed Parameters `MasterVolume`…`VoiceVolume` |
| `AudioApplier` (static) | Громкости через микшер; fallback на `AudioListener.volume` при отсутствии микшера; `duckOnMenus` — музыка ×0.35 вне `Gameplay` |
| `GraphicsApplier` (static) | Runtime-клон URP-ассета: `renderScale`, тени, MSAA, soft shadows; `SettingsScope` выбирает «бункерный»/«дроновый» shadow distance |
| `CameraApplier` (static) | `farClipPlane`, `renderPostProcessing`, `renderShadows`, `antialiasing` у FPS- и дрон-камер; подписка на `GameEvents.DroneSwitched` |
| `PostFxRouter` (singleton) | Три runtime-Volume (`PostFX_EW` / `PostFX_NV` / `PostFX_Settings`), раздача эффектов; `ApplyMotionBlur(enabled)` включает эффект с интенсивностью 0.6 (только вне миссий) |
| `HudStyleService` (static) | Стиль HUD: `Scale`, `Transparency`, `ApplyAlpha`, `CrosshairSize`, `LineThickness`, `TargetFrameVisible`, `GUI.matrix`-масштабирование |
| `SettingsScope` (static) | `IsMission` — по наличию `DroneManager` в сцене |

**Особенности:**

* `SettingsService.SchemaVersion = 4`, `SettingsData.version` тоже `4` — свежесозданный объект больше не проходит миграцию. Миграция: `v < 3` → `windowMode` из `fullscreen`; `v < 4` → `resolutionWidth/resolutionHeight` из legacy-поля `resolutionIndex` (оно оставлено только для чтения старых файлов и всегда сбрасывается в `-1`). `windowMode` — единственный источник истины, `fullscreen` — производное зеркало (`SettingsApplier.ApplyScreen`).
* Разрешение хранится в пикселях: `graphics.resolutionWidth/resolutionHeight` (`0 × 0` = «Текущее»). Индекс в `Screen.resolutions` больше не сохраняется, поэтому настройка не «съезжает» при смене монитора/набора режимов. UI-дропдаун ↔ данные конвертируют `SettingsApplier.FindResolutionOptionIndex()` / `SetResolutionOption()`.
* Лимит FPS «побеждает» VSync: при `fpsLimit > 0` принудительно `QualitySettings.vSyncCount = 0` и `Application.targetFrameRate = fpsLimit`; при `fpsLimit == 0` действует настройка VSync (`targetFrameRate = -1`).
* Motion Blur бункера больше не утекает в миссии: `SettingsApplier.ApplyHeavy` вызывает `ApplyMotionBlur(!SettingsScope.IsMission && graphics.bunkerMotionBlur)`. Сам эффект включается корректно (`active = enabled`, `intensity = 0.6`; раньше было `active = !enabled` и `intensity = 0`).
* `ApplyHeavy()` запускается только для дескрипторов с `AppliesHeavy` (вкладка «Графика») и только по отпусканию слайдера — настройки управления/звука/геймплея больше не дёргают `Screen`/`QualitySettings`/URP на каждый тик.
* Удалены неиспользуемые поля настроек (22.11.2026): `graphics.droneFog`, `graphics.droneFogDensity`, `graphics.droneLensDistortion`, `graphics.ewVisualDistortion`, `graphics.nightVisionGain`, `gameplay.reduceDangerousEffects`. `EWReceiver.VisualScale` больше не масштабируется настройкой (жёстко `1f` — прежний дефолт `ewVisualDistortion`), поэтому шум и джиттер РЭБ всегда при 100 %. Старые ключи в `settings.json` игнорируются `JsonUtility` и исчезают при следующем сохранении.
* `GraphicsApplier` использует рефлексию к `m_SoftShadowsSupported` — при обновлении URP поле может исчезнуть, и soft shadows молча перестанут применяться.
* Ограничение Editor Play Mode: `Screen.SetResolution`/`Screen.fullScreenMode` и `Application.targetFrameRate` в редакторе применяются не полностью (Game view) — проверять их нужно в билде. Путь данных и фактические вызовы проверены в Play Mode (кэш `SettingsApplier.lastWindowMode/lastResolutionWidth/lastResolutionHeight`).

---

## 9. Аудио

| Скрипт | Ответственность |
|---|---|
| `DroneMotorAudio` | Процедурный луп моторов (4 гармоники 96 Гц + «wobble»), громкость/питч от `DroneMotors.CurrentThrust01`, `AudioBus.Motors`; клип генерируется один раз (static cache) |
| `EWAudio` | Процедурный фильтрованный белый шум, громкость/питч от `EWReceiver.jamIntensity`, `AudioBus.EW` |
| `ProceduralClips` (static) | Кэшированные процедурные клипы: `Click()` (интерфейс) и `Thud()` (посадка/вставание) — без внешних аудио-ассетов |

**Особенности:**

* `Assets/Resources/Audio/MainMixer.mixer` — группы `Master → Music / SFX / DroneMotors / EW / UI / Voice` и Exposed Parameters `MasterVolume`, `MusicVolume`, `SfxVolume`, `MotorsVolume`, `EwVolume`, `UiVolume`, `VoiceVolume`. `AudioBus` ищет группы по имени (`FindMatchingGroups`), остальные системы (`EffectService`, `DroneMotorAudio`, `EWAudio`, `UISoundService`) берут группы напрямую.
* В `MainMenu` на `MenuCamera` добавлен `AudioListener`, а звук видео-фона (`MenuVideoPlayer`, 1 аудиодорожка) выведен в `AudioSource` с группой `Music` — слайдеры «Музыка» и «Приглушать при меню» слышны уже в главном меню.

---

## 10. Editor-утилиты (`Assets/Scripts/Editor`)

| Скрипт | Меню | Что делает |
|---|---|---|
| `TacticalUITheme` (runtime static) | — | Палитра/метрики UI: `AccentRed #DB143D`, `PanelFill`, `Scrim`, размеры модалок/строк/карточек, `ButtonColorBlock()` |
| `TacticalUIBuilder` (editor static) | — | Загрузка спрайтов HUF Pack и `LiberationSans SDF`, `EnsureImage/EnsureText/EnsureButton`, `BuildTacticalFrame` (контур + красные уголки) |
| `MissionSelectPanelBuilder` | `Tools/DroneSimulator/Mission UI/Rebuild Prefabs`, `.../Apply All` | **In-place** обновляет `MissionSelectPanel.prefab` и `MissionCard.prefab` (не пересоздаёт `Header`/`MissionDetailsPanel`, сохраняет fileID) |
| `MissionContentBuilder` | `Tools/DroneSimulator/Mission UI/Apply Mission Content` | Заполняет описания/подсказки миссий и имена дронов в ассетах |
| `SettingsPanelPrefabBuilder` | `Tools/DroneSimulator/Create SettingsPanel Prefab` | **In-place** стилизует `SettingsPanel.prefab` (модалка 1400×920, HUF, TMP, Reset/Close) |

С 22.11.2026 билдеры идемпотентны: повторный запуск не создаёт дубликатов, не меняет fileID компонентов и не рвёт ссылки сцен. Ручные правки префаба всё равно лучше вносить в билдер — при следующем запуске они будут перезаписаны стилем.

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

---

## 12. Fix-QA тренировки: изменения скриптов (22.11.2026)

### 12.1 Новые файлы

| Файл | Роль |
|---|---|
| `Assets/Scripts/Core/GameStateBootstrap.cs` (static) | `EnsureGameplayState()`: поднимает `SettingsService` (если сцену запустили напрямую) и доводит FSM по матрице `Boot → MainMenu → Loading → Gameplay`. Вызывается из `MissionController.Awake()` |
| `Assets/Scripts/Editor/TrainingPanelsBuilder.cs` | меню `Tools ▸ DroneSimulator ▸ Mission UI ▸ Rebuild Training Panels`: in-place пересборка `PausePanel` / `CrashPanel` / `DebriefPanel`, стилизация `InstructionText`/`FadeText`/`SubtitleText`, назначение ссылок. Идемпотентен, корни панелей сохраняются |

### 12.2 Изменённые скрипты

| Скрипт | Что изменено |
|---|---|
| `TrainingGate` | событие стало `event Action<TrainingGate>` (отдаёт саму арку), добавлен `ResetGate()`; цвет кольца кэшируется и восстанавливается |
| `TrainingManager` | учёт колец через `HashSet<TrainingGate>` + `ResetGateProgress()` при входе в `StrikeFlyGates` и после замены борта → softlock `2/3` невозможен; респавн через `StartCoroutine(RespawnStrikeDroneRoutine)` на `WaitForSecondsRealtime(1.5f)` + субтитр «БОРТ ПОТЕРЯН»; `CleanupChapter()` вместо глобального удаления `ParticleSystem`/`AudioSource`; `trainingHints` скрывает `InstructionText`; `TrainingStep.Completed` выставляется явно; `DroneManager.RespawnPoint` переключается по главам |
| `ScreenFader` | `Time.unscaledDeltaTime`, `WaitForSecondsRealtime`, поле `holdSeconds` — переход главы не зависает при `timeScale = 0` |
| `MissionController` | FSM-бутстрап через `GameStateBootstrap`; `CrashPanel` больше не открывается там, где есть `TrainingManager` (магическая строка `"PreparingDemo"` убрана); из `Paused` сначала возврат в `Gameplay`, потом `Debrief` |
| `MissionUIManager` | пауза/настройки: `OpenPause`, `OpenSettings`, `ApplyPausedState` (`timeScale = 0`, `AudioListener.pause = true`, FSM `Paused`), `HandleEscapePressed()` (приоритет: настройки → пауза → игра), `AudioListener.pause = false` при продолжении/дебрифе/выходе, `ReturnToMainMenu()`, `RestartMission()` по `MissionContext.Current.sceneName` с рестартом через `Loading`, кнопки панелей подписываются кодом (рекурсивный поиск внутри `Plate`) |
| `DebriefController` | кнопки `RetryMission()` / `ReturnToMainMenu()` (плюс прежний `ReturnToMenu`), `Esc` = «вернуться в бункер», защита от `MissionContext.Current == null`, семантические цвета `TacticalUITheme.StatusCompleted/StatusLocked` |
| `DroneManager` | роль дрона (`DroneConfig.droneRole`) вместо подстроки `"FPV"`; границы полигона: `IsOutOfBounds`, события `OutOfBoundsStarted/Ended`, автовозврат в зону через `outOfBoundsGrace`; `ResolveSpawnPosition()` (raycast вниз) — спавн у поверхности; свойство `RespawnPoint` |
| `DroneConfig` | новый `enum DroneRole { Recon, Strike, Interceptor }` и поле `droneRole` |
| `GameStateMachine` | `Loading` разрешён из `Gameplay` и `Paused` (рестарт миссии идёт через `Loading`) |
| `MissionSelectScreen` | автовыбор: первая **непройденная** доступная миссия → первая доступная → первая в списке |
| `UISoundService` | `AudioSource.ignoreListenerPause = true` — клики UI слышны при `AudioListener.pause` |
| `TargetLockManager`, `FPVZoom`, `NightVisionController` | ранний выход при `Time.timeScale <= 0` (ввод не двигает шаги/зум/ПНВ на паузе) |
| `ReconHUD`, `StrikeHUD` | OSD локализован (`[КАМЕРА РАЗВЕДКИ АКТИВНА]`, `ЗАХВАТ:`, `ВЗВЕДЁН / КУМУЛЯТИВНЫЙ`, `[ВНИМАНИЕ: ПОМЕХИ РЭБ n%]`), палитра Tactical Metal: `Steel` + `AccentRed` для захвата/предупреждений |

### 12.3 Ресурсы и префабы

* `Assets/Settings/PC_Renderer.asset` — фича переименована в `Night Vision Feature` (совпадает со строкой в `NightVisionController`);
* `Assets/Prefabs/Drones/StrikeDrone 1.prefab` — `NightVisionController.config = SmallFpvHeatConfig`, `isNightVisionActive = false`; то же поле сброшено в `GimbalDrone Variant.prefab`;
* `DroneConfigs/*.asset` — `droneRole` (Mavic = Recon, SmallFpv = Strike, Intersector = Interceptor);
* `Resources/Missions/Mission1.asset` — `targetCount = 3`.





