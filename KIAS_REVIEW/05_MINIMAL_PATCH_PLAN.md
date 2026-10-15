# Минимальный план будущих исправлений

> Исторический аудит исходного HEAD `5635bd2c`: ссылки на проверявшиеся исходники ведут в неизменяемый `baseline-source/`. Текущие исправления и результаты проверок описаны в [07_FIXES_AND_VALIDATION.md](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/07_FIXES_AND_VALIDATION.md>).

Это план по результатам read-only аудита, **не применённый патч**. Все production-файлы, HEAD и submodule сохранены. Не требуется переписывать KIAS или добавлять новый общий framework. Исправления поведения, очистку поставки и измерительные эксперименты следует разделить, чтобы изменение результата можно было объяснить.

## 1. Сначала изолировать диагностический Test — KIAS-01

Места: `KiasServiceSystem.cs:218–237,263–282`, `Controllers/KiasControllerRuntimeSystem.cs` queues/timers и `KiasControllerIoSystem.Commands.cs:23`.

Минимальная приемлемая семантика: диагностическое событие никогда не создаёт настоящую небезопасную команду, даже через 600-секундный Timer, после reconnect и после завершения пятисекундного окна. Настоящие события параллельно не теряются.

Предпочтительный вариант нужно выбрать после небольшого прототипа: отдельное диагностическое исполнение на копии volatile graph state и безопасные output sinks. Альтернатива — causal diagnostic token во всех очередях, таймерах и feedback плюс запрет на изменение живого state, который затем потребит реальный input. Просто добавить bool к `Command` недостаточно: diagnostic pulse может изменить latch/counter и позже повлиять на реальную команду. Не предлагать blanket reset всех VM по окончании теста: это уничтожит игровые таймеры.

Порядок: перенести evidence scenario в regression с обратным ожидаемым состоянием; воспроизвести RED на текущей версии; реализовать выбранную изоляцию внутри KIAS; проверить delayed trigger и одновременный real trigger. DeviceLink test-policy остаётся в силе до эквивалентной замены, иначе обход снова откроется.

## 2. Небольшие исправления lifecycle и состояния — KIAS-02,03,05,06,07

| Изменение | Конкретное место | Минимальный объём | Обязательная проверка |
|---|---|---|---|
| Выключать native optical camera при individual offline | Crew.RebuildCoverage перед циклом Online | Пройти прежние/текущие KIAS scanners, выключить недоступные штатным SetActive; затем включить доступные | DATA/power/module off, viewer разъединяется, reconnect работает |
| Не стирать корректные outputs на no-op topology | IO.Clear и OnTopology | Развести уничтожение grid и reconcile membership; сохранить только допустимые state outputs | Unchanged Boolean остаётся, offline/moved/deleted исчезает, Pulse не replay |
| Invalidates после изменения Room | LocalUi.OnDeviceSettings | Сравнить нормализованное старое/новое значение; invalidate только при изменении | ANY A теряет, ANY B получает, SPECIFIC сохраняет, неизменённое поле не вызывает rebuild |
| Очищать deficit при исчезновении Power role | Power.OnTopology else | `_deficits.Remove(args.Grid)` вместе с `_samples.Remove`; проверить политику unknown | Живой core, снят только Power server, stale distress не остаётся |
| Reconcile native alarm sources | Protocol.OnTopology/availability/integration change | Убрать stale membership и прочитать актуальное состояние при reconnect; не вызывать clear для неизвестного состояния | Danger→offline→Normal→online и повторный Danger без фантомного Mayday |

Для первых четырёх действий новая общая абстракция не нужна. Alarm reconciliation нельзя свести к безусловной очистке всех тревог на любом rebuild: это создаст ложное благополучие и пропуски.

## 3. Fairness без изменения причинного порядка — KIAS-04

Места: Runtime.Update, timer/event/work/command loops и QueueWork. Сначала двухгридовый regression на доказанную задержку, затем 40-grid case. Разделить ready/pending работу по гриду либо обойти не более исходной длины очереди с переносом временно заблокированного грида. В пределах грида сохранить FIFO, epoch, root cause и feedback budget. Для timers учитывать due time, не исполнять будущий timer ради обхода dirty head. Лимиты операций не повышать.

Acceptance: healthy grid продолжает работу при непрерывном изменении другого; affected grid не выполняет команды по stale binding; после commit все законные события достигают своих получателей один раз; CPU/tick не превышает введённый бюджет. Mean speedup не является обязательным условием исправления fairness.

## 4. Палитра и UI — KIAS-08,11

Пагинация/серверный search для device/profile rows с bounded payload и стабильными EntityUid; желательно сохранение прежнего SPECIFIC binding даже если он вне текущей страницы. Не заменять поиск по ID перебором произвольных net entities из клиента. Путь mapping использует тот же catalogue paging, сохраняя `CanMapEdit` как строгий server gate.

Refresh отделить малым патчем: одно построение state на accepted edit, обязательный resync на stale revision/invalid message. Не менять вместе с этим layout, графовую семантику или сериализацию program version.

## 5. Сузить внешнюю поверхность — KIAS-09,10

Последовательность:

1. Подключить KiasHullStructure к штатному DamageChangedEvent и отфильтровать positive delta, сохранив порядок до DestructibleSystem. Удалить специальную вставку из DamageableSystem только после RED/GREEN на фактический урон/уничтожение.
2. Наблюдать AmmoShotEvent внутри KIAS. Использовать подходящую глобальную фильтрованную подписку, поскольку directed SpaceArtillery pair уже занят. Удалить дополнительный KIAS callback из GunSystem.
3. Сохранить нейтральные native APIs: CancelShots, SetEnabled, HasDeedAccess/IsAllowed, InjectionChanged, old/new anomaly signal, bounded sealed-room query. Для DeviceLink, accepted impact, projectile post-damage, autopilot completion оставить минимальный hook с данными; переносить policy в KIAS.
4. CableNode: существующий gate необходим. Самый малый безопасный первый шаг — ограничить и маркировать текущий hook, сохранив поведение. Замена на нейтральный `ConnectionsEnabled`/attempt-event — отдельный небольшой API patch после проверки node rebuild. Не создавать универсальную middleware-систему ради одного кабеля.
5. FireControl: neutral reservation/pre-fire context вместо `kiasAutomatic`, но gate должен стоять до aim/fire. IFF: neutral label extension с Dirty. Эти два изменения можно отложить относительно прямых correctness fixes, если ревьюер принимает явно маркированный узкий текущий hook.
6. NetworkConfigurator: восстановить прежнее поведение обычных устройств, оставить выбор List/Link в необходимом KIAS случае либо в отдельном общем исправлении с обоснованным UX. Не прятать глобальный behavior change под маленький diff.
7. Отдельно решить доставку 12 INDEPENDENT_UPSTREAM_FIX файлов/ссылки движка. Не делать слепой массовый revert родительских Station AI/radio изменений. Перестроить будущую поставку относительно согласованной базы только по отдельной задаче, затем проверить весь diff.
8. Восстановить README Monolith, объединить актуальные KIAS инструкции в Docs/KIAS, перенести fixture/art sources, исключить task-pack manifests/history. Корневые BAT можно сохранить как пользовательский интерфейс запуска. Удаление рабочего lab fixture вместо переноса — не минимальный исправляющий патч.

## 6. Конкретные предлагаемые #Forge-change блоки

Ниже иллюстрации точных границ **существующих сохраняемых вставок**, а не скомпилированный patch. Остальные строки метода остаются прежними. Каждый независимый участок получает свою пару; нельзя оборачивать весь GunSystem/DeviceLinkSystem целиком. Для общей независимой правки использовать собственное название, не KIAS.

### ShuttleNavControl: два коротких участка

```csharp
// #Forge-change START (KIAS): штатный радар передаёт матрицу рисования наложению наследника.
DrawSensorCoverage(handle, worldToView, xform.MapID);
// #Forge-change END (KIAS)
```

```csharp
// #Forge-change START (KIAS): наследник рисует покрытие без копии штатного радара.
protected virtual void DrawSensorCoverage(DrawingHandleScreen handle, Matrix3x2 worldToView, MapId mapId) { }
// #Forge-change END (KIAS)
```

### AME: поле события и публикация после изменения

```csharp
// #Forge-change START (KIAS): адаптеру нужен сигнал фактической смены впрыска AME.
public event Action<EntityUid, bool>? InjectionChanged;
// #Forge-change END (KIAS)
```

После существующего `controller.Injecting = value;`:

```csharp
// #Forge-change START (KIAS): публикуем подтверждённое состояние без опроса AME.
InjectionChanged?.Invoke(uid, value);
// #Forge-change END (KIAS)
```

### CableNode: минимальный необходимый gate до отдельного neutral API

```csharp
// #Forge-change START (KIAS): штатный CableNode не имеет отключения связности для реле.
public override bool Connectable(IEntityManager entMan, TransformComponent? xform = null)
{
    return base.Connectable(entMan, xform)
        && !entMan.System<Content.Server._Forge.KIAS.KiasRelaySystem>().IsCableBlocked(Owner);
}
// #Forge-change END (KIAS)
```

Такой блок объясняет необходимость gate, но сам по себе не снимает замечание о прямой зависимости. Дальнейший neutral API должен гарантировать group rebuild при переключении канала/реле; возврат true по старому cache недопустим.

### SharedGunSystem: сохранить нативный setter

```csharp
// #Forge-change START (KIAS): отменяем очередь через владельца gun state и штатный Dirty.
public void CancelShots(EntityUid gunUid, EntityCoordinates? restoreAim = null)
{
    if (TryComp<AutoShootGunComponent>(gunUid, out var auto))
        auto.RemainingTime = TimeSpan.Zero;
    if (TryComp<GunComponent>(gunUid, out var gun))
    {
        gun.ShootCoordinates = restoreAim;
        Dirty(gunUid, gun);
    }
}
// #Forge-change END (KIAS)
```

### Atmos room safety

Новый метод `AtmosphereSystem.RoomSafety.cs:8–29` целиком является добавленной интеграцией; блок вокруг **этого метода**, без wrapping всего класса:

```csharp
// #Forge-change START (KIAS): sealed-room query читает закрытые для внешних систем atmos tiles.
// Здесь остаётся существующий IsSealedSafeRoom с ограничением BFS и InvalidatedCoords.
// #Forge-change END (KIAS)
```

Строка-заполнитель в этом примере — указатель для автора будущего patch, не предлагаемый production-комментарий. Метод не заменять заглушкой. Параметр limit сохранить bounded на стороне caller; не снимать Access/RA0002 глобально ради переноса кода.

### YAML: узкие добавления

В соответствующих recipe lists lathe.yml, раздельно:

```yaml
    # #Forge-change START (KIAS): штатный производитель включает набор инструментов KIAS.
    - KiasTools
    # #Forge-change END (KIAS)
```

```yaml
    # #Forge-change START (KIAS): штатный производитель включает платы KIAS.
    - KiasBoards
    # #Forge-change END (KIAS)
```

Аналогично, с исходным отступом компонентов:

```yaml
  # #Forge-change START (KIAS): базовая конструкция получает marker для нативного damage event.
  - type: KiasHullStructure
  # #Forge-change END (KIAS)
```

Для cable tag — причина «реле выбирает штатный кабель без глобального добавления компонентов при startup»; для PDC/decoy — «штатное орудие предоставляет совместимую возможность KIAS». Не маркировать огромный базовый прототип. Сохранять прежние non-KIAS компоненты/recipes в точности.

### Остальные обязательные границы

| Файлы | Что обрамить | Краткая причина блока |
|---|---|---|
| Client/Server csproj | Каждый условный ItemGroup или один будущий Import | Lab-файл включается только явным build property; XML-комментарий, не // |
| AtmosAlarmableSystem | Новое поле/параметр Source и два changed construction sites | KIAS различает реальные источники тревоги без полного scan |
| DeviceLink events/System | Только новый event/InvokeSink и invocation sites | Нативный порт публикует факт сигнала и использует штатную проверку sink |
| PowerMonitoringConsole/SharedPower | Data enum value и три skip-граничных участка | DATA не является четвёртым силовым каналом |
| JammerSystem | SetEnabled и замена тела OnActivate вызовом setter | Команда использует штатную батарею/компоненты jammer |
| Company/ShipAccess/ConsoleLock | Общие query methods и заменённые calls | Единое нативное правило доступа без копии в KIAS |
| Anomaly event/System | PreviousStability и его заполнение | Наблюдаем переход порога, а не опрашиваем все аномалии |
| Projectile/Shuttle impact/Autopilot | Минимальная публикация подтверждённого события | Нужен postcondition, ранний/отменённый input не эквивалентен |
| IFF | Только label modification/helper Dirty | KIAS статус должен попасть в штатную сетевую подпись |
| FireControl client/shared/server | Только reservation field/render/population/pre-fire gate | Согласование manual/automatic control до side effect |

После замены GunSystem/Damageable на существующие события KIAS-блоки там не остаются вообще. Комментарий не является оправданием ненужного кода.

## 7. Исправить достоверность rotated smoke — KIAS-12

NativeLab JSON loader должен передавать шестой параметр Input. Генератор уже записывает нужное число. Добавить независимый round-trip assertion generator→реальный C# parser→driver angle, а не только проверку наличия строки `fixtureRotationDegrees` в Python output. Native proof должен сверять requested/actual angle; malformed значения должны завершать run явной ошибкой. Короткий smoke с ненулевым углом достаточен для исправления; новые многочасовые ABBA не нужны.

## 8. Оптимизации и то, что можно отложить

После перечисленных исправлений: один UI Refresh; reuse результатов CanControl внутри безопасного неизменяющего прохода; измерить no-op reconcile, grid/person scans, room geometry allocations. Новые долгоживущие кэши вводить только с полным списком invalidation events и негативными тестами. Подробная таблица стоимости/риска/критериев — в отчёте 04.

Можно отложить: объединение небольших adapter-систем ради внешнего вида, новый generic framework событий, переделку всех UI widgets, оптимизацию холодной компиляции без профиля, engine Physics изменения. Существующие small adapters, enum domains, native profile snapshots, bounded queues и frozen-map gate решают реальные задачи; их не нужно вычищать как «AI-код» по стилю.

## Gate будущего исправляющего PR

Проверить correctness regressions каждого изменённого контракта; затем полный актуальный KIAS-фильтр, relevant vanilla integration tests, compiler/resource checks. Показать diff от актуального merge-base и свежий submodule SHA. Старые evidence tests должны быть превращены в regression правильного поведения, а не оставлены зелёными assertions дефекта. Ручной smoke должен включать UI права/владение, карту split/FTL, оптического viewer, многопрофильную палитру и Test с timer. Только после этого отдельно оценивать performance-кандидат.
