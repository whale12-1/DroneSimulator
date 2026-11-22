# 05 — Текущее состояние проекта

Дата аудита: **21.11.2026**. Обновлено: **22.11.2026** (унификация UI и вёрстка `MainMenu` / `Bunker(main)`). Оценка сделана по коду и ассетам (комментариев-маркеров `TODO/FIXME` в проекте **нет** — их искали по всем `.cs`).

---

## 1. Что реализовано и работает (по сценам)

### `Boot`
* ✅ Подъём `SceneLoader` + `SettingsService`, переход в `MainMenu`.
* ✅ Экран загрузки с `CanvasGroup`.

### `MainMenu`
* ✅ Видео-фон (VideoPlayer → RenderTexture).
* ✅ Кнопки Play / Settings / Exit.
* ✅ Полноценная панель настроек (4 вкладки, 43 параметра: 13/10/8/12), сброс и сохранение. Проверено в Play Mode 22.11.2026 (см. §7): все 19 параметров «Группы A» применяются, сохраняются и сбрасываются.
* ✅ Кнопка «Сброс» в настройках (22.11.2026): возвращает дефолты `SettingsData` и оставляет панель открытой. Раньше на кнопке висел persistent `OnClick → MainMenuController.OnBackFromSettings` (работала как крестик), а поле `resetButton` в префабе отсутствовало — код на кнопку не подписывался. С 22.11.2026 `ResetButton` перенесён в `SettingsPanel.prefab`, сценовые дубликаты удалены.
* ✅ Единый тактический стиль UI (22.11.2026): `SettingsPanel` — модальное окно 1400×920 (затемнение + металлическая плашка + рамка с красными угловыми скобками), TMP `LiberationSans SDF` во всех строках, HUF-слайдеры/тумблеры/дропдауны, красный `#DB143D` для сброса/закрытия, активная вкладка белая с красной полосой. Исправлены наложение списка на вкладки (16 px), невидимая активная вкладка, огромная пустая колонка между подписью и контролом, синие/зелёные контролы, «толстый» текст 30 px на кнопке «Сброс».
* ⚠️ Панель выбора миссий присутствует в сцене, но недостижима (см. §2, раздел «Что недоделано»); оформлена в общем стиле префаба.

### `Bunker(main)`
* ✅ FPS-перемещение и обзор, анимации сидения/печати.
* ✅ Взаимодействие `E` (raycast + подсказки), посадка/вставание (`ChairInteractable`).
* ✅ Радиус подсказки стула ограничен (21.11.2026): `ChairInteractable.maxInteractDistance = 0.8 м` — горизонтальное расстояние от игрока до ближайшей точки `BoxCollider` объекта `Chair_Blocker`. Глобальный рейкаст `PlayerInteractionDetector.interactionRange = 3 м` не менялся, взаимодействие с терминалом не затронуто.
* ✅ Исправлены баги взаимодействия со стулом (21.11.2026):
  * **Переход `SitDown` больше не проигрывается.** `ChairInteractable.SitDown()` телепортирует игрока на `sitPosition` и сразу вызывает `PlayerController.ForceSitIdle()` (`IsSitting = true`, `IsTyping = false`, `animator.Play("SitIdle", 0, 0f)` + `animator.Update(0f)`). Проверено: `GetCurrentAnimatorStateInfo(0)` в том же кадре после `Interact()` уже `SitIdle`, состояние `SitDown` не запускается. Переход `Idle → SitDown` в `PlayerAnimator.controller` остаётся как есть, но кодом не используется.
  * **Высота посадки** — `Chair_Blocker/SitPoint.localPosition` = `(0, **−0.385**, 0)` (было `−0.44`; промежуточный вариант `−0.5` отменён), мир `−1.526`. Замер `SkinnedMeshRenderer.BakeMesh` по всей длине клипа `SitIdle` (7 с, 11 сэмплов): самая низкая точка меша `Soldier_body` держится в диапазоне **+0.002 … +0.010 м** над полом (`−1.573`) — подошвы стоят на полу и не проваливаются ни в одном кадре (вариант `−0.5` давал до `−1.6822`, т.е. 11 см под полом). Таз при этом на ~0.22 м выше плоскости сиденья (`−1.083`): клип mixamo «Sitting» рассчитан на более высокое кресло; приоритет отдан ступням — чтобы опустить таз, нужен foot IK или другой клип.
  * **`Space` (встать)** — `ChairInteractable.StandUp()` телепортирует на `standPosition` и вызывает `PlayerController.ForceIdle()` (`IsSitting`/`IsTyping` = false, `animator.Play("Idle", 0, 0f)` + `Update(0f)`) — мгновенный `Idle`, без остаточной анимации сидения.
  * **Точка вставания** — `Blockers_Furniture/StandPoint` = `(1.425, −1.6, 1.893)` — **справа** от стула (позиция стула + `right · 0.85 м` при повороте 109°, `right = (−0.326, 0, −0.946)`). Слева и спереди вставать нельзя: мешают `Safe1_Blocker` и `ComputerNCamGEo_Blocker`.
  * **Принудительная посадка (`BunkerStateRestorer`)** — проверено: сразу после загрузки `Bunker(main)` с `MissionContext.ReturnedFromMission = true` игрок уже в `SitIdle` на `SitPoint` (подошвы на полу), рабочий стол открывается, флаг сбрасывается. Проверка «не сажать уже вставшего» перенесена из `ForceSitIdle()` в `ComputerUIController.CloseDesktop()` (`if (playerController != null && playerController.isSitting)`), чтобы закрытие рабочего стола не возвращало позу сидения.
  * **Вставание заблокировано, пока открыт рабочий стол (22.11.2026).** Пока `ComputerUIController.IsOpen == true`, `ChairInteractable` игнорирует `Пробел` — ранний выход в `Update()` и защита в самом `StandUp()` (включая путь через `Interact()`), публичный признак — `ChairInteractable.IsStandUpBlocked`. Причина бага: `MonoBehaviour.Update()` не зависит от `Time.timeScale`, поэтому при открытом ОС (`timeScale = 0`) стул поднимал игрока, а окно «рабочего стола» оставалось открытым. Ссылка на `ComputerUIController` резолвится в `Awake()` через `FindAnyObjectByType<ComputerUIController>(FindObjectsInactive.Include)` (объект `ComputerOS` в сцене выключен, `m_IsActive: 0`), правок сцены не потребовалось. `Esc`/`E` закрывают ОС, игрок остаётся сидеть (`CloseDesktop()` → `ForceSitIdle()`), вставание доступно сразу после закрытия. Принудительная посадка `BunkerStateRestorer` не затронута: он вызывает `Interact(go, force: true)` → `SitDown()`, а guard стоит только в `StandUp()`.
* ✅ «Рабочий стол» ОС с анимацией открытия, иконка «Миссии».
* ✅ Меню миссий с карточками, деталями, статусами, наградой, статистикой и блокировкой по пререквизитам.
* ✅ Единый тактический стиль UI бункера (22.11.2026): `MissionSelectPanel` — непрозрачный терминал с рамкой, **вертикальным списком карточек 840×150** (было горизонтальная лента 400×400), детали 860×814, красный `#DB143D` у «Назад»/«Запустить»; `PausePanel` — модалка 560×620 с металлическими кнопками 460×60, красным выходом и футером-подсказкой; подсказки взаимодействия вынесены на металлические плашки с красным акцентом; `ComputerOS` — металлическая иконка «МИССИИ» 160×160 + декоративные шапка/футер терминала (логика ввода и `Time.timeScale` не менялись). Удалены просвечивание рабочего стола сквозь меню миссий, тёмный заголовок на светлом фоне, «вымытая» полупрозрачная плашка списка и внутренние линии спрайтов-плашек поверх текста.
* ✅ Пауза (`Esc`), настройки, выход в главное меню.
* ✅ Кнопка «Сброс» в настройках паузы (22.11.2026): работает и мгновенно обновляет строки. Раньше persistent `OnClick` имел `m_Target: {fileID: 0}` (вызов не выполнялся), а `resetButton` не был назначен — теперь кнопка ищется по имени и подписывается кодом. Сама кнопка живёт в `SettingsPanel.prefab`, сценовый дубликат удалён.
* ✅ Возврат из миссии с восстановлением позы за компьютером (`BunkerStateRestorer`).
* ⚠️ Окна «Магазин» и «Карта миссий» в `InteractionUIController` — пустые контейнеры без контента.
* ⚠️ Нет выбора/покупки дронов в бункере (только миссии).

### `PreparingDemo`
* ✅ 3 главы обучения с текстами инструктора, переключением окружения и респавном дрона.
* ✅ Полоса препятствий из 3 колец (`TrainingGate`).
* ✅ Оптика: зум, гимбал, ПНВ, автозахват цели.
* ✅ Уничтожение наземной цели тараном, «снимок» обломков (пробел).
* ✅ Цель в помещении (залететь в дом, уничтожить склад).
* ✅ Броня/модули: пробитие с учётом угла, детонация БК, потеря хода, отрыв деталей.
* ✅ Бесконечные попытки: после потери ударного дрона — респавн через 1.5 с.
* ✅ Дебриф + возврат в бункер.

### `FirstMission(Forest)`
* ✅ Сценарий с титрами: наблюдение → задача → уничтожение БТР → отступление РСЗО → погоня.
* ✅ БТР-ИИ: приоритизация самой опасной цели, наведение башни с упреждением, стрельба частицами, дымовая завеса.
* ✅ РСЗО: патруль → залп 4 ракет → отступление; условие провала — уход из района.
* ✅ РЭБ: передатчик на технике, помехи по расстоянию, визуальные искажения и звук.
* ✅ Смена дронов `1..9`, recon-дрон после подрыва ударного.
* ✅ Маркер последней позиции цели при переключении на ударный дрон (`TargetPingSystem`).
* ✅ 6 тайлов террейна + `TerrainDistanceBoost`, запечённое освещение, постобработка.
* ⚠️ Сценарий дублируется: победа/поражение считается и в `AmbushMissionDirector`, и (частично) в `MissionController`.

### Сквозные системы
* ✅ FSM + шина `GameEvents` + единый контракт урона `IDamageable`.
* ✅ Прогресс: `progress.json`, очки, лучшие времена, повторные прохождения, разблокировки.
* ✅ Настройки: `settings.json` с версионированием и атомарной записью; применение на каждой сцене. Сброс возвращает дефолты `SettingsData` (включая `fpsMouseSensitivity` = 1.0 — было 1.67) и сразу проставляет `version = SchemaVersion` (**4**).
* ✅ Аудит настроек (22.11.2026): `SchemaVersion 4` + `SettingsData.version = 4` (свежий объект не проходит миграцию); разрешение хранится в пикселях (`resolutionWidth/Height`, легаси `resolutionIndex` только для миграции); `windowMode` — единственный источник истины, `fullscreen` — производное зеркало; лимит FPS принудительно отключает VSync; Motion Blur исправлен (была инверсия + нулевая интенсивность) и ограничен бункером (`!SettingsScope.IsMission`); добавлен `Resources/Audio/MainMixer.mixer` (7 шин + Exposed Parameters), `AudioListener` в `MainMenu`, звук видео-фона выведен в шину `Music`, `duckOnMenus` реализован; `showInteractionHints` скрывает и нижнюю подсказку; счётчик FPS работает в `MainMenu`; кнопки меню/паузы/настроек и посадка на стул озвучены процедурными клипами.
* ✅ Удалены 5 неиспользуемых настроек (22.11.2026): «Туман у дрона», «Дисторсия объектива», «Искажения РЭБ», «Усиление ПНВ» (вкладка «Графика», 17 → 13) и «Уменьшить опасные эффекты» (вкладка «Геймплей», 13 → 12). Из `SettingsData` убраны `graphics.droneFog`, `graphics.droneFogDensity`, `graphics.droneLensDistortion`, `graphics.ewVisualDistortion`, `graphics.nightVisionGain` и `gameplay.reduceDangerousEffects`; `EWReceiver.VisualScale` фиксирован в 1, шум РЭБ рисуется всегда. Старый `settings.json` читается без ошибок (ключи, которых нет в классе, игнорируются `JsonUtility`). Проверено в Play Mode: `MainMenu` и `Bunker(main)` — «Графика» 13 пунктов, «Геймплей» 12, переключение вкладок, изменение настроек и «Сброс» работают без ошибок. (Позже, в аудите настроек 22.11.2026, `SchemaVersion` поднят до 4 — см. §7.)
* ✅ URP-настройки в рантайме (render scale, тени, MSAA), runtime-Volumes для РЭБ/ПНВ/Motion Blur.
* ✅ Процедурные звуки (моторы, РЭБ) — без внешних аудио-ассетов.
* ✅ Счётчик FPS (опционально).

---

## 2. Что находится «в разработке» / недоделано

| Область | Состояние | Признаки в коде/ассетах |
|---|---|---|
| Магазин/ангар в бункере | 🟡 каркас | `InteractionUIController.OpenShop()/OpenMissionBoard()`, `HangarInteractable`, `MissionBoardInteractable`, `CloseMissionBoardButton` — окна открываются, но контента нет |
| Выбор дрона в бункере | 🟡 каркас | `GameSession.SelectedDroneConfig` — поле есть, но сервис **нигде не используется** |
| Третья миссия | 🟡 пустой ассет | `Missions/Mission3.asset`: `sceneName` пуст, `defaultDrone: {fileID: 0}`, не в реестре |
| Тип цели `ReachPoint` | 🟡 код есть, данных нет | `MissionController` умеет, но `reachPointTarget` = `null` во всех сценах |
| Рестарт миссии | 🔴 неверно | `MissionUIManager.RestartMission()` всегда грузит `PreparingDemo` |
| Перехватчик (Interceptor) | ⛔ вне области | `IntersectorHUD`, `IntersectorCollision`, `IntersectorConfig.asset`, сцены `player`/`FunctionalUI-Demo` |
| Бомбы/сброс | ⛔ вне области | `DropDroneMechanism`, `DroppedBomb`, `DropHUD`, поля `bombPrefab/maxAmmo/dropCooldown` в `DroneConfig` не используются в 5 сценах |
| Вражеские «Шахеды» | ⛔ вне области | `EnemyDroneHealth`, `ShahedFlight`, `BulletParticleCollision` |

---

## 3. Технический долг — по приоритетам

### 🔴 P0 — может ломать функциональность

| # | Проблема | Где | Комментарий |
|---|---|---|---|
| 1 | **Рестарт миссии всегда грузит обучение** | `MissionUIManager.cs:87` | `LoadAsync("PreparingDemo")` вместо `MissionContext.Current.sceneName` |
| 2 | **Битые кодировки (невалидный UTF-8)** — 7 файлов | `TrainingGate.cs`, `NIghtVisionController.cs`, `HimarsStrike.cs`, `TargetPingSystem.cs`, `VehicleDust.cs`, `WheelRotator.cs`, `ScreenFader.cs` | Русские комментарии и часть `[Header]`-строк нечитаемы; риск дальнейшей порчи при пересохранении |
| 3 | **Двойное кодирование (mojibake)** | `DamageSystem.cs`, `VehicleSmartAI.cs` | Комментарии вида `РЎРµРЅСЃРѕСЂС‹`; файлы валидны как UTF-8, но текст уже потерян |

> Исправлено 22.11.2026 (настройки, Группа A): инверсия `MotionBlur` + нулевая интенсивность (`PostFxRouter.ApplyMotionBlur`), сброс `windowMode` миграцией (`SettingsData.version` = 3 vs 2), мёртвые категорийные громкости (нет `MainMixer`), неработающий `duckOnMenus`, подсказка «Встать [Пробел]» при выключенных подсказках, счётчик FPS в меню, лимит FPS при включённом VSync, `ApplyHeavy` на каждом тике слайдеров. Подробности — §7.
>
> Нумерация пунктов ниже сохранена исторической (4–5 закрыты), чтобы не ломать ссылки в задачах.

### 🟠 P1 — архитектурные «протечки» и хрупкость

| # | Проблема | Где |
|---|---|---|
| 6 | Ядро (`SceneLoader`) знает про сцену бункера и ждёт `BunkerStateRestorer` | `SceneLoader.cs:117-126` |
| 7 | `MissionController` знает имя сцены обучения | `MissionController.cs:114` (`sceneName.Contains("PreparingDemo")`) |
| 8 | Поиск дрона по подстроке в имени конфига | `DroneManager.cs:175`, `:243`, `:280` |
| 9 | `MissionController` — одновременно «исполнитель» и «таймер», а директора обходят его через `CompleteMission` + флаг `autoComplete` | `MissionController.cs:13`, `:145` |
| 10 | Дублирование сценария победы/поражения между директором и `MissionController.LateUpdate` | `AmbushMissionDirector.cs:93-111`, `MissionController.cs:130-139` |
| 11 | `BunkerPauseController` — god object (10 ссылок), вручную включает/выключает компоненты | `BunkerPauseController.cs:6-15`, `:179-204` |
| 12 | Хрупкая логика `Esc` на таймерах 0.2 / 0.25 с | `BunkerPauseController.cs:68-81`, `ComputerUIController.cs:27-30` |
| 13 | Смешение двух Input API в одном проекте | `DroneFlightController`, `TrainingManager`, `NightVisionController` — старый; UI — новый |
| 14 | ПНВ через рефлексию приватного поля URP + магическое имя фичи | `NIghtVisionController.cs:22-48` |
| 15 | `PostFxRouter.Bind(NightVisionController)` — мёртвая ветка (вызов закомментирован) | `PostFxRouter.cs:39-43` |
| 16 | `SettingsPanel.NormalizeZ()` — костыль под «съехавший» по Z префаб | `SettingsPanel.cs:79-92` |
| 17 | `MissionMenuController.Awake()` правит FSM из `Boot` | `MissionMenuController.cs:30-31` |
| 18 | `MissionMenuController.FindBackButton()` ищет по имени, иначе берёт первый `Button` | `MissionMenuController.cs:53-64` |
| 19 | `BunkerStateRestorer` ждёт анимацию фиксированные 2 с | `BunkerStateRestorer.cs:44` |
| 20 | `SceneLoader` ждёт фиксированные 1.5 с после загрузки | `SceneLoader.cs:10` |

### 🟡 P2 — читаемость, мусор, дубли

| # | Проблема | Где |
|---|---|---|
| 21 | Имя файла ≠ имя класса | `EWEmiter.cs` → `EWEmitter`, `MIssionRegistry.cs` → `MissionRegistry`, `DroneHealth.cs` → `EnemyDroneHealth`, `DroneFlightScript.cs` → `ShahedFlight` |
| 22 | `currentHealth` возвращает **остаток** здоровья (`=> totalHealth`) | `VehicleHealth.cs:19` |
| 23 | `ProgressService.CompleteMission(m, score)` — второй путь записи прогресса (в 5 сценах не используется) | `ProgressService.cs:122-135` |
| 24 | `GameEvents.ClearAll()` не вызывается нигде | `GameEvents.cs:25` |
| 25 | `GameSession` — мёртвый singleton | `MainMenu/GameSession.cs` |
| 26 | `Bootstrapper.missionRegistry` — поле без использования | `Bootstrapper.cs:6` |
| 27 | `MainMenuController.registry` нужен только для логов; `OnBackFromMissions()` недостижим | `MainMenuController.cs:7`, `:91` |
| 28 | `MissionSelectPanel` в `MainMenu` — мёртвый UI | сцена `MainMenu.unity` |
| 29 | Подробные `Debug.Log` в рабочих путях | `ChairInteractable.cs:50,71,78,81,99`, `DroneManager`, `VehicleSmartAI` |
| 30 | `VehicleSmartAI` рисует `Debug.DrawRay` каждый кадр | `VehicleSmartAI.cs:167-168` |
| 31 | `TrainingManager` удаляет все `ParticleSystem` вне Canvas и глушит аудио при смене главы | `TrainingManager.cs:185-195` |
| 32 | `PlayerInteractionDetector` — raycast каждый кадр без троттлинга | `PlayerInteractionDetector.cs:11-35` |
| 33 | Кнопки магазина/карты миссий открывают пустые окна | `InteractionUIController.cs:72-112` |
| 34 | `Mission3.asset` — пустой недостижимый ассет | `Resources/Missions/Mission3.asset` |

---

## 4. Мёртвый / недостижимый код (вне 5 сцен)

Скрипты существуют и компилируются, но ни в одной из 5 сцен (и ни в одном их префабе) не привязаны:

`DropHUD`, `IntersectorHUD`, `DropDroneMechanism`, `DroppedBomb`, `IntersectorCollision`, `BulletParticleCollision`, `EnemyDroneHealth` (`DroneHealth.cs`), `ShahedFlight` (`DroneFlightScript.cs`), `MissionBoardInteractable`, `HangarInteractable`, `CloseMissionBoardButton`, `GameSession`, `ProgressService.CompleteMission`, `MissionObjectiveType.ReachPoint` (нет данных в миссиях).

Плюс **сцены вне области** (подлежат удалению по плану): `Scenes/player.unity`, `Scenes/FunctionalUI-Demo.unity`, `Scenes/FirstMission(SpizjenyiVaarik).unity`, а также `SampleScene` и `TerrainDemoScene`, которые всё ещё включены в Build Settings.

Удаление этих скриптов освободит от зависимостей поля `DroneConfig` (`bombPrefab`, `maxAmmo`, `dropCooldown`) и `SettingsData` (`monoMode`, `muteOnSignalLoss`, `KeyBinding.keyBindings` — не используются в UI и в 5 сценах).

---

## 5. Риски при ближайших изменениях

1. **Domain Reload / Enter Play Mode Options.** `GameStateMachine`, `MissionContext`, `EWService.BoundaryJamIntensity`, `ProgressService.data`, `SettingsService.Instance` — статические. При отключённом Domain Reload состояние протечёт между запусками; обнуления сейчас нет (`GameEvents.ClearAll()` не вызывается).
2. **Удаление «лишних» сцен.** `SceneLoader.IsSceneInBuildSettings` ищет по имени — дыры в индексах безопасны, но `SampleScene` и `TerrainDemoScene` надо убрать из Build Settings, иначе они останутся загружаемыми.
3. **Правка `DroneConfig`.** На него завязаны геймплей, HUD, РЭБ и ПНВ. Удаление «бомбовых» полей сломает `DropHUD`/`DroppedBomb` — удалять нужно вместе.
4. **Правка UI-префабов.** Editor-билдеры (`MissionSelectPanelBuilder`, `SettingsPanelPrefabBuilder`) могут перезаписать ручные изменения: сначала правьте билдер, потом префаб.
5. **Кодировки.** Перед массовым переформатированием `.cs` конвертируйте 7 битых файлов в UTF-8, иначе русские комментарии превратятся в мусор безвозвратно (часть данных уже потеряна).
6. **Новый дрон.** Помимо префаба и `DroneConfig`, его нужно добавить в `MissionDefinition.availableDrones` **и** в `DroneManager.allDronesInScene` конкретной сцены — иначе он не попадёт в `activeSlots`.
7. **Проверка экранных настроек.** `windowMode`, разрешение и лимит FPS в Editor Play Mode не отражаются в Game view — это ограничение редактора, а не регрессия; проверять их нужно в билде (данные и вызовы проверены в Play Mode, см. §7).

---

## 6. Рекомендованный порядок работ

1. **Быстрые победы (P0):** `RestartMission` (настройки Группы A закрыты — см. §7).
2. **Гигиена кодировок:** конвертация 7 файлов в UTF-8 (без изобретения нового текста — просто сохранить как UTF-8 с текущими символами, где возможно; где текст потерян — переписать осмысленно).
3. **Развязка слоёв:** убрать `BunkerStateRestorer` из `SceneLoader` (сделать событие `SceneLoader.SceneReady`), убрать `"PreparingDemo"` из `MissionController` (перенести логику crash-панели в `TrainingManager`), заменить поиск по `"FPV"` на явный `DroneRole`/флаг в `DroneConfig`.
4. **Единый реестр модальных окон** бункера вместо ручных таймеров (стек окон с приоритетом).
5. **Удаление мёртвого кода и сцен** (после подтверждения, что соответствующие фичи не вернутся).
6. **Тесты/валидация:** сейчас нет ни одного unit-теста. Минимально полезные — `ProgressService.ComputeScore`, `ProgressService.IsUnlocked`, `GameStateMachine.CanTransitionTo`, `MissionText.*`.

---

## 7. Настройки: проверенный статус (22.11.2026)

Аудит `SettingsPanel` (43 параметра): **19 — Группа A** (глобальные + `MainMenu`/`Bunker(main)`), **24 — Группа B** (дроны/миссии, в этой задаче не трогались). Проверка выполнена в Play Mode по цепочке `Boot → MainMenu → Bunker(main)`.

Группа A (проверено): режим окна, разрешение, VSync, render scale, лимит FPS, уровень качества, дистанция теней бункера, мягкие тени бункера, постобработка бункера, Motion Blur бункера, чувствительность/инверсия мыши FPS, мастер-громкость, музыка, SFX, UI, duck, подсказки взаимодействия, счётчик FPS.

Что подтверждено фактическим состоянием Unity:

* `Screen` / `SettingsApplier`: выбор режима пишет `windowMode` + зеркало `fullscreen` и обновляет кэш (`lastWindowMode`); разрешение — `resolutionWidth/Height` (`0×0` = «Текущее»); для 640×480 вызывается `Screen.SetResolution(640, 480, mode)` (кэш `lastResolutionWidth/Height` = 640/480).
* `QualitySettings`: VSync (`vSyncCount` 1↔0), лимит FPS (`targetFrameRate` = 144 при `fpsLimit = 144`, VSync принудительно 0; при 0 — `targetFrameRate = -1`), уровень качества (`GetQualityLevel` 3↔5), lod bias.
* runtime URP (клон `PC_RPAsset (Runtime)`): `renderScale` (0.6 / 0.85), `shadowDistance` (10/20/25), `m_SoftShadowsSupported` (рефлексия, false/true).
* `Camera`: `farClipPlane = bunkerFarClip` (80), `renderPostProcessing` ← «Постобработка бункера» (false/true).
* `PostFxRouter`: `MotionBlur.active/intensity` (включение → `active = true`, `intensity = 0.6`; выключение → `0`); вне бункера (`SettingsScope.IsMission`) эффект не применяется.
* `AudioMixer` (`MainMixer`): `SetFloat` работает для всех 7 Exposed Parameters; duck-музыка в меню = −9.1 дБ (×0.35), в геймплее = 0 дБ; видео-фон меню играет в шине `Music`; клики UI и «стул» слышны в шинах `UI` / `SFX`.
* `PlayerController`: `fpsMouseSensitivity` (2.0) и `fpsInvertY` читаются в реальном времени.
* `InteractionUIController`: выключение подсказок скрывает и центральную, и нижнюю («Встать [Пробел]»).
* `FpsCounterHud.ShouldDraw()` = true в `MainMenu` и `Gameplay`, false при выключенной настройке.
* Сохранение/загрузка: `settings.json` (`version: 4`) пишется с новыми полями, после перезапуска Play значения применяются; «Сброс» в `MainMenu` и `Bunker(main)` возвращает дефолты, панель остаётся открытой, строки пересобираются (13 на вкладке «Графика»).

**Ограничения проверки.** В Editor Play Mode `Screen.SetResolution`/`Screen.fullScreenMode` и `Application.targetFrameRate` не отражаются в Game view (проверять нужно в билде; путь данных и вызовы проверены). MCP-скриншоты в Play Mode при нефокусированном редакторе не содержат `OnGUI`/Overlay-UI, поэтому счётчик FPS проверен по `ShouldDraw()` и коду `OnGUI`.

**Отложено (Группа B, не трогали):** `droneFov`, `droneFarClip`, `droneShadowDistance`, `droneInvertY`, `gimbalInvertY`, `stickSpeed`, `centeringSpeed`, `gimbalSpeed`, `throttleSpeed`, `deadZone`, `throttleHold`, `motors`, `ew`, `voice`, `hudTransparency`, `hudScale`, `crosshairSize`, `hudLine`, `targetFrame`, `subtitles`, `subtitleFont`, `shake`, `throttleShake`, `trainingHints`. У `throttleShake` и `trainingHints` **нет потребителей** — чинить вместе с миссиями.

---

## 8. Итоговая оценка

Проект находится в состоянии **играбельного вертикального среза**: полный цикл «меню → бункер → обучение → боевая миссия → дебриф → бункер» работает, прогресс и настройки сохраняются, есть два разных по механике уровня и общая боевая система (физика дрона, урон по модулям, РЭБ, HUD, ПНВ).

Основные слабые места — не геймплей, а **связность**: ядро знает про конкретные сцены, директора миссий дублируют логику `MissionController`, UI-модальность держится на таймерах, а часть файлов имеет повреждённые кодировки. Всё это локально и чинится без переписывания проекта.

---

## 9. Унификация UI и вёрстка (22.11.2026)

Задача: привести `MainMenu` и `Bunker(main)` к стилю главного меню (`HUF Pack.png`, `LiberationSans SDF`, красный `#DB143D`) и убрать наложения. Боевые сцены (`PreparingDemo`, `FirstMission(Forest)`), их HUD и панели не затрагивались.

### 9.1 Что изменено

| Область | Было | Стало |
|---|---|---|
| `SettingsPanel` | плоская панель на весь экран, Arial, синяя активная вкладка, строки 56 px, контрол 260 px у правого края | модалка 1400×920, TMP, белая активная вкладка с красной полосой, строки 50 px, подпись + значение + контрол 320 px |
| Вкладки/список | список начинался на 16 px выше низа вкладок и перекрывал их | список начинается через 12 px после вкладок |
| Дропдаун | опции на всю ширину 1852 px, текст с 3 пробелами | опции 320×40 справа, красная полоса выбранной, отступ 16 px |
| Слайдеры/тумблеры | синяя заливка, зелёная галочка, «✓» | стальная заливка HUF #49/#22, чекбоксы HUF #20/#21 |
| «Сброс»/«X» | белые `UISprite`, текст 30 px, кнопки в разных инстансах | в префабе, HUF #1 + `#DB143D`, 168×44 и 48×48, ровно одна в сцене |
| `PausePanel` | 3 серые кнопки 320/320/400×70 внизу, без плашки | модалка 560×620, кнопки 460×60, красный выход, футер |
| `MissionSelectPanel` | полупрозрачная плашка, горизонтальная лента карточек 400×400, тёмный заголовок, смешанные поколения вёрстки | непрозрачный терминал с рамкой, вертикальный список 840×150, светлый заголовок, детали 860×814 |
| `MissionCard` | серая плитка #12, чужой замок, LE 400×400 | плоская плитка 840×150, превью 200×126, HUF #20/#50, рамка выбора `#DB143D` |
| `ComputerOS` | белый квадрат «миссии» | металлическая плитка 160×160 + иконка #15 + шапка/футер терминала |
| Подсказки | белый TMP 28 без фона | плашки 560×56 / 460×52, красный акцент |
| `ShopWindow` | заголовок 60, кнопка 300×80 `UISprite` | плашка 720×420, заголовок 32, кнопка 220×56 красная |

### 9.2 Технические решения

* `Assets/Scripts/UI/TacticalUITheme.cs` — единые цвета/размеры и `ButtonColorBlock()`; `Assets/Scripts/Editor/TacticalUIBuilder.cs` — загрузка HUF-спрайтов/шрифта, стилизация, `BuildTacticalFrame`.
* `SettingsPanelPrefabBuilder` и `MissionSelectPanelBuilder` переписаны на **in-place** правку (`LoadPrefabContents` → `SaveAsPrefabAsset`) и идемпотентны: `Header`/`MissionDetailsPanel` не пересоздаются, ссылки сцен сохраняются.
* Крупные панели используют плоскую заливку + собственную рамку (2 px контур + красные уголки), потому что `#12`/`#7` при растяжении дают внутренние линии поверх контента.
* Фикс активной вкладки: `SettingsPanel.SelectTab()` меняет `Image.color`, `fontStyle` и красную полосу напрямую (ColorTint не применялся без смены состояния `Selectable`). При смене вкладки скролл сбрасывается вверх.
* `SettingsListBuilder` строит TMP-строки; для описаний миссий включён перенос строк (`Normal`), для однострочных подписей — `NoWrap`.
* `CanvasScaler` остался `ConstantPixelSize` (как в эталоне); макет рассчитан на 1920×1080 и использует anchor-based привязки.

### 9.3 Проверено в Play Mode

* `MainMenu`: главное меню без изменений; настройки — 4 вкладки (13/10/8/12 строк), дропдаун, слайдер, тумблер, «Сброс» (панель остаётся открытой), «X»/`Esc`; клики и hover работают.
* `Bunker(main)`: пауза → настройки → `Esc` (закрывает только настройки), «Продолжить», «Выйти в главное меню» (persistent-вызовы на месте); подсказки у стула/терминала; рабочий стол; иконка «Миссии»; список миссий, детали, блокировка, `LaunchButton`, «Назад».
* Геометрические проверки: карточки 816×150 внутри 860-й колонки, детали 860×814, `LaunchButton` 812×64 (14 px до низа), кнопки паузы внутри плашки, иконка рабочего стола не пересекается с шапкой/футером, текст не выходит за свои rect.
* Консоль Unity — **0 ошибок**.

### 9.4 Остаётся (не входило в задачу)

* У миссий `Mission1/Mission2` не задан `preview`, поэтому обе карточки показывают один и тот же дефолтный скриншот из префаба. Нужны превью-ассеты (или фолбэк по роли дрона) — это контент, а не вёрстка.
* `InteractionUIController` по-прежнему не скрывает подсказки при открытом `ComputerOS` (они скрываются паузой и самим ОС только логикой стека); при желании стоит вынести в общий менеджер модальности.
* `CanvasScaler` фиксирован в пикселях: на не-16:9 разрешениях модалки сохраняют абсолютный размер. При необходимости перейти на `ScaleWithScreenSize` (1920×1080, match 0.5).


