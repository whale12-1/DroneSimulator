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
* ✅ (22.11.2026) Панель выбора миссий **удалена из сцены** как мёртвый UI: инстанс `MissionSelectPanel` не открывался ниоткуда, поле `missionSelect` убрано из `MainMenuController`. Панель миссий осталась только в `Bunker(main)`.

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
* ✅ «Рабочий стол» ОС с анимацией открытия, иконка «Миссии». С 22.11.2026 фон рабочего стола анимированный — статичная пластина + вращаемый лепесток радара, вращение идёт и при `Time.timeScale = 0` (см. §9.7).
* ✅ Меню миссий с карточками, деталями, статусами, наградой, статистикой и блокировкой по пререквизитам.
* ✅ Единый тактический стиль UI бункера (22.11.2026): `MissionSelectPanel` — непрозрачный терминал с рамкой, **вертикальным списком карточек 840×150** (было горизонтальная лента 400×400), детали 860×814, красный `#DB143D` у «Назад»/«Запустить»; `PausePanel` — модалка 560×620 с металлическими кнопками 460×60, красным выходом и футером-подсказкой; подсказки взаимодействия — компактные белые надписи 20/18 px с обводкой `Outline`, без плашек (`InteractionHint` на 75 % высоты, `SittingHint` у нижнего края — см. §9.5); `ComputerOS` — металлическая иконка «МИССИИ» 160×160 + декоративные шапка/футер терминала (логика ввода и `Time.timeScale` не менялись). Удалены просвечивание рабочего стола сквозь меню миссий, тёмный заголовок на светлом фоне, «вымытая» полупрозрачная плашка списка и внутренние линии спрайтов-плашек поверх текста. Невидимость карточек списка исправлена отдельно — см. §9.6.
* ✅ Пауза (`Esc`), настройки, выход в главное меню.
* ✅ (22.11.2026) Единый координатор модальных окон `BunkerModalStack`: `Time.timeScale` и курсор больше не правятся каждым окном по отдельности; закрытие панели миссий без открытого ОС возвращает управление, прямой старт `Bunker(main)` доводит FSM до `Gameplay` (пауза работает).
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
| 12 | Хрупкая логика `Esc` на таймерах 0.2 / 0.25 с — **частично закрыто 22.11.2026**: `timeScale`/курсор централизованы в `BunkerModalStack`, таймеры оставлены как защита от двойного `Esc` | `BunkerPauseController.cs`, `ComputerUIController.cs`, `MissionMenuController.cs` |
| 13 | Смешение двух Input API в одном проекте | `DroneFlightController`, `TrainingManager`, `NightVisionController` — старый; UI — новый |
| 14 | ПНВ через рефлексию приватного поля URP + магическое имя фичи | `NIghtVisionController.cs:22-48` |
| 15 | `PostFxRouter.Bind(NightVisionController)` — мёртвая ветка (вызов закомментирован) | `PostFxRouter.cs:39-43` |
| 16 | `SettingsPanel.NormalizeZ()` — костыль под «съехавший» по Z префаб | `SettingsPanel.cs:79-92` |
| 17 | ~~`MissionMenuController.Awake()` правит FSM из `Boot`~~ — ✅ **закрыто 22.11.2026**: переход удалён, состояние доводит `BunkerStateRestorer.EnsureGameplayState()` | `BunkerStateRestorer.cs` |
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
| 27 | `MainMenuController.registry` нужен только для логов — **частично закрыто 22.11.2026**: недостижимый `OnBackFromMissions()` удалён | `MainMenuController.cs` |
| 28 | ~~`MissionSelectPanel` в `MainMenu` — мёртвый UI~~ — ✅ **закрыто 22.11.2026**: инстанс удалён из сцены | сцена `MainMenu.unity` |
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
| Подсказки | белый TMP 28 без фона | плашки 560×56 / 460×52, красный акцент (**22.11.2026** заменены на компактные надписи 20/18 px с `Outline` — см. §9.5) |
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

### 9.5 Подсказки взаимодействия — компактные надписи (22.11.2026)

Задача: убрать металлические плашки у подсказок в `Bunker(main)`, сделать их компактными читаемыми надписями и переставить по экрану. Затронуты только сцена `Bunker(main).unity` и документация — C#-скрипты, префабы и общий материал `LiberationSans SDF` не менялись.

| Что | Было | Стало |
|---|---|---|
| `InteractionHint` | `Plate` (HUF, 560×56) + `Accent` (красная полоса 3×32) + TMP 22 | только `CanvasGroup` + TMP-`Label` **20 px** + `Outline` |
| `SittingHint` | `Plate` (460×52) + `Accent` (3×28) + TMP 20 | только `CanvasGroup` + TMP-`Label` **18 px** + `Outline` |
| `InteractionHint` позиция | `(0, +150)` — выше центра | **`(0, −270)`** = 810 px от верха, 75 % высоты (граница 3-й/4-й четверти), `sizeDelta 560×40` |
| `SittingHint` позиция | `(0, −440)` — 100 px от низа | **`(0, −470)`** — центр 70 px от низа, нижняя кромка 52 px, `sizeDelta 460×36` |
| Шрифт / цвет / отступы | LiberationSans SDF, белый, `margin 26/4` | LiberationSans SDF, белый, `margin 0`, Center + NoWrap |

Технические решения:

* Дочерние `Plate` и `Accent` удалены у обеих подсказок; остались только `CanvasGroup` на корне и TMP-`Label`. Ссылки `hintGroup` / `hintText` / `sittingHintGroup` / `sittingHintText` указывают на корни (`CanvasGroup`) и `Label` (`TMP_Text`) — не менялись, `Missing` нет.
* Читаемость на любом фоне бункера — компонент `UnityEngine.UI.Outline` на `Label` (`(0,0,0,0.85)`, смещение `1.5,−1.5`, `useGraphicAlpha`). Это дешёвая замена TMP-underlay: TMP `outline`/`underlay` живут **в материале**, и их включение потребовало бы правки общего `LiberationSans SDF` (обводка у всех надписей проекта) либо нового material preset — отклонено, чтобы не трогать общие ассеты и код.
* Editor-билдеры (`SettingsPanelPrefabBuilder`, `MissionSelectPanelBuilder`, `MissionContentBuilder`) — `[MenuItem]`-only, плашки подсказок они не создают; повторный запуск удаление не откатит.
* `CanvasScaler` у `InteractionCanvas` — `ScaleWithScreenSize`, 1920×1080, match 0.5, поэтому `anchoredPosition` задаётся в референсных пикселях (`y = −270` = ровно 810 px от верха при 1080p).
* Место подсказок в иерархии не менялось: `InteractionHint` и `SittingHint` — первые сиблинги `InteractionCanvas`, все модалки (`ComputerOS`, `MissionSelectPanel`, `PausePanel`, `SettingsPanel`) рисуются поверх них.

Проверка (Play Mode, `Bunker(main)`):

1. Подход к стулу — «Сесть [E]» на 75 % высоты, без плашки, белый текст с обводкой (проверено и прямым вызовом, и реальным нажатием `E` через Input System).
2. Игрок сидит — «Встать [Пробел]» внизу (52 px от низа), без плашки; вставание по `Пробел` возвращает подсказку «Сесть [E]» через обычный путь.
3. Сидя + взгляд на терминал — одновременно «Войти в систему [E]» (75 %) и «Встать [Пробел]» (низ): зазор между rect 162 px, тексты/мониторы не перекрываются.
4. Настройка «Подсказки взаимодействия»: при `showInteractionHints = false` `ShowHint`/`ShowSittingHint` оставляют `CanvasGroup.alpha = 0` у обеих подсказок, при включении — снова 1.
5. Консоль Unity — 0 ошибок; `git status` — только `Bunker(main).unity` и файлы `Docs/`; временные скриншоты в `Temp/UIAudit/` удалены.

### 9.6 Список миссий: карточки больше не «чёрный прямоугольник» (22.11.2026)

Задача: в `MissionSelectPanel` левая колонка (`Scroll View`) выглядела сплошным чёрным прямоугольником — карточки «Тренировка» и «БТР и РСЗО», их плашки, текст, иконки и красная рамка выбора не отображались, хотя клики по этим зонам выбирали миссию и правая панель деталей обновлялась корректно.

| Что | Было | Стало |
|---|---|---|
| `Scroll View/Viewport` | `Mask` (Image-based stencil, `showMaskGraphic = false`) **и** `RectMask2D`; `Image` полностью прозрачный | только `RectMask2D`; `Image` остаётся прозрачным (`alpha = 0`), legacy `Mask` удалён |
| Плашка карточки | `(0.10, 0.11, 0.12)` — почти сливалась с подложкой списка | `TacticalUITheme.MissionCardFill` `(0.13, 0.14, 0.155)` ≈ `#212428` |
| Подложка списка | `(0.02, 0.022, 0.025, α 0.92)` | `TacticalUITheme.MissionListFill` `(0.015, 0.016, 0.018, α 0.96)` |
| Порядок детей карточки | `MetaText` рисовался после `LockedOverlay` (метаданные оставались яркими под затемнением) | `Preview → NameText → MetaText → isCompleted → LockedOverlay → SelectedFrame` |

Причина: материал `Mask` включает `UNITY_UI_ALPHACLIP`; при `alpha = 0` у `Image` маски фрагменты вырезаются, stencil не записывается, и все графики карточек с `Stencil Comp: Equal` не растеризуются. Raycast-фильтры `Mask`/`RectMask2D` проверяют только попадание в прямоугольник, поэтому клики продолжали работать — отсюда симптом «невидимые, но нажимаемые» карточки. В `SettingsPanel` был только `RectMask2D`, поэтому список настроек не пострадал.

Технические решения:

* `MissionSelectPanelBuilder.BuildScrollView()` удаляет legacy `Mask` через `DestroyImmediate` перед настройкой `RectMask2D`; фон списка и плашка карточки берутся из `TacticalUITheme` (`MissionListFill` / `MissionCardFill`).
* `MissionSelectPanelBuilder.NormalizeCardOrder()` явно задаёт `SetSiblingIndex` для шести детей карточки — повторный прогон билдера баг не возвращает (проверено: `Tools ▸ DroneSimulator ▸ Mission UI ▸ Rebuild Prefabs`).
* Правки только в `MissionSelectPanel.prefab`, `MissionCard.prefab`, `MissionSelectPanelBuilder.cs`, `TacticalUITheme.cs`. Сцены (`Bunker(main)`, `MainMenu`), правая панель деталей, шапка и «НАЗАД» не менялись; сценовых оверрайдов на удалённый компонент не было.
* Прогон билдера может обновить общий атлас `LiberationSans SDF - Fallback.asset` (TMP добавляет глифы). Этот файл в задачу не входит и был откачен через `git checkout`.
* `Mask` остался только один такой в UI-префабах — теперь их нет вообще (в `SettingsPanel` и `MissionSelectPanel` используется `RectMask2D`).

Проверка (Play Mode, `Bunker(main)`, 22.11.2026):

1. Обе карточки видны: превью 200×126, заголовок 22 Bold, метаданные (`1 цель · без лимита` / `2 цели · без лимита`), на заблокированной — замок и «ЗАБЛОКИРОВАНО» (затемнение накрывает всю карточку, включая метаданные).
2. Клик по «Тренировке» — рамка `#DB143D` на ней, детали «Тренировка», статус «НЕ ВЫПОЛНЕНО», `LaunchButton` активна («ЗАПУСТИТЬ МИССИЮ»).
3. Клик по «БТР и РСЗО» — рамка переезжает на вторую карточку, детали «БТР и РСЗО», статус «ЗАБЛОКИРОВАНО», `LaunchButton` = «НЕДОСТУПНО» (`interactable = false`).
4. «НАЗАД» закрывает меню (`MissionMenuController.IsOpen = false`, `CanvasGroup.alpha = 0`); пауза/настройки/подсказки не затронуты.
5. Консоль Unity — **0 ошибок**; `git status` — только два префаба, два скрипта и `Docs/`; временные снимки в `Temp/UIAudit/` удалены.

### 9.7 Анимированный фон рабочего стола `ComputerOS` (22.11.2026)

Задача: заменить статичный фон `Textures/comp.png` фоном оператора БПЛА из `Assets/Textures/Gemini_Generated_Gif_ymmp07ymmp07ymmp.gif` (вращающийся радар, пустая левая зона под ярлыки). Требования: радар крутится при открытом рабочем столе (`ComputerUIController` выставляет `Time.timeScale = 0`), цикл бесшовный, иерархия и логика ОС (`E` / `Esc`, блокировка вставания) не меняются.

| Что проверили | Результат |
|---|---|
| Импорт GIF в Unity | `textureType: Default`, `spriteMode: 0`, flipbook 1×1 — берётся только первый кадр, анимации нет |
| Параметры GIF | 640×360 (ровно 16:9), 30 кадров по 10 cs → 10 fps, 3.0 с, loop = 1 |
| Разница кадров (яркость, зона радара) | шаг 1.1–1.4, стык `f29 → f0` = **3.66**; «кадр i vs f0» растёт до 9.7 к f26 |
| Угол лепестка по кадрам | голова проходит полный оборот, но ширина следа растёт 15° → 80°, `f0` — пустой радар |
| Вывод | GIF — «разгон радара», а не цикл; покадровая анимация (`Sprite[]`) дала бы видимый сброс каждые 3 с |
| `ffmpeg` / `MediaEncoder.EncodeVideo` | ffmpeg в системе нет; статического `EncodeVideo` в Unity 6000.6 нет → вариант «mp4 + VideoPlayer» требовал своего энкодера |

Решение — **пластина + вращаемый лепесток** (полный разбор — `04` §9):

* `Assets/Textures/DesktopBackground/DesktopBackground_plate.png` — кадр 0 (пустой радар, карта, рамка), назначается на `Image` самого `ComputerOS` (растянут на весь канвас, `Simple`, без `preserveAspect`). С 22.11.2026 **очищена**: мусорный текст справа сверху заклеен клоном фона, круг радара — чистый графит, кольца 26/44.5/63 и обод `r = 79.5` (центр 524;179) перерисованы ровными окружностями, буквы N/E/S/W и красные дуги сохранены.
* `Assets/Textures/DesktopBackground/DesktopBackground_sweep.png` — **процедурный луч 1024×1024 RGBA32** (генерируется в `DesktopBackgroundSetup.GenerateSweepTexture`): сектор 65°, передняя кромка `#FF2A55`, шлейф `#DB143D` (alpha 0.42 → 0), `alpha = 0` вне сектора. Старый вариант (пиксельная разница кадра 14 и пластины, R ≈ 105.5) тянул за лучом серый «призрак» радара.
* `ComputerOS/DesktopSweep` — дочерний объект с индексом 0 (под `Btn_Missions`, `TerminalHeader`, `TerminalFooter`), `Image` = лепесток, `raycastTarget = false`, квадрат `473.55×473.55`, `pivot (0.5, 0.5)`, `anchoredPosition (612, 3)` = центр радара (524;179).
* `Assets/Scripts/UI/DesktopRadarSweep.cs` — вращение `120°/с` по часовой на `Time.unscaledDeltaTime`; ранний выход при `ComputerUIController.IsOpen == false`, поэтому закрытый рабочий стол кадры не тратит (объект ОС после `CloseDesktop()` остаётся активным с `alpha = 0`).
* `Assets/Scripts/Editor/DesktopBackgroundSetup.cs` — пункты меню `Tools ▸ DroneSimulator ▸ UI`: настройка импорта кадров и привязка фона к сцене (идемпотентно).
* Сжатие кадров — `Uncompressed`: при `Normal` (DXT) на контрольном скриншоте была блочность контуров карты и колец радара. Кадры 640×360, поэтому ~0.9 МБ на текстуру.
* Исходный `.gif` после извлечения удалён; `comp.png` оставлен на диске, но **ссылок на него в сцене больше нет** (проверено поиском GUID по `Assets/`).

Проверка (Play Mode, `Bunker(main)`, 22.11.2026):

1. Рабочий стол открыт → `IsOpen = true`, `Time.timeScale = 0`, `DesktopSweep.localEulerAngles.z` растёт (0 → 179° → 350°), т.е. радар вращается на «замороженном» времени.
2. Фон заполняет 1920×1080 без обрезки, иконка «МИССИИ» 160×160 читается в свободной левой зоне, шапка (56 px) и футер (48 px) — поверх фона.
3. Клик по иконке → `MissionMenuController.IsOpen = true`, `CanvasGroup.alpha = 1` (панель миссий открывается).
4. `CloseDesktop()` → `IsOpen = false`, `timeScale = 1`, угол замирает (138.87 → 138.87 спустя несколько секунд); повторное открытие возобновляет вращение с того же угла.
5. Консоль Unity — **0 ошибок и варнингов**; `git status` — `Bunker(main).unity`, два новых скрипта и папка `Textures/DesktopBackground/`; побочная правка TMP-атласа `LiberationSans SDF - Fallback.asset` откачена через `git checkout`.
6. Временные скриншоты в `Temp/UIAudit/` удалены.

Ограничения (приняты осознанно):

* Источник 640×360 → на 1920×1080 это 3× апскейл, картинка мягкая; другого разрешения в исходнике нет.
* Плавность ограничена дискретностью исходных 30 кадров только при покадровом воспроизведении; в принятом решении лепесток вращается непрерывно (60 fps и выше), шва цикла нет.
* Фон по-прежнему растягивается на весь канвас, поэтому на не-16:9 радар будет слегка эллиптическим (как и прежний `comp.png`); `preserveAspect` не включался, чтобы не получить чёрные поля.

### 9.8 Чистый луч радара и очистка пластины (22.11.2026)

Задача: убрать артефакты вычитания GIF (серые пятна, которые «тянулись» за вращающимся лучом), поставить луч ровно в центр радара и почистить подложку.

| Что | Было | Стало |
|---|---|---|
| Луч | вырез из GIF с «призраком» всего радара, обрезка по R ≈ 105.5 | процедурный 1024×1024 RGBA32: сектор 65°, `#FF2A55` → `#DB143D`, `alpha = 0` вне сектора |
| Пивот `DesktopSweep` | (526;184) при реальном центре (524;179) | `(0.5, 0.5)`, `anchoredPosition (612, 3)`, квадрат `473.55×473.55` — центр совпадает с радаром |
| Обод на пластине | эллипс 81 × 84.7 (GIF растянут по высоте) | ровная окружность `r = 79.5`; кольца 26 / 44.5 / 63 перерисованы |
| Подложка | мусорный текст справа сверху, зелёный шум в круге | текст заклеен клоном фона, круг — чистый графит (`maxChroma = 4`), буквы N/E/S/W и красные дуги сохранены |

Технические решения:

* `Assets/Scripts/Editor/DesktopBackgroundSetup.cs` — `GenerateSweepTexture()` (суперсэмплинг 3×3, жёсткая отсечка по центру пикселя: за пределами 65° альфа ровно 0) и `CleanPlateTexture()` (клон-заплатка вместо текста, графитовая заливка эллипса старого обода `a = 83`, `b = 86.5`, маска букв `lum ≥ 130` + дилатация, перерисовка колец/перекрестия/обода). Обе операции идемпотентны.
* `ApplySweepRect()` в `BindBunker()` — размер и позиция считаются от эталонного канваса 1920×1080 (`size = 2 · 78 · 3 / (506/512) = 473.55`); если `ComputerOS` в сцене другого размера, билдер пишет предупреждение в консоль.
* Изменения только в `Bunker(main)` (RectTransform `DesktopSweep`), текстурах и editor-скрипте; `DesktopRadarSweep`, префабы и боевые сцены не менялись.

Проверка (Play Mode, `Bunker(main)`, 22.11.2026):

1. Два кадра с разными углами луча: вращение ровно вокруг центра, кромка доходит до обода и не выходит за него, шлейф плавный, пятен и «призраков» нет.
2. `DesktopSweep`: `size 473.55`, `pivot (0.5, 0.5)`, мировой центр `(1572, 543)`; радиус луча 234 px против 238.5 px обода.
3. Пластина: внутри круга `maxChroma = 4`; в зоне бывшего текста нет пикселей > 60 (mean 9.9 против 9.8 у чистого фона); буквы и красные дуги на месте.
4. Консоль Unity — 0 ошибок; `git status` — только `Bunker(main).unity`, папка текстур, editor-скрипт и `Docs/`; `Temp/UIAudit/` удалён.

---

## 10. Fix-QA: исправления по результатам сквозного аудита (22.11.2026)

Аудит цикла `Boot → MainMenu → Settings → Bunker(main) → Pause → ComputerOS → MissionSelectPanel → MainMenu → Bunker(main)` выявил 2 критичных, 13 средних и 14 мелких дефектов. Исправления выполнены четырьмя этапами, каждый — с проверкой в Play Mode.

### 10.1 Что исправлено

| ID | Проблема | Исправление | Файлы |
|---|---|---|---|
| **K1** | Прямой старт `Bunker(main)` оставлял FSM в `MainMenu`: `Esc` не открывал паузу, `BunkerStateRestorer` не мог довести `Loading → Gameplay` | `EnsureGameplayState()`: `Boot → MainMenu → Loading → Gameplay` по матрице FSM; самовольный переход из `MissionMenuController.Awake()` удалён | `BunkerStateRestorer.cs`, `MissionMenuController.cs` |
| **K2** | `CloseMissionMenu()` не восстанавливал `timeScale`/курсор → «замороженная» игра без модалки | Координатор `BunkerModalStack` (`AnyOpen` + `Apply()`) стал единственным владельцем `Time.timeScale` и `Cursor` | **новый** `BunkerModalStack.cs`, `MissionMenuController.cs`, `ComputerUIController.cs`, `BunkerPauseController.cs`, `SettingsPanel.cs` |
| **M1** | Подсказки взаимодействия висели поверх терминала, рейкаст крутился при `timeScale = 0` | `PlayerInteractionDetector`: при `BunkerModalStack.AnyOpen` → `HideHint()` и выход из `Update()` | `PlayerInteractionDetector.cs` |
| **M2** | `FindAnyObjectByType<MissionMenuController>` каждый кадр при открытом ОС | Ссылка кэшируется в `Awake`, в `Update` — только чтение; `ToggleDesktop` использует кэш | `ComputerUIController.cs` |
| **M3** | «ЗАПУСТИТЬ МИССИЮ» активна до выбора миссии | `ShowEmpty()`: `launchButton.interactable = false`, подпись «ВЫБЕРИТЕ ОПЕРАЦИЮ» | `MissionDetailsPanel.cs` |
| **M4** | При открытии панели миссий ничего не выбрано | `SelectDefaultMission()` — первая доступная миссия выделяется сразу, правая панель показывает брифинг | `MissionSelectScreen.cs` |
| **M5** | Первый `Esc` закрывал только детали (выглядело как «Esc не работает») | Один `Esc` закрывает всю панель, как «НАЗАД» | `MissionMenuController.cs` |
| **M6** | Битые сериализованные ссылки в `Bunker(main)` (`chairCollider`, `registry`, `playerController`) | Ссылки назначены в сцене (фолбэки в коде остались как страховка) | `Bunker(main).unity` |
| **M9** | `OpenMissionMenu()` без guard'а + отложенный `Destroy` → 4 карточки на кадр | `if (IsOpen) return;` и отвязка карточек от `Content` до `Destroy` | `MissionMenuController.cs`, `MissionSelectScreen.cs` |
| **M10** | Раскрытый дропдаун «запоминался» между открытиями панели | `SettingsListBuilder.CollapseDropdown()` вызывается из `SettingsPanel.Hide()` | `SettingsListBuilder.cs`, `SettingsPanel.cs` |
| **M11** | «Сброс» срабатывал с одного клика и без подтверждения | Двухшаговый сброс: «Сброс» → «Подтвердить?» (3 с), размеры кнопки не менялись | `SettingsPanel.cs` |
| **M13** | Возврат `MainPanel` зависел от persistent-вызова в сцене | Событие `SettingsPanel.Closed` + подписка `MainMenuController` (работает и по `X`, и по `Esc`) | `SettingsPanel.cs`, `MainMenuController.cs` |
| **m4** | Панель миссий и иконка «МИССИИ» без звуков клика | `UISoundService.Attach` для панели (в `Awake` и после `Populate`) и `DesktopIcon` | `MissionMenuController.cs`, `DesktopIcon.cs` |
| **m7** | Мёртвый `MissionSelectPanel` в `MainMenu` | Инстанс удалён из сцены, поле `missionSelect` и недостижимый `OnBackFromMissions()` убраны из кода | `MainMenu.unity`, `MainMenuController.cs` |
| **m9** | Три объекта с именем `Content` | Переименованы в `SettingsContent`, `MissionListContent`, `DetailsContent` (ссылки — через `[SerializeField]`, поиск по имени не используется) | `Bunker(main).unity` |
| **m11** | Цель «Не потерять все борта» неверна для обучения | Для `PreparingDemo` выводится «Борт можно заменить после потери» (по `sceneName`, `.asset`-файлы не менялись) | `MissionDetailsPanel.cs` |
| **m13** | Выход из игры без подтверждения | Двухшаговое «Выход» → «Выйти?» (3 с, `Esc` отменяет), затем `Quit()` | `MainMenuController.cs` |

### 10.2 Сознательно не менялось

* **M7** — пауза со стула (`Esc` сидя игнорируется) оставлена как есть по решению дизайна.
* **M8** — `ShopWindow` / `InteractionUIController.CloseShop()` не тронуты.
* **M12** — `SceneLoader.postLoadDelay = 1.5 с` оставлен: задержка скрывает телепортацию на стул после миссии.
* Остальные мелкие пункты (`m1` позиции подсказок, `m2` `Cursor.visible`, `m3` hover-звуки, `m5` превью миссий, `m6` скролл настроек, `m8` магазин, `m10` подписи «только для миссий», `m12` логи, `m14` разрешение в редакторе) — без изменений.

### 10.3 Проверка (Play Mode, 22.11.2026)

| Сценарий | Результат |
|---|---|
| Прямой старт `Bunker(main)` | `state = Gameplay`, `timeScale = 1`, `Cursor.Locked`, `Esc` открывает паузу |
| Панель миссий без ОС (закрытие) | `timeScale = 1`, курсор захвачен, `AnyOpen = false` |
| Панель миссий поверх ОС (закрытие) | `timeScale = 0`, ОС остаётся открытым |
| Открытие терминала | центральная подсказка `alpha = 0`; после закрытия снова `alpha = 1` |
| Панель миссий | автовыбор «Тренировки», 2 карточки, `goal1 = «Борт можно заменить после потери»`, у заблокированной — «Не потерять все борта» + `interactable = false` |
| `Esc` в панели миссий | закрывает всё за одно нажатие, возврат на рабочий стол |
| Двойной `OpenMissionMenu()` в одном кадре | 2 карточки (раньше 4) |
| Настройки: дропдаун | после закрытия по `X` и повторного открытия строк 13 (свёрнут) |
| Настройки: «Сброс» | 1-й клик — «Подтвердить?» (значения не меняются), 2-й — дефолты, панель открыта |
| Настройки: `X` / `Esc` | `MainPanel.alpha = 1`, панель закрыта |
| Выход из игры | 1-й клик — «Выйти?», `Esc` отменяет, таймаут 3 с возвращает «Выход», 2-й клик завершает Play |
| `MainMenu` без `MissionSelectPanel` | сцена чистая, `Play/Settings/Exit` работают |
| Полный цикл `MainMenu → Bunker → Pause → Settings → ComputerOS → MissionSelect → MainMenu → Bunker` | без ошибок, без дублей синглтонов (`SceneLoader/SettingsService/Pause/Computer = 1`) |
| Консоль Unity | 0 ошибок проекта (только известный внешний warning VideoPlayer `Color primaries`) |

---

## 11. Fix-QA тренировочной миссии `PreparingDemo` (22.11.2026)

Сквозной аудит цикла `Bunker(main) → ComputerOS → PreparingDemo → пауза/настройки → победа/поражение → Bunker(main)` выявил 4 критичных, 16 средних и 10 мелких дефектов. Исправления выполнены четырьмя этапами, каждый — с проверкой в Play Mode.

### 11.1 Что исправлено

| ID | Проблема | Исправление | Файлы |
|---|---|---|---|
| **C1** | Кольца «сгорали» до шага полосы препятствий → `2/3` и softlock | `TrainingGate.OnGatePassed` отдаёт арку; учёт в `TrainingManager` (`HashSet`) + `ResetGateProgress()` при входе в `StrikeFlyGates` и после замены борта | `TrainingGate.cs`, `TrainingManager.cs` |
| **C2** | Панель краха в обучении замораживала время и отменяла респавн | `CrashPanel` не открывается при наличии `TrainingManager`; респавн на `WaitForSecondsRealtime(1.5f)` + субтитр «БОРТ ПОТЕРЯН · ЗАМЕНА» | `MissionController.cs`, `TrainingManager.cs` |
| **C3** | Переход главы зависал при `timeScale = 0` | `ScreenFader` на `unscaledDeltaTime` / `WaitForSecondsRealtime` | `ScreenFader.cs` |
| **C4** | Прямой старт `PreparingDemo`: FSM оставался в `Boot`, дебрифа и прогресса нет | `GameStateBootstrap.EnsureGameplayState()` (+ `SettingsService`) в `MissionController.Awake`; защита дебрифа от `MissionContext.Current == null` | `GameStateBootstrap.cs` (новый), `MissionController.cs`, `DebriefController.cs` |
| **M1** | В паузе миссии не было настроек | Кнопка «Настройки» + инстанс `SettingsPanel.prefab`, `MissionUIManager.OpenSettings`, `Esc`: настройки → пауза → игра | `PreparingDemo.unity`, `MissionUIManager.cs` |
| **M2** | Пауза не останавливала звук, FSM не переходил в `Paused` | `ApplyPausedState`: `timeScale = 0`, `AudioListener.pause = true`, FSM `Paused`; клики UI через `ignoreListenerPause` | `MissionUIManager.cs`, `UISoundService.cs` |
| **M3** | `mapCenter = NULL` — границ полигона и «потери сигнала» не было | `MapCenter`, `safeRadius 220` / `maxRadius 300`, `outOfBoundsGrace 5 с`, события + автовозврат в зону, субтитр-предупреждение | `DroneManager.cs`, `TrainingManager.cs`, сцена |
| **M4** | `spawnPosChapter3 = Chapter2_spawn` — 315 м до танка | Отдельный `Chapter3_spawn` (45 м), `DroneManager.RespawnPoint` переключается по главам | сцена, `TrainingManager.cs`, `DroneManager.cs` |
| **M5** | Дебриф «3 / 1» | `Mission1.targetCount = 3` → «3 / 3», 175 очков | `Mission1.asset` |
| **M6** | Панели миссии в legacy-стиле (`UISprite`, серые кнопки, «Вы рызбились», `MISSION`/`STATS`) | `TrainingPanelsBuilder`: scrim 0.62, плита HUF, рамка + красные уголки, кнопки 460×60, TMP 30/24/18/15/11, исправленные тексты | `TrainingPanelsBuilder.cs` (новый), сцена |
| **M7** | `InstructionText` — мелкий текст без плашки, зелёные кейкапы; `FadeText` смещён влево | Плашка Tactical Metal + `Label` 18, кейкапы `#DB143D`; `FadeText` → `Center` 64 | `TrainingPanelsBuilder.cs`, `TrainingManager.cs` |
| **M8** | HUD на английском, зелёная палитра | OSD локализован, палитра `Steel` + `AccentRed` для захвата/РЭБ | `ReconHUD.cs`, `StrikeHUD.cs` |
| **M9** | ПНВ не работал: фича `NightVisionFeature` ≠ `"Night Vision Feature"` | Фича переименована в `Night Vision Feature`; `config` назначен; ложный `isNightVisionActive` сброшен | `PC_Renderer.asset`, префабы дронов |
| **M10** | Поиск ударного дрона по подстроке `"FPV"` | `DroneConfig.droneRole` (+ фолбэк по имени для старых конфигов) | `DroneConfig.cs`, `DroneManager.cs`, `.asset` |
| **M11** | Ввод двигал обучение/зум/захват/ПНВ на паузе | Guard `Time.timeScale <= 0` в `TrainingManager`, `DroneManager`, `TargetLockManager`, `FPVZoom`, `NightVisionController` | перечисленные скрипты |
| **M12** | `RestartMission()` всегда грузил `PreparingDemo`, без `Loading` | Рестарт по `MissionContext.Current?.sceneName`, FSM через `Loading`; `Loading` разрешён из `Gameplay`/`Paused` | `MissionUIManager.cs`, `GameStateMachine.cs` |
| **M13** | В дебрифе только «Вернуться в бункер» | Добавлены «ПОВТОРИТЬ» и «В ГЛАВНОЕ МЕНЮ» (+ `Esc` = в бункер) | `DebriefController.cs`, сцена |
| **M14** | Смена главы удаляла все `ParticleSystem`/глушила `AudioSource` в сцене | `CleanupChapter()` — только эффекты/звуки уходящих глав (`ParticleSystem.Clear`, без предупреждений sub-emitters) | `TrainingManager.cs` |
| **M15** | `trainingHints` без потребителя, субтитров нет | `trainingHints` скрывает текст инструктора; отдельный канал `SubtitleText` с `subtitlesEnabled`/`subtitleFontSize` | `TrainingManager.cs`, сцена |
| **M16** | Два активных `AudioListener` | `AudioListener` удалён у `BackgroundCamera` | сцена |
| **m1** | `TrainingStep.Completed` не выставлялся | Шаг и глава выставляются явно перед завершением | `TrainingManager.cs` |
| **m2** | Дрон падал с 10 м при спавне | `ResolveSpawnPosition()` — raycast до опоры + зазор 0.8 м | `DroneManager.cs` |
| **m3** | Триггеры колец не совпадали с арками (перепутаны оси) | BoxCollider = мировой AABB рендерера с учётом поворота | сцена |
| **m4** | Пустые VFX/дым у целей, модулей и стены | `BigExplosion`, `VFX_Fire_01_Medium_Smoke`, `SmokeEffect`, `dust` | сцена |
| **m6** | `DebriefCamera` в `(0,0,0)` | Ракурс `(-49, 57.7, -40.8)` на танк | сцена |
| **m9** | После возврата выделялась пройденная миссия | Автовыбор первой непройденной доступной миссии | `MissionSelectScreen.cs` |
| **m10** | «Борт можно заменить» противоречило поведению | Теперь соответствует: замена борта реально работает | `TrainingManager.cs` |

**Не менялось (по решению):** `Mission1.description` (m5), бонус за время/`timeLimit` (m7), `GimbalDrone Variant/DroneCollision.config` (m8).

### 11.2 Проверка (Play Mode, 22.11.2026)

| Сценарий | Результат |
|---|---|
| Прямой старт `PreparingDemo` | `FSM = Gameplay`, `SettingsService` поднят, настройки применены (FOV 100, farClip 2000); завершение → `Debrief` |
| Кольца | ранний пролёт игнорируется, при входе в шаг сбрасывается, 3/3 → `StrikeDestroyTarget` |
| Потеря борта | `CrashPanel` не открывается, `timeScale = 1`, субтитр «БОРТ ПОТЕРЯН · ЗАМЕНА 1.5 с», новый борт на земле (y ≈ 0.9), шаг сохранён |
| Переход глав | глава 2 у дома, глава 3 — 45 м от танка (было 315 м); фейдер работает и при `timeScale = 0` |
| Пауза/настройки | `Esc` → `Paused`, `timeScale 0`, звук на паузе; настройки 13/10/8/12; `Esc` → пауза → игра |
| ПНВ | фича найдена, `N` включает/выключает, ошибок `[NV]` нет |
| Границы | вылет за `maxRadius` → помехи 100 %, субтитр, через 5 с возврат в зону |
| Дебриф | «3 / 3», 175 очков, `progress.json` записан, «Тренировка» помечена выполненной |
| Возврат в бункер | игрок сидит, ОС и меню миссий открыты, автовыбор «БТР и РСЗО» |
| Выход в меню | бункер → пауза → «Выйти в главное меню» → `MainMenu`, синглтоны по 1 |
| Консоль | **0 ошибок и 0 предупреждений проекта** (внешний `Color primaries` остаётся) |

### 11.3 Известные ограничения

* Быстрый повторный клик по «Играть» **во время** экрана загрузки `MainMenu` может «съесть» загрузку (`SceneLoader` игнорирует повторный вызов, FSM остаётся в `Loading`); повторный клик восстанавливает переход. UI-загрузка блокирует клики, поэтому в обычной игре сценарий маловероятен.
* `CrashPanel` в обучении больше не показывается — он остался рабочей панелью для миссий без `TrainingManager` (`FirstMission(Forest)` его не вызывает).
* `LiberationSans SDF - Fallback.asset` после прогонов откатан через `git checkout` (все нужные глифы есть в основном `LiberationSans SDF`).

### 11.4 Клики мышью в панелях `PreparingDemo` (22.11.2026)

Симптом: `Esc` открывал/закрывал паузу, но кнопки `PausePanel`, `CrashPanel` и `DebriefPanel` не реагировали на мышь (в `MainMenu` и `Bunker(main)` всё работало).

| Что | Причина | Исправление | Файлы |
|---|---|---|---|
| Мышь не нажимает кнопки панелей | Панель отпускала курсор (`CursorLockMode.None`), `DroneFlightController.Update()` (строки 113‑119, выполняется и при `timeScale = 0`) по первому ЛКМ захватывал его, а `InputSystemUIInputModule` при `Locked` уводил UI‑указатель в `(-1,-1)` (`InputSystemUIInputModule.cs:428`) — рейкаст не попадал ни в один `Graphic` | Панели миссии держат `CursorLockMode.Confined` (не `None` — дрон не захватывает; не `Locked` — указатель жив). `MissionUIManager.LateUpdate()` при `IsModalOpen` удерживает `Confined` + `visible` | `MissionUIManager.cs`, `DebriefController.cs` |
| «Спящий» перехватчик лучей | `ScreenFader` лежит выше панелей, его `Image`/`FadeText` имели `raycastTarget = true`, а `FadeOut()` включает `blocksRaycasts` на время перехода главы | `TrainingPanelsBuilder.HardenClickThrough()`: у фейдера `raycastTarget = false` у всех `Graphic`, `blocksRaycasts = false`, `siblingIndex = 1`; `SettingsPanel` — последний; в панелях `raycastTarget` только у `Image` кнопок и корневого скрима; `LogError` при отсутствии `GraphicRaycaster`/`EventSystem`/`InputSystemUIInputModule` | `TrainingPanelsBuilder.cs`, `PreparingDemo.unity` |

Физика дрона (`DroneFlightController`, `DroneMotors`, PID, `DroneCollision`) не менялась; `MainMenu` и `Bunker(main)` не затрагивались.

Проверка (Play Mode, 22.11.2026):

| Сценарий | Результат |
|---|---|
| Прямой старт `PreparingDemo` → `Esc` | `FSM = Paused`, `timeScale = 0`, `Cursor.lockState = Confined`, `visible = true`, `IsModalOpen = true` |
| `RaycastAll` по кнопкам паузы | верхний хит — `continue` / `settings` / `exit`, обработчик клика — сама кнопка |
| ЛКМ при открытой панели | курсор остаётся `Confined`, указатель UI не `(-1,-1)` (замер: `pos ≈ (971, 486)`, `raycast = exit`) |
| Имитация захвата курсора дроном (`lockState = None`) | `LateUpdate` возвращает `Confined` + `visible` |
| «Продолжить» | `Gameplay`, `timeScale = 1`, `Locked` + `visible = false`, дрон управляется (DFC `enabled = true`) |
| «Настройки» | `Confined`; верхний хит — `CloseButton`; `Esc` закрывает только настройки, второй `Esc` снимает паузу |
| `CrashPanel` | `Confined`; верхние хиты `return` / `exit` |
| `DebriefPanel` | `Confined`; верхние хиты `RetryButton` / `ReturnButton` / `MainMenuButton`; клик «Вернуться в бункер» → `Loading` → `Bunker(main)` |
| Запуск `PreparingDemo` из `Bunker(main)` | `EventSystem = 1`, `Canvas = 1`, `MissionContext = training_mission`; пауза/клики работают идентично |
| Консоль | 0 ошибок; `git status` — только `MissionUIManager.cs`, `DebriefController.cs`, `TrainingPanelsBuilder.cs`, `PreparingDemo.unity` (+ `Docs/`) |

Примечание: при автовыборе в бункере «Тренировка» уже помечена выполненной, поэтому первым выбирается `mission_1` (`FirstMission(Forest)`). Для проверки путь из бункера запускался явно на `training_mission` (тот же код, что у кнопки «ЗАПУСТИТЬ»); запрещённая сцена не изменялась.





