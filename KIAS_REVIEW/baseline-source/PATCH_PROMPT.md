# KIAS PATCH PROMPT — correctness + UI/UX + regressions

Работай в текущей локальной ветке `KIAS` Monolith. Спецификация подготовлена по HEAD `3e067d069ebb45b192db06db8a38354f9f053cca`, но сначала проверь фактический HEAD/status/diff. **Не переписывай KIAS с нуля. Не ограничивайся планом: реализуй, протестируй и доведи до рабочего состояния.**

Сначала прочитай `00_AGENT_MASTER_PROMPT.md`, `01_REPO_FINDINGS.md`, `02_TARGET_ARCHITECTURE.md`, `03_DEVICE_PROTOCOL_SPEC.md`, `05_PERFORMANCE_AND_TESTS.md`, `06_REPO_SOURCE_MAP.md`, `07_CONTROLLER_GRAPH_SPEC.md`, `08_PROTOCOL_MIGRATION.md` и `Docs/KIAS/VALIDATION.md`.

Главные задачи:

1. **Controller-card metadata.** После успешного `WRITE` физический предмет должен называться пользовательским `Program.Name`; описание — нормальное локализованное описание программируемой интегральной схемы KIAS. Draft rename до WRITE предмет не меняет. Eject/reinsert/save-load не рассинхронизируют имя.

2. **Speaker graph.** Рабочий путь: String -> `Message`, Signal/Импульс -> `Announce`/`Alarm`. Убрать ложное впечатление, что generic node `Config.Text` у Speaker является сообщением. Исправить ALL Speaker так, чтобы одно событие доходило до всех выбранных динамиков; anti-spam/cooldown сохранить, но не подавлять соседние targets общим key. Добавить end-to-end test до реального local speech/chat path.

3. **Inspector.** Убрать универсальный набор room/group/text/number/seconds/enum/bool/comparison у каждого node. Показывать только параметры, которые реально используются этим node kind/profile. Room/group у selectors назвать фильтрами и показывать только когда применимо. SPECIFIC не должен получать бессмысленные selector filters.

4. **Наглядность нод.** String/Number/Bool/Timer/Cooldown/Compare/Enum и selector filters показывают важное значение прямо на canvas. Не должно быть overlap/clip.

5. **Types/terminology.** В UI `Signal` = «Импульс», Bool = «Да/Нет». Строгую типизацию сохранить. При несовместимом wire не молчать: показать понятную причину/подсказку. Логику назвать общепринято: AND/OR/XOR/NOT/NAND/NOR/XNOR, Edge = «Детектор фронта», Latch = понятное инженерное название. Enum больше не редактируется сырым int: используй domain/localized dropdown; не соединяй концептуально разные enum domains бездумно.

6. **Port help.** Каждый graph-visible port получает уникальное краткое RU/EN описание. Убрать generic `Типизированный порт...` почти везде. Inspector показывает friendly direction/type/name/description; canvas hover — tooltip. Документировать и `$MatchedCount/$OnlineCount/$HasAny/$Source`. `Key/EventKey` объяснить как ID события для dedup/cooldown, а не «ключ доступа».

7. **Разделить освещение.** `ALL Освещение` = все напрямую доступные KIAS светильники на grid, включая compatible lights с integration kit. Отдельный профиль/пункт `ALL Контроллеры групп освещения` = только `KiasLightController`. Не решай это giant prototype switch; используй минимальную capability/marker abstraction если текущего profile resolver недостаточно.

8. **OFF -> ON integrated device.** Сейчас KIAS выключает target через `SetPowerDisabled`, после чего `_power.IsPowered` делает его `NoPower`/offline и обратный ON недоступен. Раздели target functional power и KIAS control-plane availability для integrated endpoints. Пока core/DATA path/integration endpoint живы, команды управления доступны даже при выключенном target. Native KIAS machines всё ещё требуют реального питания. Нужен regression test integrated lamp OFF -> remains addressable -> ON.

9. **Light groups UX.** Service multitool Group mode не должен скрыто использовать generic `Message`. Сделай отдельное явное поле группы, показ current group, feedback при назначении. Игрок назначает одно имя группе ламп и `KiasLightController`.

10. **Вернуть обычный DeviceList workflow.** KIAS graph/DeviceLink не заменяет штатный NetworkConfigurator. AirAlarm должен обычным мультитулом в list mode получать список AirSensor/GasVent/GasScrubber как раньше. Сейчас пользователь фактически может идти только через ручные ports. Воспроизведи проблему тестом; особенно проверь entity с одновременно `DeviceListComponent` и DeviceLink ports, `NetworkConfiguratorSystem.DetermineMode/OnUsed`, default LinkMode и KIAS-added components. Исправь минимально. Link mode и non-KIAS workflows не ломать.

11. **Preset «Разгерметизация».** `atmosphere` preset существует, загружается/WRITE проходит. Не добавляй hardcoded костыль. Проверить реальную цепь AirSensor -> DeviceList/atmos network -> AirAlarm Danger -> normal `AtmosAlarmEvent` -> KIAS `AtmosDanger` -> controller preset -> actions; затем AtmosClear. Тест, который напрямую вызывает KIAS trigger, недостаточен.

12. **Полный UI visual pass.** Текущий UI выглядит как debug form. Приведи programmer/rack/service/local/management KIAS окна к игровому качеству. В качестве качества/иерархии изучи `Content.Client/Shuttles/UI/ShuttleConsoleWindow.xaml` и screenshot `references/ui_shuttle_reference.png`: вложенные панели/рамки, секции, кнопочные группы, читабельные статусы, нормальные отступы. Не копируй shuttle UI буквально. Canvas остаётся главным пространством. Inspector ориентир 300–340 px где позволяет окно, **без horizontal scrollbar**. Long labels — wrap/ellipsis + tooltip.

13. **RU localization.** Все player-facing KIAS prototypes получают нормальные русские name + `.desc`; `KIAS` всегда латиницей. Никаких сырых английских entity names/raw enum ints в обычном RU UI.

### UI validation — особый приоритет

Текущий `KiasControllerLayoutTests` недостаточен: он в основном доказывает, что layout имеет finite size.

Расширь проверки на 850x500, 1200x720, 1600x900; empty/populated graphs, длинные RU labels и каждый тип inspector. Автоматически проверь отсутствие H-scroll в inspector, корректную видимость capability fields, inline node values, port descriptions/tooltips, различие Lighting/LightGroupController, нормальный mismatch feedback.

**Кроме тестов обязателен live UI smoke.** Запусти локальный клиент/сервер штатным способом репо, открой programmer, rack, service multitool, light controller/local UI и management console. Проверь минимум standard + small size; сохрани screenshots/log notes в ignored `.kias/ui-smoke/` (или аналогично). Не утверждай «UI проверен», если видел только `Measure/Arrange`.

### Обязательные gameplay smokes

- Rename -> WRITE -> item name;
- String + OnStart -> SPECIFIC Speaker;
- ALL Speaker из 2+ динамиков;
- ALL Lighting direct fixture ON/OFF;
- ALL LightGroupController group ON/OFF;
- integrated light OFF -> ON;
- service tool group assignment;
- standard multitool DeviceList -> AirAlarm with sensor/vent/scrubber;
- real depressurization -> AirAlarm panic/Danger -> `atmosphere` preset;
- recovery -> clear.

Не делай broad global refactor ради этой задачи. Если трогаешь общие DeviceLink/NetworkConfigurator/Atmos/Power systems, обязательно объясни необходимость и добавь non-KIAS regression tests.

В конце: root cause каждого бага, changed files, test commands/pass counts, live UI smoke details, screenshots location, `git diff --check`, оставшиеся ограничения. Не скрывай непроверенные пункты.
