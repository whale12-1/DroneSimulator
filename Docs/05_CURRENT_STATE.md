# 05 — Текущее состояние проекта

Дата аудита: **21.11.2026**. Оценка сделана по коду и ассетам (комментариев-маркеров `TODO/FIXME` в проекте **нет** — их искали по всем `.cs`).

---

## 1. Что реализовано и работает (по сценам)

### `Boot`
* ✅ Подъём `SceneLoader` + `SettingsService`, переход в `MainMenu`.
* ✅ Экран загрузки с `CanvasGroup`.

### `MainMenu`
* ✅ Видео-фон (VideoPlayer → RenderTexture).
* ✅ Кнопки Play / Settings / Exit.
* ✅ Полноценная панель настроек (4 вкладки, ~48 параметров), сброс и сохранение.
* ⚠️ Панель выбора миссий присутствует в сцене, но недостижима (см. долг №4).

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
* ✅ Пауза (`Esc`), настройки, выход в главное меню.
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
* ✅ Настройки: `settings.json` с версионированием и атомарной записью; применение на каждой сцене.
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
| 1 | **Инверсия Motion Blur** | `PostFxRouter.cs:49` | `settingsMotionBlur.active = !enabled;` — при включённой настройке эффект выключается, и наоборот |
| 2 | **Рестарт миссии всегда грузит обучение** | `MissionUIManager.cs:87` | `LoadAsync("PreparingDemo")` вместо `MissionContext.Current.sceneName` |
| 3 | **Битые кодировки (невалидный UTF-8)** — 7 файлов | `TrainingGate.cs`, `NIghtVisionController.cs`, `HimarsStrike.cs`, `TargetPingSystem.cs`, `VehicleDust.cs`, `WheelRotator.cs`, `ScreenFader.cs` | Русские комментарии и часть `[Header]`-строк нечитаемы; риск дальнейшей порчи при пересохранении |
| 4 | **Двойное кодирование (mojibake)** | `DamageSystem.cs`, `VehicleSmartAI.cs` | Комментарии вида `РЎРµРЅСЃРѕСЂС‹`; файлы валидны как UTF-8, но текст уже потерян |
| 5 | **`WindowMode` сбрасывается при миграции** | `SettingsData.cs:124` (`version = 2`) vs `SettingsService.cs:11` (`SchemaVersion = 3`) | Новый `SettingsData` сразу проходит миграцию и переопределяет `windowMode` из `fullscreen` |

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

---

## 6. Рекомендованный порядок работ

1. **Быстрые победы (P0):** инверсия Motion Blur, `RestartMission`, выравнивание `SchemaVersion`/`SettingsData.version`.
2. **Гигиена кодировок:** конвертация 7 файлов в UTF-8 (без изобретения нового текста — просто сохранить как UTF-8 с текущими символами, где возможно; где текст потерян — переписать осмысленно).
3. **Развязка слоёв:** убрать `BunkerStateRestorer` из `SceneLoader` (сделать событие `SceneLoader.SceneReady`), убрать `"PreparingDemo"` из `MissionController` (перенести логику crash-панели в `TrainingManager`), заменить поиск по `"FPV"` на явный `DroneRole`/флаг в `DroneConfig`.
4. **Единый реестр модальных окон** бункера вместо ручных таймеров (стек окон с приоритетом).
5. **Удаление мёртвого кода и сцен** (после подтверждения, что соответствующие фичи не вернутся).
6. **Тесты/валидация:** сейчас нет ни одного unit-теста. Минимально полезные — `ProgressService.ComputeScore`, `ProgressService.IsUnlocked`, `GameStateMachine.CanTransitionTo`, `MissionText.*`.

---

## 7. Итоговая оценка

Проект находится в состоянии **играбельного вертикального среза**: полный цикл «меню → бункер → обучение → боевая миссия → дебриф → бункер» работает, прогресс и настройки сохраняются, есть два разных по механике уровня и общая боевая система (физика дрона, урон по модулям, РЭБ, HUD, ПНВ).

Основные слабые места — не геймплей, а **связность**: ядро знает про конкретные сцены, директора миссий дублируют логику `MissionController`, UI-модальность держится на таймерах, а часть файлов имеет повреждённые кодировки. Всё это локально и чинится без переписывания проекта.


