# MASTER PROMPT — KIAS correctness, integration and UI/UX pass

Работай в **актуальной ветке `KIAS`** Monolith. Базовая точка, по которой составлена эта спецификация: `3e067d069ebb45b192db06db8a38354f9f053cca` (2026-10-08). Перед началом всё равно проверь текущий HEAD; если ветка ушла вперёд, используй фактический код как источник истины и не откатывай более новые исправления.

## 0. Что уже существует

KIAS уже реализована: ядро, DATA topology, access model, устройства, integration kit, programmable controller cards, programmer, controller rack, graph runtime, ANY/ALL/SPECIFIC, presets, тесты и документация. **Не переписывай систему с нуля.**

Этот проход исправляет обнаруженные в реальной игровой проверке проблемы:

- имя записанной controller-card не отражается на физическом предмете;
- speaker graph path неудобен/частично ломается, особенно broadcast через ALL;
- inspector показывает нерелевантные поля для каждого узла;
- graph terminology и типы непонятны игроку;
- порты почти не документированы;
- значения constants/config не читаются прямо на нодах;
- `ALL Освещение` сейчас фактически означает light-group controllers, а не все светильники;
- интегрированное устройство можно выключить через KIAS, но после этого KIAS считает его offline и не может включить обратно;
- group-light workflow через service multitool непрозрачен;
- штатный DeviceList/NetworkConfigurator workflow для air alarm / sensors / vents / scrubbers должен продолжать работать как раньше и не заменяться ручной прокладкой source/sink ports;
- preset `atmosphere`/«Разгерметизация» записывается и запускается, но в реальном сценарии не получает ожидаемый `AtmosDanger` из-за сломанной/неочевидной upstream-конфигурации;
- KIAS UI функционален, но выглядит как debug/tool UI, плохо читается и не соответствует качеству игровых консолей Monolith;
- русская локализация неполная, в названиях встречается кириллическое «КИАС» вместо требуемого латинского `KIAS`, у сущностей нет нормальных `.desc`.

## 1. Обязательный preflight

До изменения кода:

- `git status`, branch, HEAD, diff;
- прочитать этот пакет документации;
- открыть фактические файлы из `06_REPO_SOURCE_MAP.md`;
- прогнать baseline KIAS integration tests;
- вручную воспроизвести по возможности проблемы: speaker, integrated light OFF→ON, group lighting, air alarm DeviceList, depressurization preset, card metadata;
- проверить `.github/PULL_REQUEST_TEMPLATE.md` и релевантные Monolith devwiki/contribution правила;
- не разносить KIAS-специфичные решения по глобальным системам, если можно исправить внутри KIAS/существующего generic API;
- если приходится менять общий `DeviceLink`/`NetworkConfigurator`, изменение должно быть минимальным, обоснованным и покрытым регрессиями обычного gameplay вне KIAS.

## 2. Физическое имя controller-card

После успешного `WRITE` физический controller item должен отображать пользовательское имя программы.

Требования:

- draft rename **не** переименовывает предмет до `WRITE`;
- успешный `WRITE` атомарно обновляет program + metadata item name;
- описание предмета должно по-человечески объяснять, что это интегральная/программируемая схема KIAS;
- после eject/reinsert/map persistence имя не расходится с `Program.Name`;
- использовать штатный `MetaDataSystem.SetEntityName/SetEntityDescription`, не хранить отдельный UI-only label;
- дефолтная пустая карточка имеет понятное локализованное имя.

## 3. Speaker

Профиль `Speaker` имеет data-input `Message` и impulse-input `Announce`/`Alarm`. Поле `node.Config.Text` внешнего Speaker не должно притворяться сообщением.

Должны работать:

`String constant -> Speaker.Message` + `OnStart/Signal -> Speaker.Announce`.

Для `ALL Speaker` одно событие обязано прозвучать из **всех выбранных динамиков**, а не только из первого из-за общего emission/cooldown key. Не отключай anti-spam целиком: keying должен различать адресованный speaker/target при broadcast либо broadcast должен выполняться корректно единым механизмом.

Добавить end-to-end test controller runtime -> real KIAS speaker -> local IC chat path.

## 4. Graph inspector и ноды

Текущий inspector не должен безусловно показывать `Room + Group + Text + Number + Seconds + Enum + Bool + Comparison` для каждого узла.

Сделать capability-driven inspector:

- StringConstant: только строка;
- NumberConstant: только число;
- BoolConstant: только Да/Нет;
- EnumConstant: typed dropdown, не сырой integer;
- Timer/Cooldown/Clock: только релевантное время и state;
- Counter: initial number;
- Latch/Toggle: initial state;
- comparison nodes: оператор сравнения;
- purely functional nodes (`AND`, `OR`, `NOT`, `IF`, `Edge`, `OnStart`...) не показывают бессмысленные config fields;
- external ANY/ALL: только применимые selector filters и readable device/profile info;
- SPECIFIC не должен получать бессмысленный selector filter, способный случайно исключить уже выбранное устройство.

### Наглядность прямо на нодах

Нода должна показывать важное configured value без выбора inspector:

- `Строка` -> текст, с безопасным truncation;
- `Число` -> число;
- `Да/Нет` -> состояние;
- `Таймер` -> `5 с`;
- `Cooldown` -> `10 с`;
- compare -> `>=`, `=`, `!=` и т.д.;
- Enum -> локализованное значение;
- ANY/ALL filters -> компактно `помещение: ...`, `группа: ...` только если они реально заданы.

Не допускать overlap с портами и заголовком на zoom 1.0.

## 5. Типы и терминология

В пользовательском UI:

- `Signal` отображать как **«Импульс»** и объяснять: одноразовое событие без true/false;
- `Bool` -> **«Да/Нет»** или «Логическое состояние»;
- `String` -> «Строка»;
- `Number` -> «Число»;
- `Entity` -> «Объект»;
- enum должен иметь domain и человекочитаемые варианты.

Не вводить неявное соединение Bool<->Signal. Вместо молчаливого отказа показать понятную причину и подсказать `Детектор фронта` или state-node.

Названия логики:

- И (AND), ИЛИ (OR), XOR — исключающее ИЛИ;
- НЕ (NOT), NAND — И-НЕ, NOR — ИЛИ-НЕ;
- XNOR — совпадение/эквивалентность с `XNOR` в названии;
- `Edge` -> «Детектор фронта»;
- `Latch` -> «RS-защёлка» если semantics действительно соответствуют Set/Reset.

## 6. Порты и help

У каждого graph-visible port должны быть:

- локализованное имя RU/EN;
- тип;
- direction;
- **уникальное краткое описание смысла**;
- при необходимости допустимые значения/domain.

Запрещено оставлять почти всем портам generic `Типизированный порт контроллера KIAS.`.

Inspector показывает порты как карточки/строки вида:

`Вход · Да/Нет — Установить`<br>
`Да включает выбранный свет, Нет выключает.`

На canvas hover по порту должен показывать tooltip с тем же help. Не выводить пользователю сырые `Input Set: Bool`.

Служебные `$MatchedCount/$OnlineCount/$HasAny/$Source` тоже документировать.

## 7. Lighting: два разных профиля

Обязательно разделить:

### `ALL Освещение`

Это **все доступные KIAS светильники на текущем grid**, включая обычные светильники, получившие integration kit / иной поддерживаемый KIAS control endpoint. Это не список light-group controllers.

Минимальные команды: `Set : Bool`, `On : Signal`, `Off : Signal`.

### `ALL Контроллеры групп освещения`

Это физические `KiasLightController` и только они. Контроллер воздействует на светильники своей группы.

Не делай profile resolver гигантским prototype switch. Если текущий `kiasControllerProfile.component` недостаточен для capability profile «светильник», добавь минимальную чистую capability/marker abstraction.

## 8. KIAS control-plane и выключенные integrated devices

Сейчас статус/`IsOnline()` завязан на `_power.IsPowered(device)`. Для integrated target, который KIAS сам выключил через `SetPowerDisabled`, это делает обратный `ON` невозможным.

Разделить:

- **functional/device power** — работает ли сам потребитель;
- **KIAS control-plane availability** — доступен ли его integration endpoint по живой KIAS DATA topology.

Пока KIAS core активен, DATA path существует, target корректно integrated/anchored и integration endpoint жив — команды управления должны оставаться доступны даже если рабочее питание target было отключено KIAS.

Не распространять это правило на обычные нативные KIAS servers/devices, которым реальное питание необходимо для работы.

Тест: integrated lamp ON -> KIAS OFF command -> lamp depowered/off -> endpoint остаётся адресуем -> KIAS ON -> lamp снова powered/on.

## 9. Group-light service multitool

Не использовать скрытое общее поле `Message` как имя группы в UI.

Для режима групп:

- отдельное понятное поле `Группа`;
- показать текущую группу выбранной лампы/контроллера;
- назначение лампе и controller одним и тем же именем группы;
- popup/feedback после успешного назначения;
- группа не обязана быть `EMERGENCY`; пользователь может задать произвольное допустимое имя;
- ясно различать настройку группы speaker и группы lighting там, где это нужно.

## 10. Не ломать обычный NetworkConfigurator / DeviceList

Graph ports — дополнительный путь автоматизации, **не замена** штатному NetworkConfigurator.

Air alarm использует `DeviceListComponent` для списка sensors/vents/scrubbers и DeviceNetwork traffic. Игрок должен иметь возможность обычным multitool workflow собрать устройства и записать список в air alarm как раньше.

Обязательно воспроизвести и починить сценарий, где сейчас пользователь попадает только в port-link workflow.

Проверить особенно `NetworkConfiguratorSystem.DetermineMode/OnUsed`, наличие одновременно `DeviceListComponent` и `DeviceLinkSource/Sink`, default LinkMode, KIAS-added ports/components. Не делай глобальный фикс наугад: сначала тест, который демонстрирует проблему.

Регрессии:

- list mode: сохранить air sensor + vent + scrubber в multitool, применить к AirAlarm, список реально обновился;
- link mode: обычные source/sink links продолжают работать;
- KIAS graph generic DeviceLink bridge продолжает работать;
- non-KIAS network configurator gameplay не ломается.

## 11. Depressurization preset

Preset `atmosphere`/«Разгерметизация» уже существует, загружается, WRITE проходит. Его не надо заменять отдельным hardcoded detector.

Проверять end-to-end цепь:

`AirSensor -> штатная atmos network/list -> AirAlarm danger -> AtmosAlarmEvent -> KIAS AtmosDanger -> controller preset -> actions`.

Нужен regression test с настоящей опасной атмосферой или максимально близким штатным event path. Отдельно `AtmosClear` после восстановления.

Если сам preset логически неправильный — исправить. Но не маскировать upstream regression генерацией KIAS события напрямую в тесте.

## 12. UI visual pass — обязательный

Цель — не копия shuttle console, а **тот же уровень визуальной иерархии и читаемости**.

Референс в репо: `Content.Client/Shuttles/UI/ShuttleConsoleWindow.xaml` и связанные controls. Пользовательский screenshot лежит в `references/ui_shuttle_reference.png`.

Для KIAS:

- тёмные вложенные панели с чёткими рамками;
- секционные заголовки;
- нормальные отступы 4–8 px, а не сплошной столбец LineEdit;
- функциональные toolbar/button groups;
- readable status cards;
- зелёный/жёлтый/красный только для состояния/акцента, не как декоративный шум;
- никаких горизонтальных scrollbars в inspector;
- inspector ориентир ~300–340 px при стандартном окне, но адаптивный;
- длинные названия wrap/ellipsis + tooltip;
- palette/search/categories читаемы;
- canvas остаётся главным пространством;
- rack, programmer, management/local service windows получают единый KIAS visual language;
- UI не должен быть завязан на магические пиксели, которые ломаются на min size/UI scale.

## 13. Локализация

Для всех реально игровых/spawnable/printable KIAS prototypes:

- русское `name` и `.desc` через locale;
- `KIAS` **оставить латиницей**;
- описания 1–2 коротких игровых предложения;
- board/item names тоже локализовать;
- не переводить technical IDs/profile IDs;
- не оставлять сырой английский UI или enum integer там, где игрок это видит.

## 14. Проверка интерфейса — не факультативна

Расширить текущий `KiasControllerLayoutTests`. Минимум:

- размеры 850x500, 1200x720, 1600x900;
- empty + populated graph + very long labels;
- no negative/non-finite sizes;
- inspector не создаёт H-scroll;
- поля inspector соответствуют node capability;
- port rows имеют localized name/type/description;
- constant values отображаются на canvas;
- несовместимый wire даёт понятную обратную связь;
- palette различает `Освещение` и `Контроллеры групп освещения`;
- длинные device/profile names не ломают layout;
- RU locale не показывает raw enum/`Input ...: Bool`/непереведённые KIAS сущности.

И **обязательный live UI smoke**: запустить локальный клиент/сервер обычным способом проекта и открыть programmer/rack/service/management UI. Сохранить screenshots/log notes локально (например `.kias/ui-smoke/`, не обязательно коммитить). Проверить минимум standard и min window sizes. Не писать в финальном отчёте «UI проверен», если была только `Measure/Arrange` проверка без живого окна.

## 15. Acceptance scenarios

Перед завершением вручную/автотестами пройти:

1. Rename -> WRITE -> eject: item называется пользовательским именем.
2. `String("Тест") + OnStart -> SPECIFIC Speaker`: динамик произносит текст.
3. То же через `ALL Speaker`: говорят все выбранные speakers без подавления первого/остальных.
4. ALL Освещение выключает и включает integrated lamps напрямую.
5. ALL Контроллеры групп освещения управляет группами через KiasLightController.
6. Integrated lamp после OFF остаётся KIAS-controllable и включается обратно.
7. Service multitool назначает группу лампе и controller понятным UI.
8. Обычный multitool list-mode связывает air sensor/vent/scrubber с AirAlarm.
9. Реальная разгерметизация переводит AirAlarm в Danger и запускает `atmosphere` controller preset.
10. Восстановление атмосферы даёт clear path.
11. Все graph ports имеют meaningful descriptions.
12. UI smoke/screenshots показывают отсутствие клиппинга/горизонтального скролла и понятную визуальную иерархию.

## 16. Final report

В конце дай:

- root cause по каждому пользовательскому багу;
- список изменённых файлов;
- какие общие системы были затронуты и почему это было неизбежно;
- команды тестов и точный pass count;
- что проверено live UI, со списком открытых окон/разрешений;
- оставшиеся ограничения;
- `git diff --check`;
- не объявляй успех по непроверенному пункту.
