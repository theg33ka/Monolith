# MASTER PROMPT — KIAS programmable controller / graph integration pass

Ты работаешь в актуальной локальной ветке `KIAS` репозитория Monolith. Базовая KIAS и первый большой patch/pass уже были выполнены. **Не переписывай KIAS с нуля и не откатывай решения первого патча.** Текущий checkout — источник истины по фактическому состоянию кода; документы этого пакета — продуктовая спецификация следующего прохода.

## 0. Цель этого прохода

Добавить к KIAS физическую, переносимую и полностью рабочую систему программируемых схем в стиле PLC/Wiremod:

1. Игрок крафтит `KIAS programmable controller` — физическую карточку/модуль со схемой.
2. Вставляет её в отдельную `KIAS controller programming console`.
3. Получает большой node-editor.
4. Перетаскивает в него конкретные устройства KIAS или виртуальные селекторы `ANY` / `ALL` по типу устройства.
5. Видит реальные typed input/output ports.
6. Добавляет логические/временные/state/value nodes.
7. Соединяет порты проводами, задаёт имя и параметры.
8. Нажимает `WRITE` — программа атомарно записывается в физический controller item.
9. Вынимает controller и вставляет его в подключённый к KIAS серверный шкаф/rack на **8 слотов**.
10. Только controller, находящийся в запитанном и Online KIAS rack, исполняет схему.
11. До 8 controller cards в одном rack исполняются независимо и реально увеличивают потребление энергии.
12. Существующая автоматика KIAS/«протоколы» должна быть перенесена с hardcoded protocol/action engine на обычные graph-программы/presets этой же системы.

Это не mockup, не fake UI и не новый параллельный KIAS. Нужен законченный gameplay path `prototype -> item/machine -> BUI -> compile -> runtime -> device I/O -> tests`.

## 1. Обязательный preflight

Перед кодированием:

- `git status`, текущая ветка, HEAD, diff;
- открыть фактические `Content.Server/_Forge/KIAS`, `Content.Shared/_Forge/KIAS`, `Content.Client/_Forge/KIAS`, `Resources/Prototypes/_Forge/KIAS`, `Docs/KIAS`, `Content.IntegrationTests/Tests/_Forge/KIAS`;
- найти изменения, сделанные первым патчем: centralized KIAS access resolver, standard wire bypass, management console/UI split, coverage diagnostics, anti-spam/perf changes, physical prerequisites и остальные реально применённые части;
- **не дублировать** уже существующие системы;
- если имена/paths из документов устарели — использовать текущие эквиваленты;
- сохранить совместимость с новым access model: programmer/rack/configuration используют централизованный KIAS authorization helper, а не прямой `OwnerUserId`;
- собрать baseline и прогнать текущие KIAS tests перед изменениями.

Исторический initial master prompt и первый patch prompt лежат в `history/`. Они нужны как контекст принятых решений, но не являются командой заново повторить работу.

## 2. Внешние референсы, которые надо изучить, но не копировать вслепую

### /tg/station Wiremod / Integrated Circuits

Изучить актуальные эквиваленты:

- `code/modules/wiremod/core/integrated_circuit.dm`
- `code/modules/wiremod/core/component.dm`
- `code/modules/wiremod/core/port.dm`
- `code/modules/wiremod/core/duplicator.dm`
- `tgui/packages/tgui/interfaces/IntegratedCircuit/*`

Нам нужны идеи:

- node graph;
- typed ports;
- output fan-out;
- event/dataflow execution;
- drag/drop;
- pan/zoom;
- component positions;
- serialization;
- component categories/search;
- visual wires.

Не тащить DM runtime и React/TGUI 1:1. Реализация должна быть нативной C#/Robust UI.

### WizDen DeviceLinking

Изучить локальные:

- `Content.Shared/DeviceLinking/*`
- `Content.Server/DeviceLinking/*`
- `Content.Client/NetworkConfigurator/*`

DeviceLinking использовать как bridge к существующим source/sink ports машин. Внутренний граф controller должен иметь собственную строгую типизацию и не должен превращаться в набор скрытых DeviceLink entities.

### Goob nested filters

Изучить как reference составных фильтров/предикатов. Не тащить Factory stack без реальной необходимости. Если появится полезная `Entity Filter` node — портировать только минимальную abstraction и соблюдать attribution.

## 3. Физические объекты

### 3.1 KIAS programmable controller

Физический переносимый item. Хранит persistent program:

- `ProgramVersion`;
- `ProgramName`;
- node records;
- wire records;
- stable node IDs;
- node configuration;
- specific-device bindings;
- selector device-profile IDs;
- UI positions;
- revision/hash при необходимости.

Controller **не выполняется**:

- в руке;
- на полу;
- в обычном storage;
- в programming console.

Он выполняется только в Online powered rack.

Использовать предоставленный sprite `assets/raw/kias_programmable_controller.png` либо ready-to-wrap RSI из `assets/rsi_ready/KiasProgrammableController.rsi`. Пиксели не перерисовывать без необходимости.

### 3.2 KIAS controller programming console

Отдельная машина:

- KIAS device;
- питание;
- DATA connectivity;
- один реальный slot для controller;
- отдельный BUI node editor;
- access через текущий centralized KIAS access system.

Без Online KIAS console может показать содержимое карточки, но не должна разрешать актуальный device discovery/rebinding/WRITE, если текущая security/online модель этого не допускает. Выбрать консистентное поведение и покрыть тестами.

### 3.3 KIAS controller rack

Отдельный серверный шкаф:

- KIAS device;
- APC power receiver;
- DATA connectivity;
- **ровно 8 реальных slots**;
- whitelist controller item;
- вставка используемого controller item — в первый свободный slot;
- девятый controller — отказ + popup;
- status UI 01..08: `EMPTY / RUNNING / OFFLINE / INVALID / FAULT`;
- examine: KIAS status, controllers X/8, running count, current load.

Использовать предоставленный sprite `assets/raw/kias_controller_rack.png` либо `assets/rsi_ready/KiasControllerRack.rsi`.

## 4. Питание

Rack реально потребляет энергию через штатную power subsystem.

Сделать configurable DataFields, например:

- `BasePowerLoad`;
- `PerControllerLoad`.

Формула:

`rack load = base + insertedControllerCount * perController`

Использовать существующий `SharedPowerReceiverSystem.SetLoad()` / актуальный серверный эквивалент.

При:

- power loss;
- KIAS DATA loss;
- KIAS hard OFF;
- rack deletion;
- controller eject/delete;
- grid transfer/split;

runtime соответствующих cards должен немедленно остановиться, очереди очиститься, timers отмениться, volatile state сброситься. После восстановления — clean/cold boot и `ON START`.

## 5. Graph data model

Создать отдельную strongly typed serializable graph model. Не использовать arbitrary `Dictionary<string, object>`.

Ориентир:

```text
KiasControllerProgram
  Version
  Name
  Nodes[]
  Wires[]

KiasControllerNodeRecord
  Id (stable)
  Kind/Profile
  X/Y
  Config

KiasControllerWireRecord
  FromNodeId / FromPortId
  ToNodeId / ToPortId
```

Лимиты server-side, минимум:

- 128 nodes/controller;
- 256 wires/controller;
- 32 external device/selector nodes/controller;
- max 256 chars string constant/message;
- bounded output fan-out;
- bounded timer/clock ranges.

Добавить schema version + migration point.

## 6. Port types

V1:

- `Signal`
- `Bool`
- `Number`
- `String`
- `Entity`
- `Enum/ContactDisposition`

`Signal` — pulse/event. Остальные — typed value/event stream с последним runtime value там, где это логично.

Никаких unsafe implicit conversions. Если conversion нужен — отдельная node.

Обычный data input: максимум один source. Signal input может принимать несколько sources.

## 7. External device profile abstraction

Не делать giant switch по prototype IDs.

Добавить расширяемую abstraction типа:

```text
KiasControllerDeviceProfile / Prototype
  Id
  DisplayName
  Compatible entity predicate/component marker
  Ports[]
```

или event-driven query (`KiasGetControllerPortsEvent`) + стабильный `ProfileId`.

Критично: **тип/профиль устройства должен быть стабильнее конкретного EntityUid и по возможности объединять варианты одного gameplay-устройства**. Например все варианты KIAS wall speaker должны иметь один controller profile `Speaker`, все weapon flash detectors — `WeaponFlashDetector`.

Это необходимо для переносимых `ANY/ALL` программ.

Port descriptor:

- stable PortId;
- localized name/description;
- direction;
- KiasPortType;
- event/value semantics;
- optional metadata.

Device I/O должно идти через reusable `KiasControllerIoSystem`/adapter. Переиспользовать существующие subsystem APIs, не менять чужие component fields напрямую, если есть system method.

## 8. Три способа адресовать устройство

### 8.1 SPECIFIC

Обычная device node привязана к конкретной entity текущего grid.

Хранит:

- binding/reference;
- profile ID;
- fallback display name;
- snapshot port schema.

При переносе карточки на другой shuttle specific node становится `UNAVAILABLE`; никакого auto-rebind по имени/ближайшему prototype.

### 8.2 ANY — любой экземпляр выбранного типа

**Это обязательная отдельная node.**

Игрок создаёт `ANY`, выбирает `Device Profile`, например `WeaponFlashDetector`, после чего node показывает **те же gameplay ports**, что обычная node этого устройства.

Семантика:

- selector динамически отслеживает все Online KIAS devices текущего grid с выбранным profile;
- **output events от любого matching device объединяются в outputs node**;
- пример: `ANY WeaponFlashDetector.Triggered` срабатывает, если сработал хотя бы один датчик на корабле;
- для каждого emitted event/value runtime также должен знать `SourceEntity`; добавить универсальный metadata output `Source` там, где это полезно и не конфликтует с profile;
- input command в `ANY` отправляется **одному** matching Online device;
- выбор по умолчанию детерминированный `FirstAvailable`/stable order. Не использовать случайность, если она не даёт gameplay-пользы;
- если позже легко добавить `PickMode = FirstAvailable | Random`, это допустимо, но `FirstAvailable` — обязательный и тестируемый baseline;
- если matching devices нет — node остаётся валидной, но показывает `0 matched`, ничего не исполняет и не fault'ит весь controller.

`ANY` является portable binding: после переноса карточки на другой корабль автоматически работает с устройствами того же profile на новом KIAS grid.

### 8.3 ALL — все экземпляры выбранного типа

**Это обязательная отдельная node.**

Игрок выбирает profile и видит тот же schema ports.

Семантика:

- output events от всех matching devices merge в один stream node; событие любого экземпляра проходит дальше;
- input command/value broadcast'ится **каждому** matching Online device;
- отсутствие matching devices не делает program structurally invalid;
- selector автоматически обновляется при topology/power/device changes;
- перенос на другой shuttle автоматически re-resolve'ит matching devices нового grid.

Пример переносимой схемы:

```text
ANY WeaponFlashDetector.Triggered
       -> [Disposition == Hostile]
       -> IF
       -> ALL Speaker.Announce
```

Такая карточка не содержит конкретных UIDs и должна одинаково работать на любом корабле, где есть KIAS, хотя бы один weapon-flash detector и speakers.

### 8.4 Общие selector metadata ports

Добавить полезные унифицированные outputs, если архитектурно чисто:

- `MatchedCount : Number`
- `OnlineCount : Number` (может совпадать с MatchedCount)
- `HasAny : Bool`
- `Source : Entity` для последнего emitted device event.

Не ломать profile ports ради этих metadata; оформить отдельной секцией/именами.

### 8.5 Optional selector filters

Если уже существуют KIAS room/group/tag metadata, можно поддержать portable filters:

- room;
- group;
- custom tag;

Default = wildcard. Не делать это обязательной зависимостью первой рабочей версии, если замедлит реализацию, но архитектура selector не должна мешать добавить фильтры позже.

## 9. DeviceLink bridge

Generic Online KIAS device с `DeviceLinkSourceComponent` / `DeviceLinkSinkComponent` должен иметь возможность экспортировать ports в graph.

Generic DeviceLink port без KIAS typed adapter = `Signal`.

Если текущий `DeviceLinkSystem` не предоставляет безопасный reusable hook для:

- наблюдения за source invocation;
- прямого вызова sink port;

добавить минимальный general-purpose API/event, сохраняя invoke/overload/loop protection.

Не генерировать `SignalReceivedEvent` в обход защиты, если можно сделать штатный direct-invoke API.

## 10. Internal node catalog

Обязательно:

**Lifecycle**
- ON START

**Values**
- BOOL CONSTANT
- NUMBER CONSTANT
- STRING CONSTANT

**Logic**
- AND
- OR
- XOR
- NOT
- NAND
- NOR
- XNOR

**Control**
- IF / ELSE

**Timing**
- TIMER / DELAY
- CLOCK

**State**
- LATCH / MEMORY BOOL
- TOGGLE
- COUNTER
- EDGE DETECTOR

**Comparison**
- Number `== != < <= > >=`
- Bool equality
- String equality
- ContactDisposition/Enum equality

Использовать семантику существующих SS14 LogicGate/SignalTimer как reference/reusable code where appropriate, но не создавать hidden entity на каждую node.

## 11. Execution model

Только server-authoritative, event-driven.

```text
external event / timer
 -> endpoint index
 -> controller queue
 -> node evaluation
 -> changed outputs/signals
 -> downstream queue
 -> actuator/device adapters
```

Не пересчитывать весь graph каждый frame.

При controller activation:

- validate;
- compile;
- build adjacency/index;
- register SPECIFIC + ANY/ALL subscriptions;
- initialize constants/state;
- emit ON START.

При topology/device changes:

- specific availability update;
- ANY/ALL matcher sets update;
- **не перекомпилировать весь program**, если не требуется;
- selector indexes обновлять инкрементально/по dirty revision.

## 12. Cycle protection

Compile validation:

- найти combinational SCC/cycles;
- reject pure instantaneous cycle;
- разрешать feedback только через явно state/time-breaking nodes (Timer/Latch/Counter/etc.).

Runtime safety:

- max ~1024 node evaluations на один external event/controller (настраиваемо);
- overflow => controller `FAULT`, понятная причина, без server crash.

## 13. Programmer UI

Сделать настоящий Robust UI node editor, не список строк.

Нужно:

- large canvas;
- pan;
- zoom;
- optional grid/snapping;
- node drag;
- Bezier wires;
- type colors;
- compatible-port highlight;
- palette + search;
- categories;
- selected-node config;
- program name;
- WRITE;
- validation/status panel;
- dirty state;
- explicit discard or safe eject policy.

Palette devices должна иметь три UX-входа:

1. `SPECIFIC DEVICES` — список конкретных Online devices текущего KIAS grid;
2. `ANY DEVICE TYPE` — выбрать controller device profile;
3. `ALL DEVICES OF TYPE` — выбрать controller device profile.

Для ANY/ALL показывать `matched N` live в editor.

Wires рендерить разумным числом сегментов/curve helper. Не повторять старую схему NetworkConfigurator с тысячами точек на кривую.

## 14. Draft / WRITE

Редактор работает с server-side draft.

- insert card -> deep-copy persistent program в draft;
- UI edits draft;
- WRITE -> full server validation + compile;
- success -> atomic replace card program, revision++;
- structural errors -> не записывать;
- temporarily missing specific/selector devices могут быть warning, а не structural error;
- dirty draft нельзя потерять молча при eject.

## 15. KIAS events и device ports

Минимально экспортировать существующие функции KIAS как controller ports. Точные названия подогнать под реальный код.

Источники:

- Room Scanner: motion/presence, Entities, Crew, biometric/spectral etc.;
- Hull Sensor: impact;
- Integrity Monitor: damage/amount/source;
- Collision Monitor: collision/relative speed/other grid;
- Atmos/Fire: danger/fire/source;
- Power: deficit/supply/consumption/channel;
- Horizon: contact/entity/distance/bearing/disposition;
- Weapon Flash Detector: trigger/source/disposition;
- Proximity: contact/entity/distance/disposition;
- Crew: critical/dead/person/presence changes;
- Navigation: arrival/contact/mayday-capable events.

Исполнители:

- Speaker: Message + Announce/Speak/Alarm;
- Recorder/Black Box: Message + Record;
- Light Controller: bool/set/on/off;
- Suppression: Trigger;
- Relay: Closed/Open/Close;
- PDC/Defence: Automatic bool/enable/disable;
- Navigation/Comms: Mayday trigger/message where allowed;
- existing door/shutter/vent/device adapters through DeviceLink where appropriate.

Сохранять физические prerequisites первого патча: graph не должен магически получать rich IFF/classification без требуемого Online receiver, если это уже было принято/реализовано.

## 16. Полная миграция существующих KIAS protocols в graphs

**Это новое продуктово обязательное требование.**

Существующий `KiasProtocolSystem` не должен оставаться вторым постоянным rule engine рядом с controllers.

Нужно разделить:

### Оставить

- raw event producers;
- subsystem algorithms (например PDC threat tracking/interception, power stats, sensor detection, FireControl integration);
- low-level safe actuator methods;
- alert/state data, если оно является состоянием подсистемы, а не decision policy.

### Убрать/вывести из active gameplay

- hardcoded `trigger -> conditions -> actions` policy execution;
- отдельный protocol editor как основной способ автоматизации;
- скрытые default actions, которые нельзя увидеть как graph.

### Сделать вместо этого

Создать prototype/data-driven library `KiasControllerProgramPrototype` (или эквивалент) с готовыми graph presets.

Перенести **все текущие default protocols** в обычные graph programs, минимум:

- Battle Alert;
- Point Defence orchestration;
- Contact;
- Decompression;
- Fire;
- Emergency Power;
- Anomaly Growth;
- Autopilot Arrival;
- Critical Vessel;
- Medical Assistance;
- MAYDAY;
- прочие реально существующие в текущей KIAS accepted/default automations.

Каждый preset должен открываться в том же node editor и быть обычной редактируемой схемой.

Portable presets должны по максимуму использовать `ANY/ALL` device selectors, а не specific UIDs.

Пример:

```text
ANY WeaponFlashDetector.Triggered
 -> [Disposition == Hostile]
 -> IF.True
 -> ALL Speaker (message = ...)
 -> ALL/filtered LightController
 -> ALL DefenceController.Automatic = true
 -> ALL Recorder.Record
```

Не пытаться переносить тяжёлый realtime PDC trajectory algorithm в graph. Graph только включает/настраивает существующий Defence/PDC subsystem; сам interception остаётся оптимизированным subsystem code.

### Legacy compatibility

Перед удалением runtime старых protocols:

- найти save/map compatibility requirements;
- сделать deterministic migration path старых `KiasProtocolRecord` в graph representation **либо** явный one-version compatibility importer;
- не оставлять два rule engines навсегда;
- migration должна быть покрыта tests;
- документы должны объяснять, что old protocols deprecated/migrated.

Если текущая dev-ветка не требует сохранения старых round/map saves, всё равно удалить дублирующую active policy архитектуру чисто и обновить prototypes/docs/tests.

Management console после миграции должна показывать automation/controller status и направлять к programmer/preset workflow, а не сохранять второй полноценный protocol editor.

## 17. Preset workflow

В programming console дать удобный `NEW FROM PRESET` / `LOAD PRESET`:

- создаёт draft на вставленной card;
- показывает обычный graph;
- пользователь может редактировать;
- WRITE делает это обычной portable program.

Не создавать «магические встроенные протоколы», которые видны в UI, но исполняются другим кодом.

## 18. Security

Любое BUI message недоверенное.

Server-side проверять:

- actor/access;
- reach/interaction;
- console/rack/controller still exists;
- same grid;
- KIAS status;
- node kind/profile valid;
- device binding valid;
- selector profile allowed;
- ports exist/direction/type;
- config bounds;
- strings;
- counts/limits;
- cross-grid target denied;
- stale specific UID denied at execution;
- ANY/ALL resolver никогда не выходит за current rack grid.

Wire-cut bypass/access semantics первого патча должны продолжать работать штатно.

## 19. Persistence

Persistent:

- card graph;
- name;
- nodes/wires/config;
- specific binding refs в формате, корректно remap'ящемся при map save/load;
- selector profile IDs;
- UI positions.

Runtime only:

- compiled adjacency;
- queues;
- timers;
- volatile latch/counter values;
- selector matched UID sets;
- subscriptions;
- last faults.

Specific binding при save/load того же grid должен remap корректно. При переносе physical card на другой ship — старые specific bindings unavailable. ANY/ALL re-resolve автоматически.

## 20. Performance

Цель проекта всё ещё ~200 KIAS-equipped grids.

Запрещено:

- frame × every controller × every node;
- frame × every selector × every KIAS device;
- full graph reevaluation on every event;
- per-node ECS entity;
- full-grid scan при каждом ANY/ALL event;
- synchronized timer spikes всех controllers;
- massive BUI state every tick.

Нужны индексы примерно:

```text
(grid, profileId) -> online devices
(grid, deviceUid, portId) -> subscribed endpoints
(grid, profileId, portId) -> ANY/ALL subscribed endpoints
```

Topology/device revision обновляет selector match sets пачкой/dirty, а не world scan.

Timers/clocks использовать centralized scheduler/min-heap/buckets, а не Update на каждую node.

Benchmark:

- 8 controllers в одном rack;
- ~100 nodes/controller;
- ~200 wires/controller;
- несколько timers/clocks;
- десятки ANY/ALL selectors;
- 50/200 KIAS grids;
- burst sensor events;
- topology churn;
- all hard-off cost near zero.

## 21. Tests — обязательный минимум

### Physical lifecycle

1. controller на полу не выполняется;
2. в programmer не выполняется;
3. только powered+Online rack выполняет;
4. rack ровно 8 slots;
5. 9-й не вставляется;
6. load меняется insert/eject;
7. power loss stops;
8. DATA loss stops;
9. hard KIAS OFF stops;
10. restore => clean boot;
11. timer cancelled on stop;
12. controller survives eject/reinsert.

### Graph

13. AND/OR/XOR/NOT/NAND/NOR/XNOR;
14. IF/ELSE;
15. Timer/Clock bounds;
16. Latch/Toggle/Counter/Edge;
17. comparisons;
18. wrong types rejected;
19. dangling wires safe;
20. duplicate node IDs rejected;
21. limits enforced;
22. combinational cycle rejected;
23. stateful feedback allowed;
24. evaluation budget faults runaway graph.

### SPECIFIC

25. same-grid specific device works;
26. delete target -> unavailable/no crash;
27. move card to another ship -> no auto-rebind;
28. cross-grid control impossible;
29. same-map save/load remap works.

### ANY

30. no matching devices -> valid/no-op;
31. 3 matching flash detectors: event from each can trigger same ANY output;
32. ANY actuator input targets exactly one matching Online device;
33. selection deterministic FirstAvailable;
34. removal/power loss reselects another device;
35. transfer card to another ship -> automatically uses matching profile there.

### ALL

36. no matching devices -> valid/no-op;
37. event from any of N matching sensors passes through ALL output stream;
38. one actuator command reaches all N matching Online devices exactly once;
39. offline devices excluded;
40. topology changes update match set without recompile;
41. transfer to another ship -> broadcasts to matching devices there.

### DeviceLink

42. source -> graph;
43. graph -> sink;
44. loop/overload protection remains effective.

### Protocol migration/presets

45. each old default protocol has graph preset equivalent;
46. no normal hardcoded protocol action path remains active;
47. preset loads/edit/writes like normal program;
48. legacy record conversion/import produces equivalent graph;
49. migrated/default Battle Alert works end-to-end;
50. migrated/default Contact works;
51. migrated/default Fire/Decompression works;
52. disabled/absent controller means automation does not happen magically elsewhere.

### Security/UI

53. unauthorized write/rebind rejected;
54. malicious profile/port/node IDs rejected;
55. programmer state only to actual opener;
56. rack UI small/bounded;
57. dirty draft cannot be silently lost.

### End-to-end acceptance

A. `Room Scanner -> Timer -> Speaker` works as physical card in rack.

B. `ANY WeaponFlashDetector -> hostile compare -> ALL Speaker` works with 2+ detectors and 2+ speakers.

C. Move the exact same card to another shuttle with the same device profiles: no reconfiguration, graph works there through ANY/ALL.

D. Add one SPECIFIC device node to that card, move it: only that node becomes unavailable; portable ANY/ALL part continues working.

E. Pull DATA cable from rack: all automation stops. Restore: clean boot and resumes.

## 22. Assets

Пакет содержит два user-supplied 32×32 RGBA sprites:

- `assets/raw/kias_controller_rack.png`
- `assets/raw/kias_programmable_controller.png`

и convenience RSI wrappers.

Используй эти изображения для новых объектов. Не генерируй замену и не меняй арт без технической необходимости. Перед коммитом привести texture path/meta/licensing metadata к conventions текущего Forge repo. Не выдумывать лицензию: это user-supplied project artwork; оформить attribution/licensing так, как требует владелец проекта/REUSE policy.

## 23. License / attribution

Перед фактическим копированием external code проверить конкретные headers/history.

Ориентиры:

- `/tg/station` Wiremod — AGPL v3 reference;
- Goob code — AGPL-3.0-or-later/repo REUSE;
- WizDen SS14 DeviceLinking — MIT upstream;
- Forge/Monolith — текущая REUSE/mixed-history policy проекта.

Создать/обновить `Docs/KIAS/THIRD_PARTY.md`:

`source project | commit | source path | license | conceptual/adapted/copied | attribution`.

Предпочитать clean-room/native C# реализацию по design reference вместо буквального порта DM/React.

## 24. Документация

Обновить реальные repo docs:

- `Docs/KIAS/ARCHITECTURE.md`
- `Docs/KIAS/PLAYER_GUIDE.md`
- `Docs/KIAS/VALIDATION.md`
- `Docs/KIAS/PARITY_AUDIT.md`
- добавить `Docs/KIAS/CONTROLLERS.md`
- добавить/обновить `Docs/KIAS/THIRD_PARTY.md`

Документы должны отражать:

- rack/programmer/card lifecycle;
- port types;
- SPECIFIC/ANY/ALL;
- portability semantics;
- presets;
- protocol migration;
- power cost;
- limits;
- troubleshooting/faults.

## 25. Порядок реализации

Работай до finished state, не останавливайся после анализа.

Phase A — recon current post-patch KIAS + tests + license audit.

Phase B — graph data model + profile/port registry + compiler/validator.

Phase C — physical controller/programmer/rack + dynamic power.

Phase D — server runtime/scheduler + SPECIFIC bindings.

Phase E — ANY/ALL selector resolver/indexes.

Phase F — device adapters + DeviceLink bridge.

Phase G — Robust UI node editor + draft/write workflow.

Phase H — expose current KIAS sensors/actuators as profiles/ports.

Phase I — convert every current/default KIAS protocol into graph presets; deprecate/remove old active rule engine; migration path.

Phase J — perf/stress/security tests, persistence, docs/assets polish.

Compile/test after each coherent phase and fix failures immediately.

## 26. Definition of Done

Задача не завершена, пока одновременно не выполнено:

- physical programmable controller item exists;
- separate programmer exists;
- powered KIAS rack with exactly 8 slots exists;
- provided sprites are used;
- controller consumes rack power indirectly through dynamic load;
- real visual node editor exists;
- strict typed ports exist;
- SPECIFIC device nodes work;
- `ANY <device type>` works and is portable;
- `ALL <device type>` works and is portable;
- ANY sensor output reacts to any matching sensor;
- ALL actuator input broadcasts to all matching devices;
- programs survive save/load/eject;
- moving a card between ships preserves portable selectors and never auto-rebinds specific targets;
- graph runtime is event-driven/server-authoritative/bounded;
- DeviceLink bridge works without defeating loop protection;
- all accepted existing KIAS default protocols are ordinary graph presets/programs, not a second hardcoded policy engine;
- PDC/other heavy subsystem algorithms remain optimized subsystem code and are orchestrated by graphs rather than reimplemented as nodes;
- legacy protocol data has an explicit migration/compatibility story;
- access model/wire hacking/UI split/perf fixes from previous patch remain intact;
- integration tests pass;
- stress test does not show obvious graph/selector tick spikes;
- docs and third-party attribution are updated.

## 27. Финальный отчёт агента

Верни:

1. Preflight/current state summary.
2. Architecture implemented.
3. Physical prototypes/items/machines added.
4. Graph node/port catalog.
5. SPECIFIC/ANY/ALL exact semantics.
6. KIAS device profiles/ports exposed.
7. Protocols migrated and preset list.
8. Legacy migration behavior.
9. DeviceLink changes, если были.
10. Security/access integration.
11. Persistence behavior.
12. Build/test commands and results.
13. Benchmark/perf results incl. worst tick if harness supports it.
14. License/attribution changes.
15. Main changed files.
16. Known limitations only if truly remaining.
