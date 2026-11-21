Контекст проекта DroneSimulator
Этот файл — часть документации проекта DroneSimulator.
Остальные файлы: CONTEXT.md, ARCHITECTURE.md, INDEX.md, SETTINGS_DESIGN.md.

1. О проекте
DroneSimulator — первый игровой проект заказчика и учебный прототип симулятора военного оператора дронов. Проект создаётся самостоятельно для изучения Unity, MCP, DSH и разработки игрового прототипа.

Текущий статус:

игра ещё не собрана в финальный билд;

проект не вышел в бету;

это не production-проект;

структура содержит экспериментальные, демонстрационные и сторонние ассеты;

рефакторинг сознательно отложен;

беспорядок, дубликаты и неиспользуемые файлы ожидаемы для текущего этапа.

Техническая база:

Unity 6.6;

основной рабочий pipeline — URP;

основная сцена базы: Assets/Scenes/player.unity;
новая сцена базы (перенос на бункер): Assets/Scenes/Bunker(main).unity;

C#-скрипты сохранены в UTF-8 без BOM;

используется новый Input System:
действия дрона идут через статический адаптер GameInput (Assets/Scripts/Input/GameInput.cs) и карту Drone из Assets/Input/InputSystem_Actions.inputactions;

прямое чтение Keyboard.current / Mouse.current осталось только в UI- и interaction-скриптах (Esc, E, клики, выбор борта 1–9);

старого Input.GetKey / Input.GetKeyDown / Input.GetAxis в Assets/Scripts больше нет (было 10 файлов, осталось 0);

старый ProjectSettings/InputManager.asset больше не используется, но остаётся включённым (activeInputHandler = Both).

При работе с проектом:

не ругать проект за временную структуру;

не менять физику, управление и концепцию объектов без отдельного разрешения;

не считать отсутствие production-архитектуры ошибкой само по себе;

перед изменениями читать существующие сцены, префабы и скрипты;

учитывать, что часть дроновых систем существует отдельно от FPS-базы;

не запускать Play Mode без явного разрешения.

2. Концепция игры
Игра — симулятор военного оператора дронов с двумя режимами.

Режим 1: ходьба по военной базе
Игрок находится на военной базе и управляется от первого лица.

Предполагаемые элементы базы:

военные здания и объекты окружения;

палатки с разными функциями;

выбор миссии;

покупка дронов;

покупка и настройка пресетов;

пополнение боеприпасов;

ограничение территории забором или невидимыми стенами;

фургон на входе в базу.

Игрок должен видеть руки и тело персонажа при опускании камеры. Текущий персонаж — SWAT-модель Mixamo.

Режим 2: управление дроном на миссии
После выбора миссии игрок отправляется на отдельную карту и управляет дроном.

Возможные типы миссий:

разведка;

атака;

уничтожение целей;

доставка;

патрулирование;

перехват;

работа в условиях РЭБ.

Предусмотренный игровой цикл:

Военная база
↓
Подход к нужной палатке
↓
Взаимодействие
↓
Выбор миссии / дрона / снаряжения
↓
Фургон
↓
Посадка
↓
Кат-сцена перехода
↓
Карта миссии
↓
Управление дроном
↓
Выполнение задания
↓
Результат миссии
↓
Дебрифинг
↓
Возврат на базу — сцена Bunker(main)

3. Что уже готово

Текущий игровой цикл сцен (реализован):

Boot → MainMenu → кнопка «Играть» → Bunker(main) (игрок стоит на точке спавна) →
ходьба по бункеру → посадка на стул (E) → терминал компьютера (E) → рабочий стол
ComputerOS → иконка «Миссии» → выбор миссии → сцена миссии → дебрифинг →
Bunker(main) с восстановлением состояния (игрок снова сидит за компьютером).

Камеры-заглушки фона (устранение технической надписи Unity «No cameras rendering»):
В сцене Boot.unity добавлен объект BootCamera (Camera: Clear Flags = Solid Color, чёрный фон,
Culling Mask = Nothing, Depth = -100) — рисует чёрный фон позади UI при запуске игры, где раньше
не было ни одной камеры. В сцене TerrainDemoScene добавлен объект DebriefCamera (Camera:
Clear Flags = Solid Color, чёрный фон, Culling Mask = Nothing), по умолчанию выключен; его
включает DebriefController при показе экрана результатов (Debrief) и выключает при возврате
на базу, когда камера дрона отключается. В сцене PreparingDemo при крушении дрона (окно
«Вы разбились» CrashPanel) тоже не остаётся активной камеры: камера дрона уничтожается вместе с
дроном, и Unity выводил надпись «No cameras rendering», которая перекрывала кнопки CrashPanel.
Для этого в MissionUIManager добавлено поле fallbackCamera (Fallback Camera), которое при
ShowCrashPanel() включает объект-заглушку DebriefCamera (Camera: Clear Flags = Solid Color, чёрный
фон, Culling Mask = Nothing), а при ResumeGame() выключает её. Эти камеры-заглушки убирают надпись
«No cameras rendering» в окне Game во всех ситуациях.

Кнопка «Играть» в MainMenu (MainMenuController.OnOpenMissions) больше НЕ открывает
меню выбора миссий: она ставит MissionContext.ReturnedFromMission = false и загружает
сцену Bunker(main) через SceneLoader.Instance.LoadAsync("Bunker(main)"). Выбор миссии
теперь доступен только внутри бункера через компьютер (MissionMenuController).

FPS-база
В Assets/Scenes/player.unity уже есть:

объект Player;

CharacterController;

PlayerController;

SWAT-модель;

FPS-камера;

камера на уровне глаз;

гравитация;

ходьба;

вращение мышью;

Animator;

состояния Idle, Walk, WalkBackward, StrafeLeft, StrafeRight (бег убран); в Animator теперь передаётся параметр Strafe (горизонтальный ввод A/D) для ходьбы боком;

начата работа над механикой сидения за компьютером: добавлены параметры Animator
IsSitting и IsTyping и состояния SitDown / SitIdle / SitTyping (клипы Stand To Sit.fbx,
Sitting.fbx, Sit To Type.fbx); в PlayerController.cs добавлены методы SetSitting(bool)
и SetTyping(bool); создан скрипт ChairInteractable (Assets/Scripts/Interaction/) для
посадки за компьютер по E и вставания по Пробелу; при посадке скрипт отключает коллайдер
стула (Chair_Blocker) и полностью выключает CharacterController игрока (гравитация в
PlayerController игнорируется через isSitting), а при вставании включает их обратно и
телепортирует игрока на StandPoint; во время сидения вращение камеры (мышью) доступно —
оператор может осматриваться, а движение (WASD) и гравитация заблокированы;

четыре невидимые границы:

Bounds_West;

Bounds_East;

Bounds_South;

Bounds_North;

окружение военной базы;

Directional Light;

URP camera/light components.

Новая сцена — Bunker(main):
В Assets/Scenes/Bunker(main).unity база перенесена в военный бункер. Есть: Player
(CharacterController + PlayerController + PlayerInteractionDetector), Main Camera, Directional Light
и ассет бункера Bunker07Anim (Assets/Models/Buildings/Base_v1/source/Bunker07Anim.fbx, инстанс со Scale 10).

Физика бункера НАСТРОЕНА:
- Collision_SHELL — физическая оболочка: COL_Floor (пол), COL_Walls (4 стены: North/South/East/West),
  COL_Ceiling (потолок). Все BoxCollider, Is Trigger = off, подогнаны по Bounds мешей.
- Блокеры крупных объектов (BoxCollider по Bounds): Blockers_Doors (двери), Blockers_Safe (сейфы),
  Blockers_Console (пульт ControlPanel + компьютер/серверная станция ComputerNCamGEo),
  Blockers_ACUnits (кондиционеры ACUnit/ACUnit1), Blockers_Furniture (стулья Chair*, шкафы CupBoard*).
- Мелкий мусор (бумаги Paper*, кассеты Cassete*, провода Cables, клавиатуры Keyboard*, мыши)
  намеренно оставлен БЕЗ коллайдеров.
- Interact_OperatorTerminal — терминал оператора на мониторе (layer Interactable,
  BoxCollider Is Trigger = off, скрипт ComputerInteractable). Открытие ОС ComputerOS — по E,
  если игрок сидит и прицел направлен на терминал; закрытие — по E или Esc в любой момент,
  независимо от направления взгляда (обрабатывается в ComputerUIController). Появление с
  анимацией зума и прозрачности.
- InteractionCanvas и EventSystem перенесены из player.unity; ссылки uiController назначены.
- Позиция Player выровнена по полу (Y = -1.74).

Осталось в настройке: невидимые границы периметра Bounds_* (в Bunker(main) пока отсутствуют).

Бег у игрока отключён: локация бункера слишком мала, бег выглядел нереалистично и ломал
анимацию. В PlayerController.cs убрана переменная runSpeed — скорость движения всегда равна
базовой speed (3). В PlayerAnimator.controller удалено только состояние Run (Idle, Walk и
WalkBackward сохранены; для движения боком по A/D добавлены состояния StrafeLeft и StrafeRight). FBX-файлы и клипы бега не удалялись — они лишь отвязаны от графа аниматора.

Дроновый прототип
В проекте уже присутствует отдельный набор систем:

физический полёт;

моторы и тяга;

FPV-камера;

камера разведывательного дрона с гимбалом;

зум;

ночное видение;

HUD;

захват целей;

РЭБ;

взрывы;

сброс бомб;

перехватчик;

цели;

техника;

здоровье техники;

модули техники;

патруль;

AI техники.

Миссионная и UI-заготовка
Есть код для:

ScriptableObject-описаний миссий;

реестра миссий;

выбора миссии в меню;

карточек миссий;

зависимостей между миссиями;

сохранения прогресса;

глобальных состояний;

асинхронной загрузки сцен;

экрана загрузки «LOADING...» при переключении сцен;

настроек (см. раздел 7: SettingsService, SettingsPanel, апплеры, звук, URP, РЭБ, управление дроном).

Текущая конфигурация миссий
Ассеты миссий: Assets/Resources/Missions/ (Mission1, Mission2); реестр: Assets/Resources/MissionRegistry.asset.
- Mission1 — «Тренировочная миссия» (id: training_mission) → сцена Assets/Scenes/PreparingDemo.unity, Default Drone: MavicReconConfig (разведывательный борт с гимбалом). Доступные борта (availableDrones): MavicReconConfig и SmallFpvHeatConfig — стартовый борт разведчик, ударный FPV берётся вторым.
- Mission2 — «Первая миссия» (id: mission_1) → сцена Assets/Scenes/FirstMission(Forest).unity, Default Drone: MavicReconConfig (разведчик). Доступные борта: MavicReconConfig + 4 × SmallFpvHeatConfig (в сцене Forest столько же ударных префабов в пуле DroneManager).
Mission2 открывается после Mission1: массив prerequisites у Mission2 содержит ссылку на Mission1 (проверено по ассету; отдельный пункт аудита «prerequisites пуст» не подтвердился).
Обе сцены добавлены в Build Settings. В Build Settings всего 5 сцен: Boot, MainMenu, Bunker(main), PreparingDemo, FirstMission(Forest). Сцены SampleScene и TerrainDemoScene из Build Settings удалены (мусор в билде).
Mission3 (id: mission_3) лежит в Assets/Resources/Missions/, но у неё пустые sceneName и defaultDrone, в MissionRegistry она не входит и не используется.
Сцена Assets/Scenes/FirstMission(SpizjenyiVarik).unity есть в проекте, но пока не привязана к миссии.

Сцена PreparingDemo настроена на завершение миссии и показ дебрифинга:
- Объект MissionManager (в корне сцены) несёт скрипты MissionController и DebriefController.
- MissionController.droneManager ссылается на DroneManager (объект GameSystems). В DroneManager заполнен
  пул allDronesInScene: GimbalDrone Variant → MavicReconConfig (разведчик) и StrikeDrone 1 →
  SmallFpvHeatConfig (ударный FPV).
- DebriefController ссылается на UI дебрифинга: панель Canvas/DebriefPanel, заголовок DebriefTitle,
  статистику DebriefStats, кнопку возврата ReturnButton и камеру-заглушку DebriefCamera
  (по умолчанию выключена, включается на экране результатов).
- Завершение миссии: Mission1 (objectiveType DestroyAllTargets, targetCount 1) завершается при
  уничтожении цели potujnojet; DebriefController показывает результат, а ReturnToMenu() возвращает
  игрока в Bunker(main).
- Особенность тренировочной миссии: при уничтожении ЛЮБОГО дрона (событие GameEvents.DroneExploded,
  подписка MissionController.OnDroneDestroyed в OnEnable/OnDisable) вместо мгновенной перезагрузки
  сцены показывается окно «Вы разбились» (CrashPanel): OnDroneDestroyed запускает корутину
  DelayedCrashPanel, которая через 0.5 с реального времени (WaitForSecondsRealtime, не зависит от
  Time.timeScale) проверяет isEnded и, если миссия уже завершена, не показывает окно; иначе находит
  MissionUIManager через FindAnyObjectByType<MissionUIManager>() и вызывает ShowCrashPanel(). Кнопка «Перезапустить»
  (RestartMission) перезагружает PreparingDemo через SceneLoader.Instance.LoadAsync("PreparingDemo"),
  кнопка «Выйти в бункер» (ReturnToBunker) загружает Bunker(main). Работает бесконечный респавн, даже
  если в запасе остались другие дроны. В остальных миссиях (например FirstMission(Forest)) потеря всех
  дронов (droneManager.GetAliveDronesCount() <= 0 в LateUpdate) по-прежнему ведёт к провалу
  EndMission(MissionResult.Failed).

Меню паузы и окно крушения (MissionUIManager): в сценах PreparingDemo и FirstMission(Forest) добавлен
объект UIManager со скриптом MissionUIManager (Assets/Scripts/Interaction/MissionUIManager.cs). По Esc
(Keyboard.current.escapeKey.wasPressedThisFrame), пока окно крушения не активно и игра не в состоянии
Debrief, переключается PausePanel (кнопки «Продолжить», «Выйти в бункер»): при открытии Time.timeScale = 0
и курсор разблокируется, при закрытии Time.timeScale = 1 и курсор блокируется. CrashPanel («Вы разбились»,
кнопки «Перезапустить», «Выйти в бункер») показывается по событию крушения дрона в тренировочной миссии
(ShowCrashPanel). Панели паузы и крушения строго взаимоисключающие с дебрифингом: Update() и
ShowCrashPanel() делают ранний return, если GameStateMachine.Current == GameState.Debrief, поэтому поверх
DebriefPanel больше не открываются PausePanel и CrashPanel. Дополнительно MissionUIManager в OnEnable/OnDisable
подписывается на GameStateMachine.StateChanged, и метод OnStateChanged при переходе в GameState.Debrief
принудительно сбрасывает Time.timeScale = 1 и выключает pausePanel и crashPanel — даже если CrashPanel
успел включиться в момент, когда крушение дрона совпало с выполнением цели, он скрывается и не
накладывается на DebriefPanel. Кнопка «Выйти в бункер» (ReturnToBunker) перед
загрузкой Bunker(main) ставит MissionContext.ReturnedFromMission = true, из-за чего игрок возвращается в
бункер сидя за компьютером (BunkerStateRestorer), как и при возврате из дебрифинга, а не стоя на точке
спавна. Кнопки PausePanel и CrashPanel стилизованы единообразно с кнопкой DebriefPanel/ReturnButton:
серый фон (Image color 0.25/0.25/0.25) и белый текст.

Скрытие HUD при открытии панелей (MissionUIManager.SetHudVisible): MissionUIManager получил статический
флаг IsHudHidden и метод SetHudVisible(bool visible). uGUI-элемент инструкции Canvas/InstructionText
(поле instructionText у MissionUIManager; у объекта выключен Raycast Target) скрывается через SetActive,
а IMGUI-HUD дрона (ReconHUD, StrikeHUD, EWReceiver, IntersectorHUD) — через ранний return в начале
OnGUI(), когда MissionUIManager.IsHudHidden == true. SetHudVisible(false) вызывается при открытии паузы
(TogglePause), показе окна крушения (ShowCrashPanel) и переходе в Debrief (OnStateChanged);
SetHudVisible(true) — в ResumeGame() и при закрытии паузы. В OnEnable() флаг сбрасывается в false,
поэтому после рестарта миссии и возврата из бункера HUD снова видим. Дополнительно те же
IMGUI-HUD-скрипты в начале OnGUI() делают ранний return, если SceneLoader.Instance != null и
SceneLoader.Instance.IsLoading == true, поэтому во время экрана загрузки «LOADING...» прицелы и текст
не рисуются поверх чёрного экрана.

Сцена FirstMission(Forest) настроена так же, как PreparingDemo — на завершение миссии и показ дебрифинга:
- Объект MissionManager (в корне сцены) несёт скрипты MissionController и DebriefController.
- MissionController.droneManager ссылается на DroneManager (корневой объект DroneManager). В DroneManager
  заполнен пул allDronesInScene: GimbalDrone Variant → MavicReconConfig (разведчик) и четыре ударных дрона
  StrikeDrone 1 / (1) / (2) / (3) → SmallFpvHeatConfig (ударный FPV).
- DebriefController ссылается на UI дебрифинга: панель Canvas/DebriefPanel, заголовок DebriefTitle,
  статистику DebriefStats, кнопку возврата ReturnButton и камеру-заглушку DebriefCamera
  (по умолчанию выключена, включается на экране результатов).
- В сцене создан объект EventSystem (EventSystem + InputSystemUIInputModule) — обрабатывает клик по кнопке возврата.
- Завершение миссии: Mission2 (objectiveType DestroyAllTargets, targetCount 2) завершается при уничтожении
  двух враждебных целей (БТР прикрытия и РСЗО); DebriefController показывает результат, а ReturnToMenu()
  возвращает игрока в Bunker(main).
- Логикой этой миссии управляет сторонний скрипт AmbushMissionDirector (Assets/Scripts/Managers/AmbushMissionDirector.cs),
  а не стандартный MissionController. Он ведёт сценарий «наблюдение → уничтожить БТР → погоня за отступающей
  РСЗО» и по завершении вызывает MissionController.CompleteMission(result, targets), который фиксирует
  результат, пишет прогресс (ProgressService.CompleteMission) и переводит FSM в Debrief. В Start() директор
  отключает автозавершение MissionController (autoComplete = false), чтобы тот не дублировал условия победы.
- Добавлено меню паузы (PausePanel по Esc): объект UIManager со скриптом MissionUIManager и панель
  Canvas/PausePanel с кнопками continue (ResumeGame) и exit (ReturnToBunker). Поля Fallback Camera и
  Crash Panel оставлены пустыми — в первой миссии потеря дронов ведёт к обычному провалу.

Возврат на базу после миссии
Завершение миссии доводит игрока до дебрифинга, после которого он возвращается не в
MainMenu, а на базу — в сцену Assets/Scenes/Bunker(main).unity. Логика находится в
Assets/Scripts/Data/DebriefController.cs, метод ReturnToMenu(): панель дебрифинга
скрывается, состояние переводится из Debrief в Gameplay (через разрешённый переход
Debrief → Loading → Gameplay, так как FSM не допускает Debrief → Gameplay напрямую),
после чего вызывается SceneLoader.Instance.LoadAsync("Bunker(main)").
ReturnToMenu() защищён от повторного вызова флагом isReturning (повторный вызов
игнорируется), а SceneLoader.LoadAsync() — флагом isLoading, который сбрасывается
после завершения загрузки сцены. Это устранило баг двойной загрузки сцены
Bunker(main) (логи показывали два вызова ReturnToMenu() и LoadAsync() в одну и ту же
миллисекунду), из-за которого игрок после возврата с миссии телепортировался на точку
спавна стоя, а не оставался сидеть за компьютером.
Сцена Bunker(main) добавлена в Build Settings, поэтому загрузка проходит без ошибки.
Русский текст дебрифинга («МИССИЯ ВЫПОЛНЕНА», «МИССИЯ ПРОВАЛЕНА», статистика) сохранён,
сам файл переведён из Windows-1251 в UTF-8 без BOM — как и остальные C#-скрипты проекта.

Восстановление состояния бункера после возврата
При возврате с миссии игрок больше не появляется на стартовой точке стоя. Состояние бункера
восстанавливается автоматически:

DebriefController.ReturnToMenu() перед загрузкой сцены ставит флаг
MissionContext.ReturnedFromMission = true (Assets/Scripts/Data/MissionContext.cs).

В сцене Bunker(main) на объекте Player висит скрипт BunkerStateRestorer
(Assets/Scripts/Interaction/BunkerStateRestorer.cs). В Start() при поднятом флаге он с задержкой
в один кадр (чтобы отработали все Start, включая блокировку курсора в PlayerController):

сажает игрока на стул — ChairInteractable.Interact() (телепорт на SitPoint, отключение
CharacterController и коллайдера стула, анимация IsSitting);

открывает рабочий стол ComputerOS — ComputerUIController.OpenDesktop();

поверх рабочего стола открывает панель миссий — MissionMenuController.OpenMissionMenu();

сбрасывает флаг ReturnedFromMission = false.

Скрипт выставляет публичное свойство IsRestoring = true в Awake() (при поднятом флаге
ReturnedFromMission) и сбрасывает его в false в конце корутины RestoreStateRoutine().
SceneLoader после загрузки сцены ждёт, пока IsRestoring станет false, поэтому экран
«LOADING...» остаётся поверх сцены до полного восстановления состояния бункера.

Прогресс миссий при этом актуален: MissionController.EndMission() при успешном завершении вызывает
ProgressService.CompleteMission(), а OpenMissionMenu() пересоздаёт карточки через
MissionSelectScreen.Populate(), которая заново читает ProgressService — галочки «Пройдено»
отображаются сразу.

Экран загрузки при переключении сцен
При переключении сцен (например, возврат с миссии в Bunker(main)) SceneLoader
(Assets/Scripts/Core/SceneLoader.cs) показывает чёрный экран «LOADING...». В LoadAsync()
перед LoadSceneAsync вызывается ShowLoading() (CanvasGroup alpha = 1, blocksRaycasts = true).
После завершения активации новой сцены (op.isDone) экран остаётся непрозрачным ещё на
пост-задержку postLoadDelay (1.5 с, таймер на Time.unscaledDeltaTime), а при возврате в
бункер — до завершения восстановления состояния BunkerStateRestorer.IsRestoring (таймаут
restoreWaitTimeout = 3 с). Только после этого вызывается HideLoading() (alpha = 0,
blocksRaycasts = false) и сбрасывается флаг isLoading. Это скрывает «швы» перехода:
прогрузку текстур миссии, появление игрока стоя и открытие рабочего стола/меню миссий.
Пустая сцена при переходе больше не мигает. UI экрана загрузки — Panel на весь экран с
текстом «LOADING...», дочерняя к объекту SceneLoader (переживает смену сцен благодаря
DontDestroyOnLoad); ссылка назначается в поле Loading Screen инспектора SceneLoader.
Поля postLoadDelay и restoreWaitTimeout настраиваются в инспекторе SceneLoader.

4. Что существует в коде, но пока не соединено с базой
Следующие подсистемы присутствуют в проекте, но не подключены к основной FPS-сцене player.unity как единый игровой цикл:

DroneManager;

MissionController;

DroneConfig;

drone prefabs;

vehicle AI;

HUD дронов;

EW-система;

mission UI;

MissionContext;

ProgressService.

Основная сцена базы содержит PlayerController, но не содержит полного набора миссионных и дроновых компонентов.

5. Что ещё не начато или не найдено
В текущем аудите не найден законченный функциональный слой:

палатки;

интеракция с палатками;

действие подхода и нажатия E;

магазин дронов;

магазин пресетов;

пополнение боеприпасов;

фургон как объект игрового цикла;

посадка в фургон;

выход из фургона;

кат-сцена через фургон;

полноценная вторая карта миссии;

связка player.unity → миссионная сцена;

полноценный debrief UI.

В Input Actions уже есть действие Interact. Написана базовая система взаимодействия
(Assets/Scripts/Interaction/): интерфейс IInteractable, PlayerInteractionDetector (луч из камеры,
клавиша E, слой Interactable), InteractionUIController (окна «Магазин» и «Карта миссий»),
готовые интерактивные объекты HangarInteractable, MissionBoardInteractable и
ChairInteractable (посадка за компьютер; управляет коллайдером стула и точкой вставания StandPoint).
Система подключена к Bunker(main): создан терминал Interact_OperatorTerminal, ссылки uiController назначены.
Интерфейс компьютера ComputerOS работает как переключатель (Toggle). Открытие — по E, когда прицел
направлен на терминал и игрок сидит: ComputerInteractable вызывает ComputerUIController.OpenDesktop()
(панель плавно появляется CanvasGroup.alpha 0→1 и зумится localScale 0.1→1 за 0.3 с, курсор
разблокируется, Time.timeScale = 0). Закрытие — по E или Esc независимо от направления взгляда:
ComputerUIController.Update() вызывает CloseDesktop() (alpha = 0, Time.timeScale = 1,
курсор блокируется, StandUp() НЕ вызывается — игрок ОСТАЁТСЯ сидеть; Animator принудительно
сбрасывается в позу SitIdle через PlayerController.ForceSitIdle(), чтобы застывшая анимация
посадки не доигрывалась). Игрок встаёт со стула только по клавише Пробел через
ChairInteractable.Update(). Esc закрывает окна по очереди: первое нажатие закрывает меню миссий
(возврат на рабочий стол), второе нажатие (когда меню миссий уже закрыто) закрывает сам рабочий
стол, но игрок остаётся сидеть. ComputerUIController игнорирует Esc, пока меню миссий открыто
(MissionMenuController.IsOpen), а также в течение 0.2 с после его закрытия (LastClosedTime), поэтому
порядок выполнения Update() двух скриптов не влияет на результат.

На рабочем столе ComputerOS есть интерактивная иконка-кнопка «Миссии» (скрипт DesktopIcon,
Assets/Scripts/Interaction/DesktopIcon.cs). По клику мышью поверх рабочего стола ComputerOS
открывается полнофункциональная панель выбора миссий из MainMenu.unity (префаб MissionSelectPanel
с MissionSelectScreen и карточками MissionCard), управляемая скриптом MissionMenuController
(Assets/Scripts/Interaction/MissionMenuController.cs). Рабочий стол ComputerOS остаётся видимым на
фоне. Старая статичная картинка MissionBoardWindow удалена. Кнопка «Назад» закрывает панель и
возвращает на рабочий стол, а кнопка «Начать» запускает миссию через MissionContext и SceneLoader.

Экран выбора миссий переведён на схему master-detail (реализовано):
слева горизонтальный ряд карточек MissionCard, справа постоянная колонка MissionDetailsPanel
(префаб MissionSelectPanel, скрипт Assets/Scripts/MainMenu/MissionDetailsPanel.cs). Клик по
карточке больше НЕ запускает миссию: он подсвечивает карточку (рамка SelectedFrame) и заполняет
панель деталей. Миссия стартует только по большой кнопке «ЗАПУСТИТЬ МИССИЮ» внизу панели деталей.
Сверху появилась шапка: заголовок «ВЫБОР МИССИИ», подзаголовок «ОПЕРАТИВНЫЙ ТЕРМИНАЛ · ОПЕРАЦИЙ: N»
и общий счёт из сохранения; кнопка «Назад» перенесена в шапку и стала белой.
Панель деталей показывает: баннер с превью (или ID миссии), статус-чип («ВЫПОЛНЕНО» / «НЕ ВЫПОЛНЕНО» /
«ЗАБЛОКИРОВАНО»), название, описание из ассета, сетку статистики (лучшее время, счёт, прохождений,
целей), чек-лист целей операции, строку техники (роль дрона и число бортов), тактическую подсказку
или причину блокировки, и кнопку запуска. Состояние «миссия не пройдена» показывает прочерки и
условия выполнения вместо пустоты; состояние «пройдена» — лучшие результаты из progress.json.
Обработка Esc стала трёхступенчатой: первое нажатие закрывает панель деталей, второе — всю панель
выбора миссий, третье — рабочий стол ComputerOS.
Статистика прохождения хранится в progress.json: в SaveData добавлен список records из MissionRecord
(id, completed, bestTime, lastTime, bestScore, lastScore, completions, attempts, bestTargets).
MissionController.EndMission вызывает ProgressService.RecordResult для любого результата (успех и
провал), а награда считается единой функцией ProgressService.ComputeScore (rewardPoints + 25 за
каждую уничтоженную цель + до 50 очков за скорость при наличии лимита времени), поэтому дебрифинг и
панель деталей показывают одинаковый счёт. Старые сохранения читаются без миграции: отсутствующие
поля заполняются значениями по умолчанию, а для уже пройденных миссий без времени панель показывает
прочерки и пояснение. Ассеты Mission1 и Mission2 получили имена «Тренировка» и «БТР и РСЗО», описания
и тактическую подсказку (поле tacticalHint в MissionDefinition).

6. Важные ограничения для будущих задач
Проект находится на стадии обучения и прототипирования. Поэтому:

сначала проверять существующие связи в сцене и префабе;

не заменять старые системы автоматически;

не переводить весь проект на другой Input System без разрешения;

не менять физику дронов без отдельного согласования;

не удалять demo-ассеты и recovery-файлы;

не считать сторонние ассеты ошибкой;

не запускать Play Mode при read-only-аудитах;

изменения вносить только после явного запроса.

7. Система настроек (реализовано)
Настройки хранятся одним JSON и применяются к игре набором апплеров. Панель открывается в главном
меню и в меню паузы бункера (SettingsPanel — дочерний объект PausePanel в Bunker(main)); в миссии
настроек пока нет (кнопка в PausePanel требует модального стека — следующий этап).

7.1 Хранение и сервис
Assets/Scripts/Data/SettingsData.cs — модель: graphics, audio, controls, gameplay, version.
Assets/Scripts/MainMenu/SettingsService.cs — singleton (DontDestroyOnLoad), файл
Application.persistentDataPath/settings.json, атомарная запись (tmp + File.Replace + .bak),
debounce 0.5 с, миграция схемы. Текущая версия схемы — 3; при миграции с версии 2 и ниже
windowMode наследуется из старого fullscreen (у кого было окно — остаётся окном).
Публичный API: EnsureInstance(), ApplyAll(), ApplyInstant(), ApplyHeavy(), ResetAll(), MarkDirty().
Сервис создаёт Bootstrapper (Boot.unity) либо лениво SettingsPanel.Start().

7.2 Панель и HUD
Assets/Scripts/UI/SettingsPanel.cs — 4 вкладки (Графика, Управление, Звук, Интерфейс), строки
собираются из SettingDescriptor; кнопка «Сброс» → ResetSettings() → SettingsService.ResetAll().
Assets/Scripts/UI/SettingsListBuilder.cs — строки Slider / Toggle / Dropdown. «Тяжёлые» настройки
графики (все слайдеры вкладки «Графика») применяются не на каждый пиксель, а по отпусканию слайдера.
Assets/Scripts/UI/SliderReleaseNotifier.cs — ловит OnEndDrag / PointerUp слайдера.
Assets/Scripts/UI/FpsCounterHud.cs — счётчик FPS в правом верхнем углу (OnGUI), сам создаёт себя
через RuntimeInitializeOnLoadMethod + DontDestroyOnLoad; рисуется только в состоянии Gameplay,
уважает прозрачность/масштаб HUD.
Assets/Scripts/Settings/HudStyleService.cs — резолвер стиля HUD: масштаб, прозрачность, размер
прицела, толщина линий, показ рамки захвата; Begin()/End() для GUI.matrix.

7.3 Звук
Assets/Resources/Audio/MainMixer.mixer — микшер проекта (создаётся из меню Tools/DroneSimulator/Impact/3).
Иерархия групп: Master → { Music, SFX → { DroneMotors, EW }, UI, Voice }. Expose-параметры (имена совпадают
с константами AudioBus): MasterVolume, MusicVolume, SfxVolume, MotorsVolume, EwVolume, UiVolume, VoiceVolume.
Громкость всех групп выставлена в 0 дБ в стартовом снапшоте Snapshot, вид микшера содержит все 7 групп.
Assets/Scripts/Settings/AudioBus.cs — загружает Resources/Audio/MainMixer, раздаёт группы
Master / Music / SFX / DroneMotors / EW / UI, переводит линейную громкость в дБ (0 → −80 дБ).
Assets/Scripts/Settings/AudioApplier.cs — применяет 7 ползунков; если микшера нет, работает fallback
через AudioListener.volume и пишет один warning в консоль.
Assets/Scripts/Core/EffectService.cs — пул из 16 AudioSource (DontDestroyOnLoad «EffectAudioPool»)
с outputAudioMixerGroup = SFX; PlayClipAtPoint больше не используется.
Assets/Scripts/Audio/DroneMotorAudio.cs — гул моторов (процедурный луп, если клип не назначен),
громкость и питч от DroneMotors.CurrentThrust01 и isMotorStarted.
Assets/Scripts/Audio/EWAudio.cs — треск помех, громкость от EWReceiver.jamIntensity.
Префабы дронов (Assets/Prefabs/Drones/): на корне Audio Source (DroneMotors) + DroneMotorAudio,
на камере Audio Source (2D, EW) + EWAudio; ссылки на DroneMotors / DroneFlightController / EWReceiver назначены.
Взрыв: Assets/Audio/SFX/Explosion.wav (процедурный синтез), назначен в explosionSound всех трёх конфигов дронов.

7.4 Графика (URP)
Assets/Scripts/Settings/GraphicsApplier.cs — на старте создаёт рантайм-клон активного URP-ассета
(оригинальный ассет не правится) и применяет renderScale, shadowDistance (20 м бункер / 300 м миссия),
shadowCascadeCount, msaaSampleCount, мягкие тени, mainLightShadowmapResolution. Клон назначается в
QualitySettings.renderPipeline текущего уровня и возвращается назад по Application.quitting.
Мягкие тени в этой версии URP не имеют публичного сеттера, поэтому пишутся рефлексией в
m_SoftShadowsSupported.
Assets/Scripts/Settings/CameraApplier.cs — FPS-камера бункера: farClipPlane + renderPostProcessing.
Камеры дронов: farClipPlane, renderShadows (по «Тени дрона»), antialiasing. Переприменяется на
sceneLoaded и на GameEvents.DroneSwitched.
Assets/Scripts/Settings/SettingsScope.cs — IsMission (в сцене есть DroneManager); от него зависит
выбор дистанции теней.

7.5 Постобработка и РЭБ
Assets/Scripts/Settings/PostFxRouter.cs — [DefaultExecutionOrder(-100)], единственный владелец Volume.
Создаёт DontDestroyOnLoad-объект PostFxRouter с тремя глобальными Volume и рантайм-профилями:
PostFX_EW (priority 10: ChromaticAberration + Vignette + FilmGrain), PostFX_NV (20: ColorAdjustments),
PostFX_Settings (30: MotionBlur). Раздаёт компоненты через Bind(EWReceiver)/Bind(NightVisionController).
Assets/Scripts/EW/EWReceiver.cs — больше не ищет Volume (авто-поиск удалён). Публично: jamIntensity,
VisualScale, SetVisualScale(0..1), SetEffects(...). Настройка «Искажения РЭБ» (ewVisualDistortion)
масштабирует CA/Vignette/Grain и IMGUI-«снег»; при 0 остаются только увод управления и предупреждение
[HUD: EW JAMMING]. Дрожание FOV стало аддитивным: EWReceiver вызывает FPVZoom.SetFovOffset(), а не
переписывает fieldOfView. reduceDangerousEffects отключает «снег» и полосы разрыва.
Assets/Scripts/Camera/NIghtVisionController.cs — цвет ПНВ из graphics.nightVisionColor
(0 зелёный / 1 белый / 2 янтарный), яркость = DroneConfig.nvPostExposure × nightVisionGain.
Assets/Scripts/Camera/FPVZoom.cs — единственный владелец Camera.fieldOfView дрона; лестница зума
масштабируется от базового FOV из настроек (droneFov), внешнее дрожание добавляется как fovOffset.

7.6 Управление дроном
Assets/Scripts/Physics/DroneFlightController.cs — ввод читается через RefreshInputSettings():
droneStickSpeed, droneCenteringSpeed, throttleSpeed, throttleMode (0 — самовозврат газа к 0.5,
1 — фиксация) и droneInvertY. Настройки применяются на входе, до дрейфа РЭБ и до PID. Физика, PID,
аэродинамика и ApplyDroneProfile() не меняются.
Assets/Scripts/Camera/DroneCameraModule.cs — gimbalSpeed и gimbalInvertY из настроек
(при отсутствии настроек — DroneConfig.tiltSpeed).
Assets/Scripts/Camera/FPVCameraShake.cs — амплитуды тряски умножаются на gameplay.cameraShakeIntensity;
при 0 камера возвращается в исходную позу.

7.7 Что раньше делалось руками, а теперь автоматизировано
Раньше эти шаги требовали ручной работы в редакторе. Сейчас они выполняются Editor-скриптами
из меню Tools/DroneSimulator (см. раздел 8):
1) Аудиомикшер Assets/Resources/Audio/MainMixer.mixer с группами Master → { Music, SFX →
{ DroneMotors, EW }, UI, Voice } и Expose-параметрами MasterVolume, MusicVolume, SfxVolume,
MotorsVolume, EwVolume, UiVolume, VoiceVolume — создаётся скриптом AudioMixerBuilder
(микшер уже лежит в проекте).
2) Audio Source + DroneMotorAudio на корень и Audio Source (2D) + EWAudio на камеру дрона —
расставляет скрипт DroneAudioBinder на всех префабах Assets/Prefabs/Drones/ (уже расставлено).
3) Префабы взрыва и бомбы, а также звук взрыва — создаёт скрипт ImpactVfxBuilder.
4) URP-клон в редакторе создавать не нужно: он делается в рантайме.
Если микшера нет, игра по-прежнему работает: общая громкость идёт через AudioListener.volume,
категорийные ползунки молчат, в консоли один warning [AudioBus].

7.8 Что пока не подключено
droneFog (туман миссии), droneLensDistortion (нужен целевой параметр), duckOnMenus (приглушение в меню),
trainingHints, osdLevel, showSubtitlesOnPause, gimbalAutoCenter, ewMotionComfort, ewPostFxQuality
(используется только как «0 = выключить пост-эффекты»), nightVisionColor (работает, но без строки в UI).
Моно-режим и «Чувствительность мыши (дрон)» удалены из UI (поля в SettingsData остались).

8. Единая палитра интерфейса (терминальный стиль)
Весь UI проекта приведён к одной палитре. Источник истины — статический класс
Assets/Scripts/UI/UIColorPalette.cs (пространство имён Assembly-CSharp, обращается как UIColorPalette).

Цвета палитры:
- PanelBackground #1E2024, alpha 0.90 — фон панелей и карточек;
- PanelSurface #2A2D33, alpha 0.90 — вложенные поверхности (панель в панели, баннер, тайл);
- PanelHeader #17191C, alpha 0.95 — шапки панелей;
- Overlay #000000, alpha 0.62 — затемнение под модальным окном, слой «ЗАБЛОКИРОВАНО»;
- Accent #D94A4A — кнопки действия (обычная кнопка = акцентный красный);
- AccentHover #E46363 и AccentPressed #A93737 — состояния кнопки;
- Success #73CC6B — успех (заголовок «МИССИЯ ВЫПОЛНЕНА», выполненная цель);
- Failure #E67373 — провал (заголовок «МИССИЯ ПРОВАЛЕНА», заблокированная миссия);
- TextPrimary #FFFFFF — основной текст;
- TextSecondary #A0A5B0 — подписи, подсказки, вторичный текст;
- Divider #FFFFFF alpha 0.16 — разделители на тёмном фоне.

Хелперы класса: FromHex, WithAlpha, Lighten, Darken, IsAcidic, SemanticiseSurface, SemanticiseText.
SemanticiseText / SemanticiseSurface заменяют «кислотные» цвета (насыщенность ≥ 0.45 и яркость ≥ 0.45)
на ближайший цвет палитры: зелёный → Success, красный → Failure/Accent, синий → TextSecondary.

Кто использует палитру:
- DebriefController — цвет заголовка результата (Success / Failure) вместо Color.green / Color.red;
- префабы Assets/Prefabs/UI/SettingsPanel.prefab, MissionSelectPanel.prefab, MissionCard.prefab;
- сцены Assets/Scenes/Bunker(main).unity, PreparingDemo.unity, FirstMission(Forest).unity;
- сериализованные поля цветов: MissionDetailsPanel (completedColor / pendingColor / lockedColor),
  SettingsPanel (selectedTabColor / normalTabColor), SettingsListBuilder (labelColor / trackColor /
  fillColor / handleColor / checkColor).

Применение и проверка — меню Tools/DroneSimulator/UI: «1. Apply Terminal Palette» перекрашивает префабы и
сцены по правилам (кнопка → Accent, панель → PanelBackground/PanelSurface, шапка → PanelHeader,
затемнение → Overlay, текст → TextPrimary/TextSecondary, кислотные цвета → семантика палитры) и пишет
полный отчёт в Logs/ui_theme_report.txt; «2. Open Palette Report» выводит отчёт в консоль.
Правила описаны в Assets/Scripts/Editor/UIThemeApplicator.cs.

8.1 Глобальный цвет шрифта (исправлено)
До этой итерации почти весь текст в игре (меню, бункер, миссии, пауза, дебрифинг) оставался
ядовито-зелёным, хотя UI уже был перекрашен по палитре. Причина была не в компонентах, а в материале
шрифта: у встроенного саб-ассета «LiberationSans SDF Material» (Assets/UnityTechnologies/ParticlePack/
TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset, guid 8f586378b4e144a9851e7b34d9b748ee,
fileID 2180264) параметр _FaceColor равнялся r0.387 g0.925 b0.052. Шейдер TMP_SDF считает
faceColor = _FaceColor × цвет вершины, поэтому все TMP-тексты с белым цветом вершины рисовались
кислотно-зелёными.

Что сделано (Editor-скрипт Assets/Scripts/Editor/TmpColorFixer.cs, меню
Tools/DroneSimulator/Text/1. Fix TMP Green Text):
- _FaceColor принудительно выставлен в Color.white у материала шрифта LiberationSans SDF Material и у всех
  остальных материалов TMP-шейдеров (включая пресеты Outline / Drop Shadow и инстансы материалов);
- во всех префабах Assets/Prefabs и в сценах Bunker(main), MainMenu, PreparingDemo, FirstMission(Forest),
  Boot у каждого TMP_Text / TextMeshPro / TextMeshProUGUI проверен fontSharedMaterial (зелёный
  _FaceColor → белый) и сериализованные цвета: m_fontColor, m_fontColor32, m_faceColor,
  m_underlineColor, m_strikethroughColor и четыре угла m_fontColorGradient;
- кислотный зелёный заменяется на UIColorPalette.TextPrimary (белый), а для семантических элементов
  (success/completed/«выполн»/«успех» → Success, fail/locked → Failure); цвета палитры считаются
  целевыми и не перекрашиваются, альфа исходного цвета сохраняется, устаревшие кэши
  m_fontColor32 / m_faceColor синхронизируются с актуальным цветом — теперь сериализованные данные
  совпадают с тем, что рисуется;
- изменённые префабы и сцены пересохранены, отчёт пишется в Logs/tmp_color_fix_report.txt
  (пункт меню «Text/2. Open TMP Color Report» выводит его в консоль).

Итог: весь текст в игре соответствует единой палитре — белый (TextPrimary), серый (TextSecondary) и
акцентные цвета (Accent / Success / Failure). Скрипты палитры (UIThemeApplicator, UIColorPalette) не
менялись. Шаг входит в общую кнопку Tools/DroneSimulator/Run Full Stabilization (шаг 9).

9. Инструменты стабилизации (Editor)
Все правки этого этапа автоматизированы и запускаются из меню Tools/DroneSimulator.
Главная кнопка — Tools/DroneSimulator/Run Full Stabilization (StabilizationMaster.cs): последовательно
выполняет все девять шагов.

Пошагово:
- Stabilization/1. Reset TMP Font Materials (ProjectStabilizer.cs) — ищет TMP-тексты, у которых
  sharedMaterial отличается от дефолтного материала шрифта, возвращает дефолт, чистит массивы
  материалов и удаляет запечённый инстанс материала из сцены;
- Stabilization/2. Fix Crash Panel Typo (ProjectStabilizer.cs) — правит «Вы рызбились» → «Вы разбились»;
- Stabilization/3. Validate Mission Drone Links (ProjectStabilizer.cs) — проверяет, что в
  availableDrones всех ассетов миссий лежат только DroneConfig, и что defaultDrone — DroneConfig;
- Stabilization/4. Clean Build Settings (ProjectStabilizer.cs) — убирает SampleScene и TerrainDemoScene
  и явно перезаписывает ProjectSettings/EditorBuildSettings.asset (Unity не сохраняет этот файл сама);
- Impact/1. Build Explosion And Bomb (ImpactVfxBuilder.cs) — процедурный префаб взрыва (вспышка + дым),
  префаб бомбы, материалы и текстуры частиц, WAV-звук взрыва, назначение в конфиги дронов;
- Impact/2. Attach Drone Audio Sources (DroneAudioBinder.cs) — AudioSource + DroneMotorAudio на корень,
  AudioSource + EWAudio на камеру для всех префабов дронов;
- Impact/3. Build Audio Mixer (AudioMixerBuilder.cs) — создаёт/дополняет MainMixer.mixer (внутренний
  AudioMixerController Unity через рефлексию): группы, громкости в снапшоте, Expose-параметры, вид микшера;
- Impact/4. Verify Audio Mixer (AudioMixerBuilder.cs) — проверяет, что ресурс загружается, группы находятся
  и Expose-параметры записаны в ассет;
- UI/1. Apply Terminal Palette (UIThemeApplicator.cs) — перекраска префабов и сцен по палитре;
- Text/1. Fix TMP Green Text (TmpColorFixer.cs) — глобальная починка цвета текста: принудительный
  белый _FaceColor у материала шрифта «LiberationSans SDF Material» и у всех TMP-материалов, чистка
  зелёных переопределений (m_fontColor, m_fontColor32, m_faceColor, подчёркивание/зачёркивание, градиент)
  и проверка fontSharedMaterial у всех TMP_Text / TextMeshPro / TextMeshProUGUI в префабах Assets/Prefabs
  и сценах Bunker(main), MainMenu, PreparingDemo, FirstMission(Forest), Boot; отчёт —
  Logs/tmp_color_fix_report.txt; пункт «Text/2. Open TMP Color Report» выводит отчёт в консоль.

10. Назначение этих документов
Эти документы предназначены для передачи контекста в будущие сессии DSH.

Использование:

CONTEXT.md — быстро понять идею, статус и правила проекта.

ARCHITECTURE.md — понять структуру, системы, связи и известные проблемы.

INDEX.md — быстро найти нужный файл, сцену, скрипт или префаб.

SETTINGS_DESIGN.md — дизайн и обоснование системы настроек (первичный документ; фактические
детали реализации см. раздел 7 этого файла и раздел 5.12 ARCHITECTURE.md).

Перед технической задачей сначала определить нужную систему по INDEX.md, затем свериться с соответствующим разделом ARCHITECTURE.md.

11. Кодировки, HUD миссии, гейтинг и Input System (реализовано)
Этот раздел описывает итерацию, закрывшую пункты аудита по кодировкам, внутримиссионному HUD,
гейтингу миссий и переводу ввода дрона на новый Input System.

11.1 Кодировки скриптов (закрыто)
Все C#-скрипты проекта — UTF-8 без BOM, битой кириллицы в Header-атрибутах больше нет.
- 6 файлов были в Windows-1251 и пересохранены в UTF-8: TrainingManager.cs, TrainingGate.cs,
  HimarsStrike.cs, TargetPingSystem.cs, VehicleDust.cs, WheelRotator.cs.
- 2 файла с двойной кодировкой (mojibake) вылечены обратным проходом cp1251 → UTF-8:
  Assets/Scripts/TargetObjectScripts/VehicleSmartAI.cs и Assets/Scripts/Core/DamageSystem.cs
  (заголовки инспектора снова читаемы: [Header("Сенсоры и Слои")] и т. д.).
- Процедура сохранена как скрипт Tools/FixScriptEncodings.ps1 (запуск:
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools\FixScriptEncodings.ps1). Скрипт
  идемпотентен: cp1251-файлы перекодируются, а mojibake лечится только при валидном round-trip;
  исходные байты складываются в Logs/encoding_backup/.

11.2 Гейтинг миссий (закрыто)
- Mission2 (Assets/Resources/Missions/Mission2.asset) в массиве prerequisites ссылается на
  Mission1.asset (guid 61426ee32a5eb854cae5384762b6c235): вторая миссия заблокирована, пока
  не пройдена тренировка. Пункт аудита «prerequisites пуст» был основан на устаревших данных.
- Проверка автоматизирована: Tools/DroneSimulator/Missions/1. Validate Mission Gating
  (Assets/Scripts/Editor/MissionGateValidator.cs). Инструмент печатает содержимое prerequisites и
  симулирует сохранение в памяти (без записи progress.json): без пройденной Mission1
  ProgressService.IsUnlocked(Mission2) = False, с пройденной — True.
- Состояние «ЗАБЛОКИРОВАНО» на карточке миссии и в панели деталей берётся из того же
  ProgressService.IsUnlocked, дополнительных правок UI не потребовалось.

11.3 HUD миссии (MissionHud)
Скрипт Assets/Scripts/UI/MissionHud.cs, объект MissionHud (дочерний к Canvas) в сценах
PreparingDemo и FirstMission(Forest); ссылки droneManager и missionController назначены
Editor-скриптом Tools/DroneSimulator/UI/2. Add Mission HUD (Assets/Scripts/Editor/MissionHudBinder.cs).
HUD — IMGUI-плашка в левом нижнем углу и показывает:
- активный борт (N из M) и число живых бортов (DroneManager.ActiveDroneNumber / ActiveSlotCount /
  AliveDronesCount);
- боезапас (только если у активного борта есть DroneDropMechanism с зарядом: AmmoCount);
- прогресс целей «Уничтожено X / Y» (MissionController.DestroyedTargets / TargetCount);
- таймер: обратный отсчёт «ДО КОНЦА» при timeLimit > 0 (краснеет на последних 30 с), иначе
  прошедшее время миссии «ВРЕМЯ».
Стиль единый: цвета из UIColorPalette, масштаб/прозрачность/толщина — из HudStyleService
(Begin/End вокруг GUI.matrix, ApplyAlpha на фон и текст). Плашка уважает MissionUIManager.IsHudHidden
(пауза, крушение, дебрифинг) и SceneLoader.IsLoading (экран «LOADING...»).
MissionController получил публичные свойства только для чтения для HUD: Mission, DestroyedTargets,
TargetCount, IsEnded, ElapsedTime, TimeLimit, TimeRemaining; DroneManager — ActiveSlotCount,
AliveDronesCount, ActiveDroneNumber, GetActiveDrone().

11.4 Input System: карта Drone, адаптер GameInput, RebindService и UI привязки
Карта Drone добавлена в Assets/Input/InputSystem_Actions.inputactions (12 действий, 26 привязок):
- Throttle (W/S), Yaw (A/D), Pitch (Up/Down), Roll (Left/Right) — 1D-оси;
- Gimbal (R/F/Q/E) — 2D-вектор; Zoom (Z/X) — 1D-ось;
- PrimaryAction (Пробел) — контекстное действие: захват цели, сброс бомбы, детонация;
- NightVision (N), MotorStart (W);
- служебные действия, сохранившие прежние возможности: HoverToggle (H), AngleToggle (M),
  GimbalReset (C).
Адаптер Assets/Scripts/Input/GameInput.cs — статический класс, который строит такой же
InputActionAsset в коде (ассет лежит вне Resources, поэтому в рантайме источником истины служит код),
включает карту, применяет сохранённые переназначения и отдаёт значения через свойства:
ThrottleAxis / YawAxis / PitchAxis / RollAxis / GimbalAxis, ZoomStep, PrimaryActionPressedThisFrame,
MotorStartPressedThisFrame, NightVisionPressedThisFrame, HoverTogglePressedThisFrame,
AngleTogglePressedThisFrame, GimbalResetPressedThisFrame. Совпадение кода и ассета проверяет
Tools/DroneSimulator/Input/1. Validate Drone Input Map (Assets/Scripts/Editor/InputMapValidator.cs).
Контекстная логика Пробела сохранена, но конфликт аудита C7 устранён: GameInput собирает подписчиков
интерфейса IPrimaryActionHandler и отдаёт нажатие ровно одному из них по приоритету.
Приоритеты: захват цели (30) → сброс бомбы (20) → детонация (10).
- Разведчик без оружия: Пробел ставит/снимает захват.
- Если в прицеле есть захватываемая цель: первый Пробел — захват, следующий — сброс (если есть заряд)
  или детонация; при уже захваченной цели и готовом оружии захват нажатие не забирает.
- Если цель не в прицеле: Пробел сразу сбрасывает бомбу (если есть) или подрывает борт.
RebindService (Assets/Scripts/Input/RebindService.cs) переназначает клавиши и хранит их в
SettingsData.controls.keyBindings: элемент списка KeyBinding.actionId вида «Throttle:2»
(действие + индекс привязки), KeyBinding.bindingJson — путь контрола («<Keyboard>/k»).
ApplySavedBindings() накатывает оверрайды при старте GameInput, TryAssignKey() назначает клавишу
и пишет в настройки, ResetBinding()/ResetAllBindings() возвращают дефолт, FindConflict() предупреждает
о дублях в консоли. В панели настроек на вкладке «Управление» появились строки кликабельных клавиш
(новый тип строки SettingType.Key, компонент Assets/Scripts/UI/KeyBindingRow.cs): клик — «Нажмите
клавишу», Esc — отмена, Backspace/Delete — сброс к дефолту, конфликты пишутся в консоль.
На GameInput переведены: DroneFlightController (газ/стики/зависание/режим углов/запуск моторов),
DroneCameraModule (гимбал и центрирование), FPVZoom (зум), TargetLockManager, DroneCollision,
DroneDropMechanism (трое — через IPrimaryActionHandler), NIghtVisionController, FPVCameraShake (тряска
от газа), TrainingManager (шаги обучения реагируют на действия, а не на клавиши). DroneManager
переключает борта 1–9 через Keyboard.current нового Input System. Легаси Input.* в Assets/Scripts
больше не используется.
Поля DroneConfig.detonateKey / targetLockKey / zoomInKey / zoomOutKey / nvToggleKey и поле
DroneDropMechanism.dropKey больше не читаются (клавиши глобальные, из настроек); данные в ассетах
оставлены как есть, физика дрона не менялась.

См. также: ARCHITECTURE.md, INDEX.md, SETTINGS_DESIGN.md.
