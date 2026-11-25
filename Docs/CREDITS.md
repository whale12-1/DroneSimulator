# CREDITS — сторонние ассеты и атрибуция

> Обновлено: **25.11.2026** (ветка `bunker/story`, реквизит `BunkerStoryProps`).
> Правило проекта: в игру допускаются только модели со свободными лицензиями — **CC0** или **CC-BY** (CC Attribution), и только при наличии единого файла **`.glb`**.

Модели скачиваются консольным помощником `Tools/sketchfab_fetch.py` (Sketchfab Data API v3, токен из переменной окружения `SKETCHFAB_API_TOKEN`):

```powershell
python Tools/sketchfab_fetch.py likes --limit 40 --downloadable
python Tools/sketchfab_fetch.py info <uid>
python Tools/sketchfab_fetch.py download <uid> --out Assets/Models/Props/Bunker
python Tools/sketchfab_fetch.py thumb <uid> --out Temp/<name>.jpg
```

---

## 1. Реквизит бункера (`Assets/Models/Props/Bunker/`)

| Пропс в сцене | Модель | UID | Автор | Лицензия | Полигоны (faces / verts) | Файл |
|---|---|---|---|---|---|---|
| `BunkerStoryProps/Prop_MilitaryBed` | Asylum Bed | `4b0d66f33ec94374a855b1b7cd454252` | FallenBranches | CC Attribution (CC-BY) | 10 096 / 5 224 | `Assets/Models/Props/Bunker/Asylum_Bed_4b0d66f33ec94374a855b1b7cd454252.glb` |
| `BunkerStoryProps/Prop_WalkieTalkie` | Military Radio Transceiver | `8654fbb33d544e1cb7d35667f3859c89` | RidwanManda | CC Attribution (CC-BY) | 2 592 / 1 343 | `Assets/Models/Props/Bunker/Military_Radio_Transceiver_8654fbb33d544e1cb7d35667f3859c89.glb` |
| `BunkerStoryProps/Prop_RadioReceiver` | Citizen Radio | `9579861bbfae44db943572b929218b74` | EvesStudio2008 | CC Attribution (CC-BY) | 1 026 / 610 | `Assets/Models/Props/Bunker/Citizen_Radio_9579861bbfae44db943572b929218b74.glb` |

Страницы моделей: `https://sketchfab.com/models/<uid>`.

### 1.1 Текст атрибуции (обязателен для CC-BY)

> «Asylum Bed» by FallenBranches — CC Attribution, via Sketchfab.
> «Military Radio Transceiver» by RidwanManda — CC Attribution, via Sketchfab.
> «Citizen Radio» by EvesStudio2008 — CC Attribution, via Sketchfab.

Внутриигровые кредиты пока не выводятся (в проекте нет экрана кредитов) — атрибуция хранится в этом файле и должна попасть в экран кредитов/README билда.

---

## 2. Удалённые модели (25.11.2026)

Файлы и их `.meta` удалены из `Assets/Models/Props/Bunker/`, объекты заменены в сцене `Bunker(main)`:

| Было | UID | Где стояло |
|---|---|---|
| `Bed_-_Low_Poly_Medieval_CC0_24704040010a42ceaeffb1631ed1ae9b.glb` | `24704040010a42ceaeffb1631ed1ae9b` | `Prop_MilitaryBed` (угол у `Door1`) |
| `Telephone_e2389b65271f4a57906fe194dbd0e56a.glb` | `e2389b65271f4a57906fe194dbd0e56a` | `Prop_FieldTelephone` (настенный телефон на шкафу консоли) |
| `The_Marconi_radio_set_4-LS_I_model_ee66cc8a387b4cf99aef6a463042db4c.glb` | `ee66cc8a387b4cf99aef6a463042db4c` | `Prop_RadioReceiver` (радио на консоли) |

---

## 3. Прочие сторонние наборы (без изменений)

Полный перечень сторонних папок проекта (`Strategic Warfare UI Starter Pack`, `UnityTechnologies/ParticlePack`, `Volklight/BTR`, `ZIL130_MilitaryTruck`, `S-Mine 35`, `ALP_Assets`, террейн-ассеты и т. д.) см. `ARCHITECTURE.md` §1 и `INDEX.md`. Их лицензии ведутся отдельно и в этой задаче не менялись.
