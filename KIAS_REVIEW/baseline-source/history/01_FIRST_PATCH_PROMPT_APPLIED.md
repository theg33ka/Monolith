Ты работаешь в репозитории Monolith, ветка `KIAS`.

Задача — провести полноценный patch/pass существующей реализации KIAS. НЕ переписывай систему с нуля и НЕ создавай параллельную вторую архитектуру. Сначала внимательно изучи существующую реализацию `Content.Server/_Forge/KIAS`, `Content.Shared/_Forge/KIAS`, `Content.Client/_Forge/KIAS`, `Resources/Prototypes/_Forge/KIAS`, `Docs/KIAS`, существующие integration tests, а также штатные системы Monolith/SS14, с которыми KIAS уже интегрируется.

Исходной продуктовой спецификацией считай мастер-спеку KIAS 2.0 проекта. В репозитории/контексте проекта также ориентируйся на `KIAS_2.0_консолидированная_таблица.xlsx`, если она доступна. Текущий код не считать автоматически правильнее спецификации: определить, что реализовано полностью, частично, иначе, либо отсутствует.

Главная цель этого прохода: KIAS должна стать законченной игровой системой, которую можно без софтлоков использовать на покупных шаттлах, POI, случайных гридах и полностью построенных игроком кораблях; при этом интерфейсы должны соответствовать конкретному устройству, диагностика должна быть наглядной, а система не должна создавать ощутимых server lagspike или аудио/чат-спама в бою.

## 0. Сначала сделать feature-parity audit

Перед серьёзным кодированием пройти по ВСЕЙ принятой спецификации KIAS 2.0 и составить `Docs/KIAS/PARITY_AUDIT.md`.

Для каждого пункта указать:

`Implemented / Partial / Missing / Deferred / Questionable`

и привести конкретные файлы/компоненты/прототипы, которые его реализуют.

После аудита реализовать все пункты со статусом `Принято` / `Принято с правкой`, которые сейчас Missing или Partial, если ниже не указано иное.

Не реализовывать автоматически пункты из листа/секции `Отложено`.

Пункты со статусом `Под вопросом` только описать в аудите и оставить без реализации, если они не нужны как зависимость текущего патча.

Из двух старых вариантов локальной сенсорики окончательно использовать вариант B: многофункциональный модульный комнатный сканер. Не плодить отдельные коробочки-датчики для функций, которые должны быть его модулями.

Особенно проверить отсутствующие или частичные системы, а не доверять только `Docs/KIAS`:

- отдельная полнофункциональная управляющая консоль KIAS;
- расширенный модульный комнатный сканер и весь набор его модулей;
- физический IFF/transponder receiver;
- docking sensor;
- wireless KIAS transceiver;
- аварийная кнопка;
- key switch / key receiver + физический ключ;
- universal relay/device adapter;
- ручные элементы управления KIAS: существующие кнопки/рычаги плюс предусмотренные flip-flop/rotary варианты;
- внешний свет / навигационные огни;
- интеграция дверей/герм/вентиляции;
- accepted defence features, включая fire-lock/PDC и предусмотренные средства внешней защиты;
- общий resource monitor;
- black box/logger;
- accepted готовые протоколы;
- остальные принятые пункты мастер-спеки.

Не считать функцию реализованной только потому, что в коде существует enum или часть серверной логики. Если ТЗ предполагает физическое устройство, ограничение сенсором или доступное игроку взаимодействие, оно должно реально существовать в прототипах/крафте/UI/gameplay.

Отдельно не перепутать **key receiver / key switch KIAS** с ранее отложенным **navigation beacon receiver**. Навигационный приёмник маяков остаётся отложенным, физический ключевой выключатель KIAS — нет.

Текущий modular room scanner уже существует. Его не дублировать. Проверить и довести.

Сейчас есть 6 слотов и Motion / ID / Transponder / Biometric / Radiation / Spectral. Сверить это с мастер-спекой. В частности, исходная спецификация объединяла ID + crew transponder в один функциональный модуль. Если объединение всё ещё соответствует конечному дизайну, мигрировать аккуратно, сохранив совместимость старых прототипов/карт, либо оставить legacy aliases.

Проверить, что каждый модуль действительно меняет серверное поведение, а не является декоративным предметом.

## 1. Полностью переделать access model KIAS

Текущая проверка через:

`KiasSystem.IsOwner(grid, actor)` → `ShipOwnershipComponent.OwnerUserId`

не является достаточной и создаёт софтлоки.

`ShipOwnershipComponent` используется в том числе для lifetime/cleanup корабля и не должен быть единственным источником ACL KIAS.

Нужен единый `KiasAccessSystem` / resolver либо эквивалентная централизованная архитектура. Никаких разбросанных по системам самостоятельных проверок OwnerUserId.

Все административные операции KIAS должны спрашивать этот resolver:

- изменение настроек;
- редактирование протоколов;
- включение/выключение KIAS;
- PDC/manual overrides;
- регистрация экипажных транспондеров;
- изменение групп/помещений;
- security-sensitive configuration;
- управление ключевым выключателем и т.п.

Приоритет определения политики доступа для ядра/грида:

### Priority 1 — купленный/приписанный игроку шаттл

Если на гриде существует нормальная ownership/deed-модель купленного шаттла, KIAS должна использовать ТОТ ЖЕ принцип авторизации, что и Shuttle Console.

Не просто сравнивать `PlayerSession.UserId`.

Изучить и переиспользовать существующую логику:

- `ShuttleDeedComponent`;
- ID card;
- PDA с ID;
- ship/grid deed;
- `ShuttleConsoleLockSystem`;
- `ShipGridLockComponent`;
- при необходимости `ShipGuestAccessComponent`.

Не копипастить две слегка разные реализации проверки deed. Если возможно, вынести/переиспользовать общий helper/service.

Владелец должен иметь возможность подойти с той же ID/PDA, которой открывается его shuttle console, и получить соответствующий доступ к KIAS.

Guest access шаттла можно учитывать для обычного управления, если это согласуется с существующей shuttle access model, но guest не должен получать право передать ownership KIAS или произвести необратимую security-конфигурацию вместо владельца.

### Priority 2 — компания грида

Если deed/персонального владельца нет, но сам grid имеет существующую `CompanyComponent` / штатно определяемую компанию-владельца, использовать company access.

Не изобретать вторую систему компаний.

Проверить существующие:

- `CompanyComponent`;
- `CompanyAccessReaderComponent`;
- company info на ID;
- текущие access helpers.

Авторизованная ID/PDA соответствующей компании должна давать доступ.

Не считать одну только IFF-окраску доказательством ownership.

### Priority 3 — специальный ACL POI

Если у грида/POI реально существуют особые права доступа, KIAS должна наследовать/использовать их.

Провести аудит `PointOfInterestPrototype.AddComponents`, grid/station components и существующих access/company/faction механизмов.

Не закрывать KIAS только потому, что объект является POI.

Закрытый режим нужен только тогда, когда для этого POI действительно существует авторитетная ограничительная access policy.

Если существующие POI ACL невозможно однозначно разрешить общим кодом, добавить маленький декларативный KIAS access-policy component, который POI может получить через `addComponents`, и привязать его к реально существующим правам конкретного POI.

### Priority 4 — свободное / самосборное ядро

Если нет:

- shuttle deed/owner;
- grid company ownership;
- специальной restrictive POI policy,

то KIAS НЕ ДОЛЖНА стартовать закрытой.

Состояние:

`OpenUnclaimed`

Игрок может пользоваться/настраивать базовую систему и провести валидной ID/PDA по ядру/управляющей консоли, чтобы установить владельца.

После claim система переходит в нормальную owned policy.

Не привязывать ownership к transient `EntityUid` конкретной карточки, если её уничтожение создаст новый софтлок. Использовать стабильную идентичность, уже принятую в проекте для ID/deed/account ownership. Перед выбором конкретного поля изучить существующие модели.

Owner должен иметь нормальный способ перевыпустить/переназначить авторизованный ID без необходимости взламывать собственное ядро.

### Dynamic reevaluation

Access context может измениться уже после появления core.

Корректно обрабатывать:

- ядро построили на существующем купленном корабле;
- корабль получил deed/company после появления ядра;
- ядро перенесли на другой grid;
- grid split;
- map load/save;
- ownership/company changed;
- core reconstruction.

Приоритет всегда должен оставаться:

`Ship deed/owner > Company > special POI ACL > explicit claim > OpenUnclaimed`

Если специфика текущего проекта требует claim перед POI только для полностью свободных систем — придерживаться именно этого принципа: нельзя swipe'ом присвоить чужой protected POI.

## 2. Взлом доступа только штатной wire-механикой

Закрытая KIAS должна поддаваться обычному игровому взлому.

Не делать отдельную кнопку "Hack KIAS", таймер или новую мини-игру.

Использовать существующие:

- `WiresComponent`;
- wire panel;
- `AccessReaderComponent` или совместимую интеграцию;
- `AccessWireAction`;
- стандартные wirecutters/multitool;
- штатное открытие панели отвёрткой, если этого требует стандарт.

Поведение access wire должно быть привычным:

- cut → access restriction bypassed;
- mend → исходная policy снова работает;
- pulse → временный bypass по штатным правилам.

Перерезание access wire НЕ должно менять ownership. Оно только отключает проверку доступа.

После восстановления провода исходная policy снова применяется.

Manual/free core в `OpenUnclaimed` не требует от игрока сначала вскрывать только что построенную им машину.

Добавить integration tests на всю матрицу:

1. Purchased ship + deed ID → доступ.
2. Purchased ship + чужая ID → deny.
3. PDA с deed-ID → доступ.
4. Один только `ShipOwnership.UserId` без нужной ID не является магическим bypass.
5. Company grid + правильная company ID → доступ.
6. Company grid + чужая company → deny.
7. Restricted POI → deny без прав.
8. Restricted POI + cut access wire → доступ.
9. Mend → снова deny.
10. Pulse → временный доступ, затем восстановление.
11. Plain random grid → OpenUnclaimed.
12. Player-built core → OpenUnclaimed.
13. Swipe ID по OpenUnclaimed → claim.
14. Core, построенное на уже купленном shuttle, автоматически выбирает deed policy.
15. Grid move/split/save-load не ломают policy.

## 3. Нормальная отдельная управляющая консоль KIAS

Сейчас `KiasDisplay`, recorder и service tool используют один и тот же большой BUI. Это нужно прекратить.

Сделать физическую **KIAS management console** — основное место полной настройки системы.

Она должна открывать полноценный интерфейс:

- overview;
- alert state;
- subsystem status;
- settings;
- protocols;
- crew/security summary;
- defence;
- power;
- navigation;
- diagnostics/fault summary;
- event log либо ссылку на logger;
- доступные параметры подключённых subsystem servers.

Это единственное обычное устройство, которому нужна большая "полная панель KIAS".

Если master spec предполагает 1×2 common resource monitor, разумно объединить его с dashboard management console либо оставить отдельный read-only resource monitor — выбрать наиболее простой и логичный вариант после сверки ТЗ, но не превращать все настенные дисплеи в admin console.

Полная панель должна быть защищена новым KIAS access resolver.

## 4. Разделить все UI по назначению

Убрать архитектуру "один KiasUiState на всё".

Сделать отдельные UI keys/state DTO/window/control для разных типов устройств.

Минимальное разделение:

**Management console**  
Полная конфигурация KIAS.

**Universal wall display**  
Компактный read-only/настраиваемый display. Показывает только выбранные данные/индикаторы. Никаких семи admin-вкладок.

**Recorder / black box**  
Только журнал, фильтры/состояние регистратора, если нужны.

**Service tool**  
Только service UI: режим, выбранная цель, diagnostics/test/link/group/room. Никакой полной панели KIAS.

**Room scanner**  
Собственный небольшой UI: online/status, установленные модули, range, room, test/coverage при соответствующих правах.

**External sensor**  
Собственный небольшой UI с параметрами датчика и кнопкой/режимом визуализации coverage.

**Crew server**  
Только то, что необходимо для регистрации/просмотра crew/transponders и статуса сервера.

**Speaker/siren**  
Локальные параметры динамика, группа, test sound и т.п., если требуется конфигурация.

Не сериализовать и не отправлять клиенту полный `Devices + Targets + Protocols + Crew + Defence + Power...`, если игрок открыл маленький датчик.

UI-state должен содержать только нужные конкретному окну данные.

Обновлять только реально открытые UI и только когда соответствующие данные изменились. Сохранить хорошую существующую оптимизацию "closed displays are not refreshed".

## 5. Полностью заменить 9×9 coverage mini-map

Существующий `KiasCoverageControl` с фиксированным массивом 81 клетки больше не использовать как основной способ диагностики.

### Малые/local sensors

Для комнатного сканера и похожих локальных устройств сделать client world overlay прямо поверх игрового мира.

При выборе `Coverage` сервисным инструментом:

- сервер проверяет, что цель валидна и пользователь имеет право диагностики;
- клиент получает только минимальное описание геометрии;
- клиент рисует полупрозрачную область действия вокруг настоящего устройства;
- overlay следует за orientation/transform;
- overlay исчезает при смене режима, закрытии UI, потере цели, удалении устройства, выходе из range/дропе tool/дисконнекте.

Не отправлять сотни world tiles каждый тик.

Для простых датчиков геометрия должна описываться параметрами:

- Circle(radius);
- Sector(radius, angle);
- Arc;
- при необходимости небольшим набором локальных cells.

Room scanner radius 7 должен визуально показывать настоящую окружность/покрытые тайлы непосредственно в игровом мире.

Если диагностируется DATA coverage, показывать реальное достижимое покрытие сети, но не превращать это в постоянный серверный flood.

### Большие external sensors

Для дальних датчиков НЕ рисовать гигантский overlay на игровом мире.

Для:

- weapon flash detector;
- PDC radar;
- Horizon;
- proximity sensor;
- других будущих large-range sensors,

открывать специализированное radar/scanner окно.

В проекте уже существует:

`Content.Client/Shuttles/UI/RadarConsoleWindow`
`ShuttleNavControl`

Переиспользовать/расширить существующий radar/nav control вместо создания ещё одной самодельной карты.

На radar view поверх нормальной карты визуально рисовать coverage конкретного датчика.

Примеры:

- weapon flash detector → сектор 90°, ±45° от направления устройства, с реальным configured range;
- PDC radar → круг реального range;
- Horizon → большой круг соответствующей дальности;
- proximity sensor → круг configured range.

Геометрия должна учитывать rotation самого устройства.

Coverage view — диагностическая визуализация, а не дополнительный серверный sensor scan. Она не должна сама искать сущности.

Если radar control уже получает нужные координаты/масштаб, воспользоваться этим. Не дублировать массовую radar telemetry без необходимости.

## 6. Настройки сирен и звуков

Добавить в management settings настройку звуков KIAS.

Не принимать от клиента произвольный resource path.

Сделать серверный allowlist / prototype-backed presets.

Минимально разделить:

- обычный notification tone;
- warning/alarm;
- battle alarm;
- emergency alarm.

Если после аудита архитектуры достаточно меньшего количества каналов — можно объединить близкие, но battle/emergency должны быть отличимы и пользователь должен иметь выбор.

Настройки:

- сохраняются с grid/core;
- переживают map save/load;
- валидируются сервером;
- имеют безопасный preview/test с cooldown;
- работают через существующие KIAS speakers.

Не спамить новым preview sound.

Убрать hardcode:

`redalert.ogg / alert.ogg`

из бизнес-логики как единственный вариант.

## 7. Переделать protocol editor и default protocols

Текущая архитектура уже содержит `KiasProtocolRecord.Enabled`, поэтому не создавать параллельную систему "disabled protocols".

Нужно довести существующую.

Все встроенные/default protocols должны быть видимы в management console и доступны владельцу для:

- enable/disable;
- изменения trigger conditions;
- cooldown;
- disposition;
- thresholds;
- target/group;
- actions;
- message;
- удаления/добавления пользовательских протоколов.

Критично: текущий UI фактически показывает только первое действие protocol record, а остальные actions сохраняет скрыто.

Это плохой UX и потенциальная ловушка.

Сделать полноценное отображение multi-action protocol:

`1 trigger -> N actions`, максимум существующие 8.

Должно быть возможно:

- добавить action;
- удалить конкретный action;
- редактировать каждый;
- видеть их все;
- не потерять невидимое действие при редактировании первого.

Default battle protocol, например, должен логично быть одной записью с несколькими actions, а не кучей неочевидных одинаковых строк, если это не ломает persistence/совместимость.

Обязательно возможность выключить стандартную автоматику полностью без удаления её конфигурации.

Сохранить ограничения:

- max 32 protocols;
- max 8 actions;
- recursion protection;
- validated target grid;
- server-side validation;
- cooldown.

Если полезно, добавить стабильный optional protocol ID для встроенных preset-протоколов, чтобы UI мог показывать понятное имя вроде `Battle alert`, но runtime не должен зависеть от локализованной строки.

## 8. Общая защита от event/audio/chat spam

Провести аудит всех KIAS event sources.

Цель: в интенсивном бою/аварии KIAS информативна, а не орёт каждую десятую секунды.

Существующую агрегацию hull damage сохранить и улучшить, а не удалить.

Нужен общий разумный event coalescing/debounce.

Различать:

1. внутреннее событие для protocol logic;
2. запись/агрегацию в logger;
3. speech/chat announcement;
4. siren/audio.

Повторяющееся событие НЕ обязано повторно проигрывать сирену только потому, что протоколу нужно обновить внутреннее состояние.

Пример для обстрела:

первое подтверждённое попадание → сразу Battle state + одна сирена + сообщение;

следующие десятки попаданий в коротком окне → агрегируются по сектору/типу;

вместо 30 сообщений возможно одно:
`Multiple hull impacts: port-aft ×17`

либо обновлённая агрегированная запись.

Повторная сирена — только после адекватного cooldown либо при escalation состояния.

Не делать глобальный cooldown, который скрывает совершенно другую критическую аварию. Debounce должен учитывать хотя бы alert class/event kind и при необходимости source/sector.

Escalation, например Normal → Battle → Emergency, может обходить suppress и звучать немедленно.

Все cooldown/window значения вынести в адекватно настраиваемые constants/DataFields, а не раскидать магические числа.

Проверить отдельно:

- HullImpact;
- HullDamage;
- Collision;
- WeaponFlash;
- Proximity;
- AtmosDanger;
- Fire;
- PowerDeficit;
- CrewCritical/Dead;
- anomaly;
- MAYDAY;
- DeviceLink-triggered speaker announcements.

## 9. Performance / lagspike audit

Нас интересует не только средний total runtime теста, но и самый тяжёлый server tick.

До серьёзных оптимизаций сохранить baseline существующих KIAS benchmark/integration scenarios.

После патча повторить.

Отдельно проверить high-load сценарии минимум порядка существующих стресс-тестов: до ~200 KIAS-equipped grids/ships там, где тестовая обвязка это позволяет.

### Обратить особое внимание на синхронные периодические сканы

Сейчас несколько систем имеют общий timer и делают всю работу одновременно:

- Crew scan ~1 Hz;
- proximity ~1 Hz;
- power ~5 sec;
- PDC ~10 Hz.

200 устройств со средней нормальной стоимостью, запущенные в одном tick, всё равно дают spike.

Там, где возможно, распределить periodic work по времени/buckets.

Например, не "каждую секунду обработать все 200 grids", а scheduler/bucket/staggered processing с тем же приблизительным update interval каждого устройства.

При этом аварийные event-driven события остаются немедленными.

### Weapon flash detector

Сейчас каждый `KiasWeaponFiredEvent` потенциально вызывает spatial lookup всех `KiasWeaponFlashComponent` рядом с выстрелом.

В крупном бою проверить стоимость сотен выстрелов.

Не делать global enumeration.

Если текущий EntityLookup уже достаточно эффективен — доказать benchmark.

Если нет — использовать индекс активных powered/online detectors по map/spatial cell либо другой existing engine-friendly mechanism.

Ранние дешёвые filters должны происходить до дорогих расчётов.

Не аллоцировать новый большой `HashSet/List/ToArray` в каждом hot event без необходимости.

### Proximity

Сохранить ограниченный spatial grid search и cap contacts, но stagger датчики.

Не делать query всего мира.

### Crew scanner

Существующий индекс tracked entities/transponders и precomputed tile coverage — хороший подход.

Не заменить его на `GetEntitiesInRange` для каждого scanner.

Оптимизировать spike scheduling, allocations и rebuilding, не ухудшая семантику.

### PDC

Сохранить идею общего projectile spatial index, который полностью выключается, когда нет активного PDC.

Не создавать отдельный projectile enumeration на каждый radar.

Проверить allocations:

- rebuild bucket dictionaries;
- `ToArray`;
- threats dictionary;
- sorting;
- interceptor tracking.

Оптимизировать только там, где benchmark показывает смысл.

### UI

Local sensor/service UI не должен заставлять сервер собирать:

- все devices;
- все protocol targets;
- все logs;
- все subsystem strings.

Coalesce частые UI refresh events.

Не обновлять закрытые BUI.

### Topology

DATA topology rebuild должен по-прежнему быть dirty/event-driven, а не per tick.

Не перестраивать сеть от простого rotation, если topology не меняется.

## 10. Доделать физические ограничения сенсоров

Принцип KIAS: никакой магической информации.

Сейчас `KiasNavigationSystem.Classify()` может классифицировать contact по IFF/company/faction программно.

Если master spec требует физический `IFF / transponder receiver`, добавить его как реальное устройство.

Без online receiver KIAS не должна получать полноценную friendly/company/IFF classification просто потому, что нужный component существует где-то на другом grid.

Детектировать сам факт угрозы конкретным sensor можно, но richer classification должна зависеть от соответствующего подключённого оборудования.

То же правило применить к другим подсистемам: если принятая спецификация требует sensor/server, отсутствие этого устройства должно реально ограничивать данные/действия.

## 11. Key switch

Сейчас `KiasMasterKey` используется непосредственно на core.

Реализовать предусмотренный отдельный physical key-operated switch/receiver.

Он предназначен для критического ручного включения/выключения KIAS либо другой предусмотренной master-control функции.

Использовать физический key item.

Не превращать его в ещё одну полную KIAS console.

UI если нужен — минимальный.

Действие:

- авторизованный ключ/пользователь;
- понятное состояние ON/OFF;
- ship-wide KIAS startup/shutdown announcement;
- OFF корректно останавливает TEST, tones, автоматические actions/PDC и periodic work;
- ON выполняет rebuild/revalidation.

Прямое взаимодействие MasterKey→Core можно оставить как legacy/service fallback, если оно оправдано, но основной gameplay должен соответствовать physical key switch из спецификации.

## 12. Отдельно проверить принятые, но визуально отсутствующие устройства

После parity audit не просто перечислить Missing, а реализовать принятые Missing-фичи.

При этом максимально переиспользовать штатные SS14/Monolith системы.

Примеры принципа:

- аварийная кнопка → вариант существующего signal button, а не новая самописная input system;
- door/shutter control → DeviceLink/existing ports;
- radar → существующий ShuttleNavControl;
- access hacking → standard wires;
- power → existing power nets;
- speakers → existing local chat/audio;
- company → existing company components;
- shuttle ownership → existing deed/access logic.

KIAS должна ощущаться расширением игры, а не отдельным движком внутри SS14.

## 13. Persistence / compatibility

Все пользовательские настройки должны сохраняться вместе с grid/map:

- access policy/claim;
- audio presets;
- protocols + enabled state + all actions;
- scanner settings;
- groups;
- rooms;
- speaker configuration;
- relay configuration;
- registered crew/transponders, где это уже предусмотрено;
- остальная accepted configuration.

Runtime caches, scheduling buckets, audio handles, spatial indices и т.п. НЕ сериализовать — пересоздавать.

Если меняются существующие components/prototypes, подумать о совместимости уже сохранённых shuttle maps.

Не ломать существующие KIAS map entities без причины.

Если старые IDs нужно заменить, оставить parent/alias/migration-compatible prototype там, где это разумно.

## 14. Security

Любое BUI сообщение считать недоверенным.

На server side перед каждым изменением повторно проверить:

- actor;
- interaction/reach при необходимости;
- grid;
- device still exists;
- device still belongs to same grid;
- online/power status;
- KIAS access;
- target is legal;
- enum/prototype IDs valid;
- numeric bounds;
- max string lengths.

Клиент не может выбрать arbitrary audio resource path или arbitrary entity вне собственного grid.

Cut access wire — единственный предусмотренный security bypass в данном патче и он должен быть явным игровым состоянием, а не дырой в проверке.

## 15. Тесты

Не ограничиваться compile.

Расширить существующие `Content.IntegrationTests/Tests/_Forge/KIAS`.

Обязательные категории:

**Access**
полная матрица из раздела 2.

**Protocols**
default protocol editable, disable/enable, multi-action editing, persistence, malicious invalid messages.

**Audio/spam**
100+ одинаковых hull impact/weapon events за короткое время не создают 100 сирен/100 одинаковых announcements; escalation не теряется.

**Scanner**
все module combinations, overlapping coverage, ID/transponder semantics, insert/remove module at runtime, no double-count.

**Coverage**
правильная circle/sector geometry, rotation, range, cleanup overlay lifecycle. Серверная часть не запускает дополнительный world scan ради картинки.

**Sensor prerequisites**
без physical IFF receiver нет магической classification; с receiver появляется.

**UI isolation**
открытие service tool не требует/не формирует full management state; recorder не получает protocol target list; sensor UI получает только sensor data.

**Persistence**
save/load configuration and access/audio/protocols.

**Lifecycle**
core OFF/ON, power loss, DATA break, core deletion, duplicate core, grid split/removal.

**Performance**
повторить текущие benchmarks и добавить burst/high-load сценарии.

Для perf-report сохранить:

- total elapsed;
- allocations;
- max/near-max individual update duration, если test harness позволяет;
- before/after comparison.

Не утверждать "performance fixed", если тест измеряет только суммарное время и не показывает spike.

## 16. Definition of Done

Патч не закончен, пока одновременно не выполняется следующее:

- покупной shuttle автоматически использует его настоящий deed/ID access;
- company grid использует company access;
- protected POI остаётся protected, но штатно взламывается access wire;
- свободный/manual/random grid никогда не получает бессмысленный locked core;
- первое свободное ядро можно штатно claim через ID;
- существует отдельная management console;
- service tool/sensors/recorder больше не открывают full KIAS;
- 9×9 mini-map больше не является основной coverage diagnostics;
- local coverage видно прямо поверх мира;
- large sensor coverage видно поверх нормального radar/nav view;
- сирены выбираются в настройках и сохраняются;
- default protocols можно редактировать и выключать;
- multi-action protocol UI не скрывает actions;
- heavy combat не превращается в аудио/чат spam;
- accepted master-spec features прошли parity audit и все Missing/Partial либо реализованы, либо в `PARITY_AUDIT.md` явно объяснено, почему пункт является Deferred/Questionable, а не тихо забыт;
- новые hot paths не сканируют весь мир;
- periodic systems не создают очевидный synchronized 1s/5s spike;
- integration tests проходят;
- актуализированы `Docs/KIAS/ARCHITECTURE.md`, `PLAYER_GUIDE.md`, `VALIDATION.md`, `PARITY_AUDIT.md`.

## 17. Порядок работы

Работай по фазам, но доведи задачу до рабочего состояния, а не останавливайся после анализа.

Phase A — audit + baseline.

Phase B — centralized access resolver + standard wire hacking.

Phase C — UI split + management console.

Phase D — coverage overlays/radar visualization.

Phase E — audio settings + protocol editor + anti-spam.

Phase F — feature-parity completion принятых пунктов.

Phase G — performance pass/time-slicing/indexing.

Phase H — integration tests, build, docs, final validation.

После Phase A можно скорректировать внутренний технический план, если реальный код показывает более правильный способ интеграции, но не менять пользовательскую семантику требований.

В конце выдать мне итоговый отчёт:

1. Что было уже реализовано и поэтому не дублировалось.
2. Что было Partial и что исправлено.
3. Что реально отсутствовало и добавлено.
4. Что осталось Deferred/Questionable по мастер-спеке.
5. Какие архитектурные изменения сделаны.
6. Какие performance hotspots найдены.
7. Benchmark before/after.
8. Какие integration tests добавлены и их результат.
9. Какие известные ограничения остались.
10. Список основных изменённых файлов.

Не маскировать отсутствие функции документацией. Проверять gameplay path от prototype → component → system → UI → interaction → test.