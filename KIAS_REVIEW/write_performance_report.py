import json
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'KIAS_REVIEW'
d=json.loads((OUT/'ABBA_RECOMPUTED.json').read_text(encoding='utf-8'))
f=json.loads((OUT/'zip-pair-source-fingerprint.json').read_text(encoding='utf-8'))
b=json.loads((OUT/'zip-pair-BASELINE_MANIFEST.json').read_text(encoding='utf-8'))
report=f'''# Независимый анализ производительности KIAS

Новых длинных прогонов не выполнялось. Пересчитаны сырые tick rows четырёх готовых прогонов из `{d['archive']}`. SHA256 архива: `{d['archiveSha256']}`. Скрипт [recompute_abba.py](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/recompute_abba.py>) и машиночитаемый [ABBA_RECOMPUTED.json](<D:/Workspace/ss14_lua/corvax/Monolith/KIAS_REVIEW/ABBA_RECOMPUTED.json>) сохранены рядом с отчётом. Старый текстовый разбор использован как перечень проверяемых утверждений, а не как источник измерений.

## Какая сборка была измерена

Архивный source fingerprint указывает HEAD `{f['head']}`, но также непустой working-tree patch `{f['workingTreePatchSha256']}`. Все **{len(f['files'])}** файлов fingerprint совпали с текущим checkout байт-в-байт; список mismatches пуст. Для 37 tracked файлов из archived matched-current-fixes.patch проверены postimage blob hashes относительно `5635bd2c4d1d5ba183700a535f07cfa340cd98ad`; все совпали. Подробности — benchmark-source-provenance.json. Поэтому вывод «это только старый 00a6, отношения к актуальной реализации нет» был бы неверен.

Это всё же не идентичность всех deployment bytes: fingerprint охватывает выбранные исходники, prepare_pair добавляет инструментирование, отдельные XAML/FTL/art не входят в файловый список fingerprint. Сопоставлены реальные архивные patch и build manifest, а не только строка branchHead. Правки после измерения не объявлены измеренным ускорением.

A строился от baseline `{b['base']}` с общими исправлениями AudioEffectSystem, ShipMoveToOperator, ChatSanitization и одинаковым исправлением impact dedup; обе стороны используют engine c2fc1375. A **не равен чистому актуальному upstream/main**. B добавляет KIAS и согласованный рабочий patch поверх matched base. prepare_pair также инжектирует измерительные hooks, а лаборатория создаёт сценарные сущности. Оценка относится к парному лабораторному миру, а не к произвольному серверному раунду.

40 кораблей, 60 TPS, 4 настоящих подключённых клиента; каждый run — 10800 warmup + 108000 measured ticks; всего 432000 measured. Метки `performanceEligible=true`, `strictReady=false`, `fullGameplayCoverage=false`, `NATIVE_FULLGAME_ABBA_COMPLETE` совместимы: серия завершена и годится для оговорённого performance сравнения, но полной игровой готовности не удостоверяет.

## Пересчитанные tick times

Все числа — миллисекунды. Pooled означает квантиль объединённых 216000 тиков каждой стороны; это **не среднее двух run-квантилей**.

| Выборка | Mean | P50 | P95 | P99 | P99.9 | Max | Тиков >16.67 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
'''
for key,run in d['runs'].items():
    q=run['whole_tick_ms'];report+=f"| {key} | {q['mean']:.4f} | {q['p50']:.4f} | {q['p95']:.4f} | {q['p99']:.4f} | {q['p999']:.4f} | {q['max']:.4f} | {q['over1667']} / {q['n']} |\n"
for key,run in d['pooled'].items():
    q=run['whole_tick_ms'];report+=f"| Pooled {key} | {q['mean']:.4f} | {q['p50']:.4f} | {q['p95']:.4f} | {q['p99']:.4f} | {q['p999']:.4f} | {q['max']:.4f} | {q['over1667']} / {q['n']} ({100*q['over1667']/q['n']:.3f}%) |\n"
report+='''
Mean +2.1732 ms, median +0.5312 ms, P99 +37.1806 ms. Старые приблизительные P99 19.29/56.45 получаются при усреднении отдельных run quantiles; pooled даёт 19.1915/56.3722. Это разница метода агрегации, а не обнаруженная подмена CSV. B1/B2 весьма близки по mean; это повышает доверие к повторяемости данного сценария, но два B run не устанавливают переносимость на другие карты/нагрузки. Нельзя считать 432000 тиков независимыми статистическими экспериментами.

Максимальные тики в несколько секунд присутствуют и в A. Например, A1 max driver ≈5181 ms близок к max whole ≈5188 ms. Поэтому объяснять весь крайний tail одной внутренней системой KIAS неверно. Перцентили компонент не складываются. Без парного per-tick профиля нельзя распределить P99 whole между методами вычитанием их P99.

## Что входит в дополнительные 2.17 ms

| Mean-компонента | A | B | B−A | Как читать |
|---|---:|---:|---:|---|
'''
for key,label,note in [('content_engine_ms','Content + engine','Включает Physics и работу KIAS/native мира'),('physics_ms','Physics','ВЛОЖЕНО в content; не прибавлять второй раз'),('driver_ms','Scenario driver','Стоимость создания/ведения нагрузочного сценария'),('observer_ms','Observer/telemetry','Стоимость наблюдения; зависит от объёма KIAS данных')]:
    a=d['pooled']['A'][key]['mean'];bb=d['pooled']['B'][key]['mean'];report+=f'| {label} | {a:.6f} | {bb:.6f} | {bb-a:+.6f} | {note} |\n'
report+='''
Остаток mean delta около +0.0226 ms после content/driver/observer — описательное согласование wall-time учёта. Content +1.3151 ms — более подходящая граница для игрового дополнительного труда, но и это не «чистая VM»: меняются количество устройств, их native updates и физические взаимодействия. Observer +0.5573 ms и driver +0.2782 ms нельзя скрывать в цене KIAS runtime. В A-карте удалены 382 KIAS/вложенные сущности при сохранении 2112 native entities; отсутствие KIAS не означает идентичную физическую работу.

Средние allocation/tick: A 266168.02 bytes, B 319698.24 bytes, Δ +53530.22 bytes (~20.1%). Это whole-process allocation в измеряемом интервале, а не только KIAS heap. GC pause: A1 899.615 ms, A2 802.900 ms, B1 1696.188 ms, B2 1506.215 ms. Паузы уже влияют на wall times; их нельзя дополнительно прибавлять к tick cost.

Pooled median working set: A 4077996032 bytes, B 4833660928 bytes; прирост mean working set ~831680823 bytes. GB здесь десятичные, не GiB. Working set включает native allocations, buffers, GC segments и историю драйвера. Утечка удерживаемых managed объектов без heap/dominator анализа не доказана.

## Очереди и задержка настоящей команды

В финале обоих B нет faulted машин, очереди пусты. В проверенном B snapshot 1040 работающих машин, 40 активных grids, 9920 online из 9960 устройств. Это утверждение о snapshot, не доказательство непрерывной доступности каждого устройства на протяжении серии.

| Метрика B1 | Enqueued/Consumed | High water | P95 tick | P99 tick | Max tick |
|---|---:|---:|---:|---:|---:|
'''
for q in d['runs']['2-B']['queueLatency']:
    # Keys deliberately obtained from the stored JSON schema rather than assumed headings.
    p95=q['p95Ticks'];p99=q['p99Ticks'];mx=q['MaxAgeTicks']
    report+=f"| {q['name']} | {q['Enqueued']}/{q['Consumed']} | {q['HighWater']} | {p95} | {p99} | {mx} |\n"
report+='''
Queue latency включает отменённую/инвалидированную работу, поэтому не равна end-to-end задержке эффекта. Отдельная метрика acceptedCommandLatency:

| Run | Count | P50 ticks | P95 ticks | P99 ticks | Max ticks |
|---|---:|---:|---:|---:|---:|
'''
for name in ['2-B','3-B']:
    q=d['runs'][name]['acceptedCommandLatency'];report+=f"| {name} | {q['count']} | {q['p50Ticks']} | {q['p95Ticks']} | {q['p99Ticks']} | {q['maxTicks']} |\n"
report+='''
При номинальных 60 TPS это P99 ≈1.15–1.17 s и max ≈1.23–1.28 s в единицах игровых ticks. Реальный wall time при длинных тиках может быть больше. Метрика заканчивается на принятом IO dispatch: событие `CommandDispatched` возникает до выполнения actuator ветки. Успешная нативная реакция проверяется отдельно scenario drivers; нельзя назвать этот histogram физическим временем выключения лампы/выстрела/пожаротушения.

Подтверждённый KIAS-04 показывает один механизм общего backpressure: pending topology на первом элементе останавливает другие grids. Он заслуживает исправления независимо от среднего расхода CPU. Но доля ABBA latency, вызванная именно им, не измерена. Поднимать budgets до исчезновения очереди без tick-cost контроля опасно.

ПКО: 181/240 перехватов в каждом B (~75.4%). В драйвере `perfectInterceptionRequired=false`, контракт требует хотя бы одного реального попадания в каждой hostile volley. Это PASS ограниченного контракта, не 100% перехват и не полная боевая сертификация.

## Счётчики фаз: правильная граница

Фазы final telemetry накоплены за warmup + measured + граничный update: CrewUpdate/RuntimeUpdate Calls = **118801**, а не 108000. Ниже exclusive totals B1 делятся на 118801 только для ориентировочного среднего на update. Это не точная per-tick delta A→B и не измерение ускорения.

| Фаза B1 | Calls | Exclusive total ms | ms/118801 | Exclusive allocation bytes |
|---|---:|---:|---:|---:|
'''
for name in ['IntegrationControl','IntegrationReconcile','IoCommand','CrewRegistration','CrewPeople','CrewScan','RoomGeometry','RuntimeTimers']:
    q=next(p['metrics'] for p in d['runs']['2-B']['phases'] if p['name']==name)
    report+=f"| {name} | {q['Calls']} | {q['exclusiveMilliseconds']:.4f} | {q['exclusiveMilliseconds']/118801:.6f} | {q['exclusiveAllocatedBytes']} |\n"
report+='''
IntegrationReconcile inclusive ≈46866 ms, exclusive ≈22963 ms: сложить inclusive с IntegrationControl означало бы двойной учёт. 90.18 млн вызовов IntegrationControl показывают многократность проверок, а не 90 млн сетевых rebuild. IoCommand exclusive включает синхронный нативный эффект, если он не выделен вложенной фазой; это не только цена switch/lookup.

RuntimeTimers **0 calls** в этом финальном сценарии — существенная граница покрытия. Нельзя ссылаться на ABBA как на проверку delayed Test/Timer ошибки KIAS-01. Сценарий не удостоверяет все произвольные пользовательские графы.

NativeProfiler/инструментирование exclusive-cost в текущих tools умеет более узкий профиль с пределом длительности 7200 ticks; старые manifests имеют `profiling=false`. Наличие нового инструмента не создаёт задним числом per-tick полного профиля старого ABBA. Cost attribution script явно отделяет exclusive/inclusive и incomplete evidence; его вывод следует читать с этой границей.

## Конкретные локальные кандидаты

**Ни одному предложению ниже не приписывается уже измеренное ускорение.** Сначала correctness fixes KIAS-01–07; затем короткие парные проверки кандидата на неизменном мире.

| Кандидат и места | Доказательство / вероятная польза | Реализация и риск | Инвалидация / проверка | Чужой код |
|---|---|---|---|---|
| Один Refresh на edit — ControllerUiSystem | Два непосредственных вызова; один из двух сборов BUI можно убрать | Малая стоимость, низкий риск; выигрыш только при редактировании | stale revision, invalid edit и прямой Edit caller обязаны получать state; измерить allocations/edits | Нет |
| Меньше повторных CanControl в Integration/Crew/IO | 90.18 млн phase calls; из одного прохода повторяется проверка online/grid/endpoint | Сначала локальная reuse результата внутри неизменённого прохода, не глобальный вечный cache; низкая–средняя стоимость | Reconcile сам может добавлять/удалять компоненты; не использовать сохранённый true после мутации. Delete, move, DATA/power off→on | Нет |
| Отдельно измерить/сократить reconcile-проход | IntegrationReconcile около 0.193 ms exclusive/update в B1 и вызовы CanControl внутри | Средняя стоимость; изменить частоту без событий — риск пропуска перемещения/детей | Сначала счётчики scanned/changed и no-op rebuild; тест actual integrated lamp OFF→ON и transient components; bounded periodic audit можно оставить | Нет |
| Гридовый индекс людей/crew с общим frame snapshot | CrewPeople 8.567 млн calls; scans обходят людей для каждого грида, часть registration повторяется | Потенциально меньше G×P; средняя стоимость и риск stale identity/transform | Move/FTL/split, mind/body changes, экипаж/PDA/серийный номер, чужой grid; сравнить 1/10/40 grids с фиксированным P | Нет |
| Сократить повторное построение геометрии по revision | RoomGeometry allocation 362 MB cumulative B1; связь room revision уже существует | Средняя стоимость; кешировать только при явном tuple grid/roomRevision/scanner settings | Tiles, двери, rotation, modules, range, move/split, external map atmosphere. Не кэшировать просто по EntityUid | Нет |
| Не терять state snapshot на no-op topology | KIAS-03 доказан; меньше повторной boot/re-emission работы возможно | Correctness patch первым; скорость не обещана | Одноразовые signals не replay, реальные unavailable snapshots не сохранять | Нет |
| Grid fairness при pending topology | KIAS-04 доказан, B dispatch P99 69–70 ticks | Средняя–высокая стоимость; прежде всего latency, mean может не уменьшиться | Per-grid ordering, equal due timers, 39 healthy +1 dirty, CPU budget и feedback | Нет |
| Убрать повторные dictionary/LINQ/native schema allocations только по narrow profile | Startup/reindex строит списки профилей, enum prototypes, native SHA256 schema | Умеренная выгода предположительна; reusable prototype metadata допустимо | Prototype reload, изменённые DeviceLink ports, profile removal; не использовать static EntityUid caches | Нет |
| Удалять obsolete RoomCrew scratch keys | Словарь очищает sets, не все исчезнувшие scanner keys до удаления grid | Небольшая bounded hygiene для длительного churn; не объясняет ABBA working set | 1000 create/delete scanner cycles, retained key count после cleanup; live mappings не потерять | Нет |

Не рекомендуются: отключение native Physics/коллайдеров, массовый Unanchor, удаление безопасности native actuator, безусловное увеличение VM budgets, глобальный CanControl cache без epoch, отказ от periodic проверки native state без надёжных событий. Это либо меняет игровой эксперимент, либо покупает mean ценой недоступности/некорректности.

## Следующие короткие измерения, если исправления будут отдельно разрешены

1. Один candidate patch, одинаковые source fingerprint/engine/build flags/world seed; записать stable map hash и состав сущностей. Прогнать correctness gate до performance.
2. 1/10/40 grids: idle, локальная перестройка одного грида, одновременная перестройка, обычная нагрузка; измерить exclusive timings, allocations, queue delay и **native effect completion**. Длительность короткого профиля не выдавать за замену длинной ABBA.
3. Для IntegrationControl — calls per scanned device и число state changes; для Crew — число examined people на grid; для geometry — cache hit/miss и причина invalidation; для UI — число state builds на одно accepted edit.
4. Для хвостов сохранять per-tick correlated driver/observer/content/physics/GC/queue markers. Не складывать percentile разных фаз. Отдельно проверить instrumentation on/off, потому что observer заметен.
5. Для памяти — fixed-duration churn и retained heap после одинакового GC/idle окна, отдельно от process working set. Инструментирование не является production feature.

Старое ABBA показывает реальный дополнительный расход и существенный tail в указанной лаборатории. Оно не устанавливает engine defect, full gameplay readiness и количественную пользу ещё не выполненных оптимизаций.
'''
(OUT/'04_PERFORMANCE_REVIEW.md').write_text(report,encoding='utf-8')
print('04_PERFORMANCE_REVIEW.md written')
