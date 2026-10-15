# Находки независимого ревью KIAS

> Исторический аудит исходного HEAD `5635bd2c`: ссылки на проверявшиеся исходники ведут в неизменяемый `baseline-source/`. Текущие исправления и результаты проверок описаны в [07_FIXES_AND_VALIDATION.md](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/07_FIXES_AND_VALIDATION.md>).

Снимок: `5635bd2c4d1d5ba183700a535f07cfa340cd98ad`. Ниже 12 отдельных замечаний: 1 × P1, 10 × P2, 1 × P3. P0 не обнаружены. VERIFIED означает подтверждение наблюдаемым исполнением либо непосредственно проверяемым структурным фактом; HIGH-CONFIDENCE — замкнутую причинную цепочку в коде без отдельного runtime-воспроизведения. Эти уровни не следует смешивать.

Четыре доказательных теста находятся в [ReviewEvidence.cs](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/ReviewEvidence.cs>). Они намеренно утверждают **наличие текущего дефекта**, поэтому их PASS не означает правильное поведение. Полный штатный KIAS-фильтр дал 167/167 PASS; отдельный evidence-фильтр — 4/4 PASS. Детали и ограничения в отчёте 06.

## KIAS-01 — P1 — тестовый сигнал исполняет настоящую отложенную команду

**Категория:** корректность диагностического режима / побочные эффекты. **Уверенность: VERIFIED.**

**Код:** [KiasServiceSystem.cs:218](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/KiasServiceSystem.cs:218>) (218–237, 263–282); [KiasControllerIoSystem.Commands.cs:23](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/Controllers/KiasControllerIoSystem.Commands.cs:23>); [Runtime.cs:160](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/Controllers/KiasControllerRuntimeSystem.cs:160>).

Сервисный Test на пять секунд выставляет общий `runtime.Testing`, вызывает настоящий `KiasMotion` и затем восстанавливает флаг и legacy cooldowns. Состояние графа, таймеры и причинное происхождение событий не изолированы. `Command` проверяет только текущее значение флага в момент исполнения.

**Сценарий и доказательство:** `TestPulseCanActuateAfterFiveSecondTestExpires` вставляет работающую карточку `scanner Link.out:KiasMotion → Timer(6 s) → DeviceAdapter.On`, использует настоящий `AfterInteractEvent` сервисного инструмента и ждёт 430 тиков. `Testing == false`, но native adapter `State == true`. Проверяется реальное изменение устройства, не счётчик команды. Игрок ожидает диагностику, но получает действие спустя секунду после её завершения. Подобный путь может управлять освещением, реле или защитой; разрушительное последствие в этой проверке не запускалось.

**Минимальная правка:** отделить диагностическое исполнение от живой VM либо пронести диагностическую причину через emissions, очереди, таймеры и обратные сигналы и запрещать соответствующие реальные команды независимо от истечения пяти секунд. Один более длинный таймаут не исправляет контракт. Очистка всех очередей грида по окончании теста потеряет законные игровые события; не рекомендована.

**Зависимости и риск:** Service, GraphMachine, Runtime, IO/DeviceLink bridge. Средний риск: нельзя остановить настоящую автоматику на время теста или позволить diagnostic state влиять на будущий real input. **После патча:** 6-секундный timer, цепочка двух timers, delayed native feedback, повторный Test, shutdown/reconnect во время Test; одновременно настоящее событие должно по-прежнему выполнить разрешённое действие. В новом regression assert adapter должен остаться false.

## KIAS-02 — P2 — потеря DATA не выключает активную оптическую камеру

**Категория:** жизненный цикл / нативная интеграция. **Уверенность: VERIFIED.**

**Код:** [KiasCrewSystem.cs:196](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/KiasCrewSystem.cs:196>) (196–210, 221–247); native [SurveillanceCameraSystem.cs](<D:/Workspace/ss14_lua/corvax/Monolith/Content.Server/SurveillanceCamera/Systems/SurveillanceCameraSystem.cs>).

`RebuildCoverage` обновляет камеры только для `runtime.Online`. Камера исчезнувшего из Online сканера не попадает в этот проход. Массовое выключение в `OnAvailability` происходит лишь при потере всего активного ядра; запрет `OnCameraActivate` препятствует будущему включению, но не отменяет уже активное состояние.

**Сценарий и доказательство:** `DataDisconnectionLeavesOpticalCameraActive` запускает штатный optical scanner, затем переносит/закрепляет его вне DATA, оставляя ядро активным. KIAS считает scanner offline, а `SurveillanceCameraComponent.Active` остаётся true. Это нарушает границу доступности и оставляет нативную камеру активной. Подключение удалённого человеческого viewer отдельно не воспроизводилось; native lifecycle просмотрен.

**Минимальная правка:** перед включением доступных камер согласовать ранее известные KIAS scanners, ставшие offline, через штатный `SetActive(false)`. Удаление optical module также должно выключать камеру. **Зависимости:** Crew, KIAS availability/topology, native camera subscribers. **Риск:** низкий–средний; нельзя выключить стороннюю non-KIAS камеру или удалить общий camera component без проверки владения. **Проверки:** разрыв DATA, power loss только сканера, move/split, снятие optical module, подключённый viewer, reconnect и повторное включение.

## KIAS-03 — P2 — любое перестроение сети стирает выходы неизменившихся устройств

**Категория:** согласованность состояния / графы / диагностика. **Уверенность: VERIFIED.**

**Код:** [KiasControllerIoSystem.cs:142](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/Controllers/KiasControllerIoSystem.cs:142>) (142–158, 162–189); [KiasControllerRuntimeSystem.cs:403](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/Controllers/KiasControllerRuntimeSystem.cs:403>).

`OnTopology → Clear` удаляет `_outputs` всех прежних участников, даже если их доступность, профиль и реальное состояние не изменились. Повторное индексирование не восстанавливает event-only выходы. Результат монитора и начальные значения новой VM теряют правду до следующего изменения устройства.

**Сценарий и доказательство:** `RebuildLosesUnchangedBooleanOutput`: online adapter публикует `DeviceAdapter.State=true`; вызывается `Rebuild` того же неизменённого грида; adapter остаётся online, но `TryOutput` возвращает false. Для периодического датчика следующий poll скрывает ошибку, для состояния, обновляемого только событием, она может сохраняться.

**Минимальная правка:** при топологическом reconcile сохранять валидные несигнальные snapshots неизменившихся online устройств/профилей либо восстанавливать их авторитетным чтением native state. Отдельно удалять snapshots offline/deleted/moved устройств. Не проигрывать старый Signal/Pulse как новое событие.

**Зависимости:** IO index, runtime boot/reindex, output monitor. **Риск:** средний — сохранение snapshot через настоящую недоступность без reread тоже даст stale data. **Проверки:** посторонний кабельный rebuild, новый rack/card, настоящий OFF/ON, потеря профиля, move между grids; old pulse не должен повториться.

## KIAS-04 — P2 — dirty-grid в начале общей очереди задерживает другие корабли

**Категория:** fairness / задержки под нагрузкой. **Уверенность: VERIFIED.**

**Код:** [KiasControllerRuntimeSystem.cs:163](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/Controllers/KiasControllerRuntimeSystem.cs:163>) (163, 182, 193, 208, 306).

Во время ожидания topology `break` останавливает обработку общей очереди timers/events/work/commands по её первому элементу. Это распространяет локальную перестройку одного грида на готовые задания других гридов. Отложить работу изменяющегося грида правильно; останавливать независимые грида не требуется.

**Доказательство:** `DirtyFirstGridBlocksSecondGridEvent` создаёт две отдельные готовые сети, ставит A и B Manual events, invalidates только A и вызывает runtime update. Counter B остаётся 0; после rebuild A становится 1. Тест доказывает head-of-line задержку, **не** бесконечное starvation и **не** причину всего ABBA P99. При лимите перестроений 8 за update и 40 активных кораблях источник задержки существенен для проверки.

**Минимальная правка:** ограниченный round-robin по готовым grids или bounded rotation очереди; сохранить причинный порядок внутри грида, бюджеты и сроки таймеров. Не повышать без доказательств лимиты всей VM.

**Зависимости:** все runtime queues, epoch/topology guard. **Риск:** средний–высокий — reorder внутри грида может поменять семантику. **Проверки:** один непрерывно dirty grid плюс 39 исправных, equal-due timers, feedback, overflow/fault, отмена старого epoch; измерить dispatch/effect latency каждой сети, а не только общее число команд.

## KIAS-05 — P2 — смена Room через локальное окно не обновляет selectors

**Категория:** инвалидация кэша / интерфейс. **Уверенность: HIGH-CONFIDENCE.**

**Код:** [KiasDisplaySystem.LocalUi.cs:212](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/KiasDisplaySystem.LocalUi.cs:212>) (212–241); [KiasControllerIoSystem.cs:123](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/Controllers/KiasControllerIoSystem.cs:123>); [KiasControllerRuntimeSystem.cs:474](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/Controllers/KiasControllerRuntimeSystem.cs:474>).

Room присваивается всем устройствам, но `_kias.Invalidate(grid)` в обработчике выполняется лишь в ветках Speaker/LightController. Runtime хранит `Matches`; Room участвует в `_io.Match`, который обновляется при reindex, а не при каждом событии. Обновление UI/coverage не переиндексирует VM.

**Сценарий:** работающий ANY/ALL по Room=A выбирает scanner/crew/wireless устройство; пользователь переименовывает его помещение в B через локальное окно, не меняя topology. Старый selector сохраняет устройство, новый не получает его до постороннего rebuild. Отдельный end-to-end тест этого UI пути ещё не выполнен; группа через сервисный инструмент — другой обработчик и не опровергает находку.

**Минимальная правка:** сравнить old/new Room и инвалидировать выборки для всех подходящих устройств. Самое малое исправление — существующий topology invalidate только при реальном изменении; узкое selector event можно отложить. **Зависимости:** LocalUi → Runtime/IO. **Риск:** низкий; не инвалидировать сеть на каждом повторном сохранении. **Проверки:** A→B, unchanged Room, ANY/ALL/SPECIFIC, scanner и speaker, карточка без перезапуска, последующее физическое перемещение.

## KIAS-06 — P2 — последний power deficit остаётся после потери Power role

**Категория:** stale cache / тревоги. **Уверенность: HIGH-CONFIDENCE.**

**Код:** [KiasPowerSystem.cs:87](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/KiasPowerSystem.cs:87>) (26, 87–98); [KiasProtocolSystem.cs:84](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/KiasProtocolSystem.cs:84>) (84–87, 123).

При исчезновении последнего Power server `OnTopology` убирает расписание/измерения, но оставляет `_deficits`. `HasDeficit` не проверяет наличие роли. Полный shutdown очищает кэш; потеря только Power role при живом core — нет.

**Сценарий:** зарегистрирован дефицит, затем Power server теряет DATA, остальные KIAS роли и core продолжают работать. Реальная сеть восстанавливается, но новых power samples нет, а Mayday/distress logic продолжает видеть прежний дефицит. **Минимальная правка:** очистить deficit при потере последнего источника/роли и определить различие «неизвестно»/«дефицит». Не отображать отсутствие мониторинга как измеренное благополучие.

**Зависимости:** Power/Protocol/Safety. **Риск:** низкий–средний: сброс не должен маскировать реальный иной повод тревоги. **Проверки:** deficit→role loss, восстановление физического питания без монитора, повторное подключение монитора, несколько Power servers, полный shutdown. Отдельный regression ещё нужен.

## KIAS-07 — P2 — FireClear, полученный offline, оставляет пожар в кэше

**Категория:** lifecycle alarm source / stale distress. **Уверенность: HIGH-CONFIDENCE.**

**Код:** [KiasProtocolSystem.cs:205](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/KiasProtocolSystem.cs:205>) (205–220), [HasDistress:84](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/KiasProtocolSystem.cs:84>).

`OnFireAlarm` возвращается по `HasRole/CanControl` **до** удаления normal source из `_fireAlarms`. Согласования индивидуальных offline/reconnected источников с текущим native alarm state нет. `HasDistress` проверяет лишь существование и grid источника; активное членство и актуальную Danger — нет.

**Сценарий:** online FireAlarm объявляет Danger, затем отключается от DATA/интеграции; fire alarm получает Normal в это время, но KIAS игнорирует его. После reconnect событие Normal может не повториться. Источник остаётся причиной distress. Полный выключатель core очищает набор, поэтому это не утверждение о вечной ошибке после любого restart.

**Минимальная правка:** reconcile источников на изменении доступности/интеграции и reconnect; stale membership удалить, при восстановлении прочитать авторитетное состояние. Политику «связь потеряна» отделить от подтверждённого пожара; не синтезировать ложный FireClear для недоступной комнаты. Аналогично проверить `_atmosAlarms`, но не приписывать ему тот же HasDistress путь без кода.

**Зависимости:** Protocol, Integration, native AtmosAlarmable. **Риск:** средний, зависит от желаемого latched alarm semantics. **Проверки:** Danger→DATA off→Normal→DATA on, FireAlarm move/delete, Atmos role loss, повторный Danger; отдельно assert отображаемое состояние и отсутствие лишнего clear notification.

## KIAS-08 — P2 — лимит палитры делает часть конкретных устройств недоступной

**Категория:** функциональность редактора / масштаб. **Уверенность: HIGH-CONFIDENCE.**

**Код:** [KiasControllerUiSystem.cs:125](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/Controllers/KiasControllerUiSystem.cs:125>) (125–144); [KiasControllerWindow.cs](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Client/_Forge/KIAS/Controllers/KiasControllerWindow.cs>), построение/поиск палитры.

Сервер ограничивает `state.Devices.Count < 256`. Это количество строк **device/profile**, а не уникальных устройств. Для online режима сначала обходятся профили: ранние профили могут занять весь лимит. Клиент ищет только в уже полученном списке, продолжения/серверного поиска нет.

**Сценарий:** корабль с >256 пар device/profile; поздний профиль или конкретное устройство нельзя выбрать в SPECIFIC даже по полному identifier. Устройства с несколькими профилями уменьшают эффективную ёмкость. Лимит 4096 mapping devices не исправляет последующий лимит строк. Runtime может обслуживать устройство; ограничение только пути редактирования. Интерактивный сценарий с 257 строками ещё требует regression.

**Минимальная правка:** ограниченная пагинация/поиск на сервере со стабильным cursor/EntityUid либо ленивое раскрытие устройств по комнате/профилю. Просто убрать все ограничения payload нежелательно. **Зависимости:** BUI shared state/message, server palette, client filter. **Риск:** средний; корректно сохранять selection/revision после topology. **Проверки:** 257+ строк, один device с несколькими profiles, последний identifier, mapping и online, удаление между страницами, прежний SPECIFIC binding.

## KIAS-09 — P2 — поставка заменяет README проекта заданием агенту

**Категория:** поверхность PR / документация. **Уверенность: VERIFIED (diff).**

**Код:** [README.md:1](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/README.md:1>); полный внешний список в [03_EXTERNAL_CHANGES_AUDIT.md](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/03_EXTERNAL_CHANGES_AUDIT.md>).

README Monolith заменён описанием локального lab-пакета с оперативными указаниями агенту. Кроме того, в корне находятся несколько master prompts, manifests, исторические планы и исходный art/reference пакет. Это попадёт в PR всей ветки, даже если последний commit выглядит небольшим. Пользователь/новый contributor теряет описание проекта, а несколько инструкций создают противоречивые пути запуска. Текущий аудит не исполнял старые указания START.

**Минимальная правка:** восстановить project README, дать короткую ссылку на один актуальный Docs/KIAS README/runbook; полезные спецификации и исходники art переместить, task-pack manifests/history исключить из продуктового PR. `Docs/KIAS/PULL_REQUEST.md` также не годится как итоговое описание всей ветки: фраза «Единственное общее изменение» противоречит проверенным 47 native-файлам и относится к старому этапу.

**Зависимости:** только упаковка/документация и пути лабораторной карты при её переносе. **Риск:** низкий, но карта является действующим fixture и не должна просто исчезнуть. **Проверки:** полный merge-base diff, ссылки runbook/launcher/test map, отсутствие потери игровых ресурсов.

## KIAS-10 — P2 — лишние native-зависимости и отсутствующая маркировка интеграций

**Категория:** архитектурная минимальность / сопровождение. **Уверенность: VERIFIED (API и diff).**

**Код:** [GunSystem.cs:206](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/Weapons/Ranged/Systems/GunSystem.cs:206>), [DamageableSystem.cs:168](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Shared/Damage/Systems/DamageableSystem.cs:168>); точные изменённые hunks и решения всех файлов — в отчёте 03 и external.diff.

Для оружия и hull damage добавлены специальные KIAS events рядом с уже существующими AmmoShot/DamageChanged. Native-код приобретает knowledge о подсистеме, хотя локальная фильтрованная подписка достаточна. В 34 native-файлах, где малую интеграцию рекомендуется сохранить/сузить, отсутствуют требуемые KIAS START/END блоки. Независимый engine network fix также включён через gitlink, хотя не нужен KIAS.

**Последствие:** лишние точки конфликта при обновлении общей игры и неочевидные обязанности сторонних разработчиков. Это не заявление о падении сервера во всех этих местах.

**Минимальная правка:** заменить только два доказанно дублирующих моста нативными событиями; минимизировать прямые KIAS policy-зависимости по таблице 03; отдельно поставить independent fixes и engine delta; маркировать действительно необходимые малые hunks. Не удалять полезные public setters, post-damage input и Atmos query ради искусственного нуля внешних строк.

**Зависимости:** Damageable/Destructible event ordering, AmmoShot subscription type, интеграционные API и история ветки. **Риск:** средний. **Проверки:** положительный/нулевой/heal/cancelled урон, уничтожение, настоящий выстрел, обычное оружие/двери/свет/радио без core; native integration regressions после будущего исключения engine delta.

## KIAS-11 — P3 — каждое успешное редактирование повторно собирает BUI state

**Категория:** лишняя работа / интерфейс. **Уверенность: HIGH-CONFIDENCE.**

**Код:** [KiasControllerUiSystem.cs:149](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Content.Server/_Forge/KIAS/Controllers/KiasControllerUiSystem.cs:149>) (149–158, 162–187, 239, 265, 277).

`OnEdit` всегда делает `Refresh` после `Edit`, а успешные ветки и часть ошибочных веток `Edit` уже вызывают `Refresh`. Пока UI открыт, это два построения нового состояния, включая профили, room grouping, Match и compile. Нет основания утверждать две физические сетевые отправки: транспорт может их объединить. Повторная серверная работа видна непосредственно.

**Сценарий:** перемещение/настройка узлов большой схемы в открытом программаторе. **Минимальная правка:** один владелец refresh на UI edit; сохранить требуемый resync при stale revision/ошибке и поведение прямых test/API callers. **Зависимости:** server BUI; внешних изменений не нужно. **Риск:** низкий. **Проверки:** счётчик построений на valid edit/invalid edit/stale revision, правильный draft revision и ошибки, bounded BUI payload. Выигрыш времени пока не измерен.

## KIAS-12 — P2 — загрузчик теряет углы сценария collision_rotated

**Категория:** достоверность benchmark coverage. **Уверенность: VERIFIED (контракт вызова/значение параметра по умолчанию), без нового игрового прогона.**

**Код:** [NativeLab.cs:194](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Tools/KiasBenchmark/Server/NativeLab.cs:194>) (объявление Input на 96 и JSON loader); [world_smoke_scenario.py:78](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Tools/KiasBenchmark/world_smoke_scenario.py:78>); [NativeGameDrivers.cs:374](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/baseline-source/Tools/KiasBenchmark/Server/NativeGameDrivers.cs:374>).

Генератор `collision_rotated` задаёт `fixtureRotationDegrees`; Input имеет соответствующий optional параметр, но единственный вызов `new Input(...)` передаёт только Id/Tick/Ship/Type/Prototype. Поле остаётся null, и `ExerciseCollision` не применяет запрошенный поворот. Сгенерировано 40 запросов поворота, первые — 37°, −0.054°, 111°, −0.048°. Точный constructor и список сохранены в [rotation-input-evidence.json](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/rotation-input-evidence.json>).

**Последствие:** успешный rotated smoke не подтверждает заданные углы; checker может засчитать только тип события/постусловие удара. Это не означает, что grid никогда не вращается от физики, и не опровергает отдельный KiasRoomGridPoseTests, который явно устанавливает rotation. Основной ABBA использует другой сценарий; его времена не отброшены из-за этой ошибки.

**Минимальная правка:** прочитать optional JSON number в шестой аргумент, отвергнуть нечисловое/неfinite значение; в proof сравнивать запрошенную и фактическую исходную ориентацию с корректной нормализацией угла. **Зависимости:** исключительно lab parser, generator, proof checker. **Риск:** низкий. **Проверки:** absent/0/37/−0.054/359.9°, malformed input; краткое реальное столкновение с ненулевым углом и assert actual rotation before impact. Большая ABBA для проверки parser не нужна.

## Результат контрпроверки: не включено в список дефектов

- «Сервисный coverage-кэш не обновляется»: опровергнуто — `BuildLocalState` пересчитывает coverage; путать это с кэшем selector Matches нельзя.
- «Сканер обязан иметь large radar»: его собственный coverage view намеренно Room; это не самостоятельный дефект.
- «KIAS programmer работает без питания в обычной игре»: опровергнуто узким CanMapEdit и тестом frozen/uninitialized/admin mapping. Исключение не следует расширять.
- «Нельзя управлять выключенным интегрированным прибором»: неверно. Control endpoint должен оставаться доступен для ON при выключенной functional power. Relay reconnect также имеет отдельный осмысленный gate.
- «Network SourcePort ломает штатный speaker»: default speaker не имеет DeviceNetwork и использует direct path, где SourcePort передан. Нестандартный сетевой speaker — NEEDS-TEST, а не подтверждённый stock defect.
- «Projectile impact всегда обращается к уже удалённой цели»: обычный destructible использует QueueDel; immediate-delete обработчик отдельно не воспроизведён. NEEDS-TEST для такого жизненного цикла.
- «Tile invalidation точно ошибочна при замене non-empty тайла»: зависит от native airtight/atmos event chain; нужен Plating↔Lattice/Space случай. HYPOTHESIS.
- «RoomCrew scratch доказывает утечку ABBA»: старые ключи scanner могут удерживаться до очистки grid; их величина/retained heap не измерены. Working set сам по себе не доказывает managed leak.
- «Physics нужно исправлять в движке»: ABBA этого не обосновывает; physics вложен в content, геометрия/состав мира различаются.
