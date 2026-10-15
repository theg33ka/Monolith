import csv
import json
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'KIAS_REVIEW'
rows = list(csv.DictReader((OUT/'inventory.csv').open(encoding='utf-8-sig')))
external = {r['file']: r for r in json.loads((OUT/'external-decisions.json').read_text(encoding='utf8'))['files']}
tests = json.loads((OUT/'test-contracts.json').read_text(encoding='utf8'))
test_index = {r['file']: r for r in tests}

purposes = {
'KiasControllerBoundUserInterface.cs':'BUI transport, открытие/закрытие editor и обработка state',
'KiasControllerLabels.cs':'Русские подписи типов, профилей и enum domains',
'KiasControllerWindow.cs':'Палитра, поиск, inspector, pending/revision и WRITE/Eject',
'KiasGraphCanvas.cs':'Координаты graph/screen, zoom/pan, hit testing, typed wires',
'KiasBoundUserInterface.cs':'Основной монитор: state → window, сообщения на сервер',
'KiasCoverageNavControl.cs':'Отрисовка room coverage, boundary cells и grid pose',
'KiasCoverageSystem.cs':'World overlay и lifecycle UI geometry',
'KiasLocalBoundUserInterface.cs':'Локальное BUI устройства, открытие/закрытие',
'KiasLocalWindow.cs':'Настройки устройств, поля, enum/range, просмотр coverage',
'KiasUi.cs':'Общие клиентские элементы/представление KIAS',
'KiasWindow.xaml':'Разметка основного монитора',
'KiasWindow.xaml.cs':'Основной монитор, списки и действия пользователя',
'KiasCommandInputs.cs':'Набор допустимых command inputs',
'KiasControllerIoSystem.Commands.cs':'Schema, online/grid/Test gates → native command',
'KiasControllerIoSystem.Sensors.cs':'Sensor outputs и snapshot текущих значений',
'KiasControllerIoSystem.cs':'Outputs, topology index, matches/selectors',
'KiasControllerPhysicalSystem.cs':'Card slots, rack power, lease и узкий CanMapEdit',
'KiasControllerRuntimeSystem.Compilations.cs':'Compile cache, revisions и faults',
'KiasControllerRuntimeSystem.cs':'VM lifecycle, epoch, timers, bounded queues и dispatch',
'KiasControllerUiSystem.cs':'Server authority, draft, palette, revision, edit validation',
'KiasRuntimeQueueMetrics.cs':'Queue age/high-water/histogram counters',
'KiasAccessSystem.cs':'Ship deed/company/POI/claim и права конфигурации',
'KiasActuatorSystem.cs':'Native lighting, suppressant cartridge/foam и DeviceLink',
'KiasAmeAdapterSystem.cs':'AME injection → native controls/state',
'KiasCountermeasureSystem.cs':'Native jammer adapter',
'KiasCrewSystem.cs':'Registration, crew/threat/fauna, scan coverage, optical cameras',
'KiasDefenceSystem.cs':'PDC/guns/IFF, ammo, target selection и reservation/gates',
'KiasDeviceIdentitySystem.cs':'Сохраняемый ID, collision resolution, room grouping',
'KiasDeviceSystem.cs':'Интеракции, схемы/модули, wire setup и local controls',
'KiasDisplaySystem.Audio.cs':'Speech/radio, channel и routing',
'KiasDisplaySystem.LocalUi.cs':'Local config validation, room/range/settings и state',
'KiasDisplaySystem.State.cs':'Сборка monitor snapshot',
'KiasDisplaySystem.cs':'BUI refresh, диагностика, coverage и отображаемые состояния',
'KiasEmissionGate.cs':'Cooldown/dedup выходящих сигналов',
'KiasExternalSensorSystem.cs':'External threats, radiation/anomaly и фильтры',
'KiasGridComponent.cs':'Grid-local registry/state ownership',
'KiasHullSystem.cs':'Damage/healing, collision impact и пороги',
'KiasIntegrationSystem.cs':'Direct/scanner handoff, availability/control vs functional power',
'KiasNavigationSystem.cs':'Shuttle native navigation/FTL → signals',
'KiasPeriodicScheduler.cs':'Ограниченная периодическая обработка grids',
'KiasPhaseMeasurement.cs':'Nested inclusive/exclusive time/allocation counters',
'KiasPowerSystem.cs':'Energy samples/deficit и role lifecycle',
'KiasProtocolSystem.cs':'Protocol conditions, fire/atmos latch, distress и outputs',
'KiasRecorderSystem.cs':'Ограниченная история/запись событий',
'KiasRelaySystem.cs':'Управление силовой связностью и reconnect',
'KiasRoomGeometry.cs':'Interior/exterior geometry, directional edges и limits',
'KiasRoomTopologySystem.Diagnostics.cs':'Диагностика room snapshots и revisions',
'KiasRoomTopologySystem.Queries.cs':'Coverage queries, tile/door lookup',
'KiasRoomTopologySystem.cs':'Invalidation, scheduling и commit room topology',
'KiasSafetySystem.cs':'Сигналы опасности и штатные пороги',
'KiasServiceSystem.cs':'Service modes, Test pulse, expiry и diagnostics',
'KiasSystem.CableVisuals.cs':'Маска DATA cable states при topology rebuild',
'KiasSystem.cs':'Device lifecycle, core/roles, DATA graph и availability',
'KiasTransformSystem.cs':'Перенос/вращение и invalidation grid membership',
'KiasUpdateMeasurement.cs':'Update measurement on/off и totals',
'KiasVentilationSystem.cs':'Native vent/pump adapter',
'KiasControllerComponents.cs':'Card/programmer/rack state, persistence и runtime-only поля',
'KiasControllerProgram.cs':'Serializable graph nodes/wires/types/config/bindings',
'KiasControllerUi.cs':'Network BUI messages/states, limits и editor views',
'KiasGraphCatalog.cs':'Typed ports внутренних и device nodes',
'KiasGraphCompiler.cs':'Graph validation, limits, cycles, enum inference и compiled ports',
'KiasGraphMachine.cs':'Typed execution, timers/state, operations и command sinks',
'KiasLegacyGraphTranslator.cs':'Legacy graph → typed program semantics',
'KiasAccessComponents.cs':'Claim/deed/company policy data',
'KiasActuatorComponents.cs':'Actuator configuration, slots и charge data',
'KiasAudio.cs':'Audio channels/messages и routing schema',
'KiasComponents.cs':'Core/device/modules/service state и serialization',
'KiasCoverage.cs':'Coverage geometry, room state и client snapshot schema',
'KiasCrewComponents.cs':'Crew registration/transponder/scanner data',
'KiasDefenceComponents.cs':'Defence scanner/gun configuration и runtime state',
'KiasDevices.cs':'Device roles/types и outputs',
'KiasDisplay.cs':'Monitor state/network schema',
'KiasExternalSensors.cs':'External sensor data/schema',
'KiasHullComponents.cs':'Hull thresholds, impact state и native event contracts',
'KiasLocalUi.cs':'Local messages, scanner/config fields и limits',
'KiasNavigationEvents.cs':'Navigation/FTL bridge event data',
'KiasPowerCableComponent.cs':'KIAS relay component state',
'KiasProtocols.cs':'Protocol definitions, conditions и configuration',
'KiasRelayComponent.cs':'Relay state/configuration',
'KiasSafetyEvents.cs':'Safety signal event contracts',
'KiasTopology.cs':'Topology/availability events и roles',
}
issues = {
'Content.Server/_Forge/KIAS/KiasServiceSystem.cs':'KIAS-01',
'Content.Server/_Forge/KIAS/Controllers/KiasControllerIoSystem.Commands.cs':'KIAS-01',
'Content.Server/_Forge/KIAS/KiasCrewSystem.cs':'KIAS-02',
'Content.Server/_Forge/KIAS/Controllers/KiasControllerIoSystem.cs':'KIAS-03, KIAS-05',
'Content.Server/_Forge/KIAS/Controllers/KiasControllerRuntimeSystem.cs':'KIAS-04, KIAS-05',
'Content.Server/_Forge/KIAS/KiasDisplaySystem.LocalUi.cs':'KIAS-05',
'Content.Server/_Forge/KIAS/KiasPowerSystem.cs':'KIAS-06',
'Content.Server/_Forge/KIAS/KiasProtocolSystem.cs':'KIAS-06, KIAS-07',
'Content.Server/_Forge/KIAS/Controllers/KiasControllerUiSystem.cs':'KIAS-08, KIAS-11',
'Content.Client/_Forge/KIAS/Controllers/KiasControllerWindow.cs':'KIAS-08 (ограниченный сервером каталог)',
'Tools/KiasBenchmark/Server/NativeLab.cs':'KIAS-12',
'README.md':'KIAS-09',
'Docs/KIAS/PULL_REQUEST.md':'KIAS-09',
}
read_docs = {'ARCHITECTURE.md','THIRD_PARTY.md','PULL_REQUEST.md','README.md','MAPPING_EDITOR.md','DEVICE_IDENTIFICATION.md','CONTROLLERS.md','VALIDATION.md'}
cross_tools = {'NativeGameDrivers.cs','KiasTelemetry.cs','NativeLab.cs','NativeProfiler.cs', 'native_replay.ps1','rerun.ps1','prepare_pair.py','build_pair.ps1','source_fingerprint.py','readiness.py','native_proof.py','fullgame_report.py','cost_attribution.py','world_smoke_scenario.py','fullgame_scenario.py','network_proof.py','physics_report.py','physics_experiment.py','fullgame_contract.json'}
bench_purpose = {
'NativeLab.cs':'JSON input, tick clock, measured window, callbacks и file evidence',
'NativeGameDrivers.cs':'Native effects: FTL, docking, weapons, crew, DATA, collision, ventilation и bounded completion',
'KiasTelemetry.cs':'Snapshot/output queues, measured costs, phase counters и direct lab actions',
'NativeProfiler.cs':'Sampling slots и instrumentation boundaries',
'LiveUiLab.cs':'Actual network BUI automation и written-card assertions; не запускалось заново',
'ScannerUiLab.cs':'Server/client coverage agreement и real UI fixture; не запускалось заново',
'KiasLabVisualCommand.cs':'Local visual-state fixtures, capture sequencing; не server/network proof',
'native_replay.ps1':'Isolated binaries, hashes, processes, environment, clients, watchdogs и metrics',
'rerun.ps1':'Strict preflight/readiness, build pair, modes и ABBA orchestration; не запускалось',
'profile.ps1':'Counterbalanced A/B + calibration/ablation; запуск не выполнялся',
'physics_experiment.py':'Replicated plan, B/C/D2 inventory, counter calibration и recovery windows',
'physics_report.py':'Diagnostic scope, nested metrics, narrow smoke и ограничения причинности',
'package_analysis.py':'Legacy archive manifest/checksums; README описывает прежние семь driver pairs',
'load_report.py':'Legacy seven-pair ABBA, minute snapshots; не текущий fullgame reporter',
'compare_native.py':'Legacy synchronous trace schema; не подменяет async fullgame/native_proof',
'live_ui.ps1':'Build flags, real desktop orchestration/restore; нет общего benchmark lease',
'instrument_physics_queries.py':'Instrumented source changes: caller изолирует checkout; самостоятельный root guard требует усиления',
}
for r in rows:
    f,c=r['file'],r['category']; p=Path(f); name=p.name
    if c=='production':
        assert name in purposes, name
        r['status']='CROSS_CHECKED'; r['notes']=purposes[name]+'; исходник и связанный caller/callee просмотрены'
    elif c=='tests':
        t=test_index[f]
        r['status']='READ'
        r['notes']=f"Методы и содержательные assertions просмотрены ({len(t['assertions'])} строк Assert); Release namespace run PASS; не утверждение построчного покрытия"
        if not t['assertions']:r['notes']='Test helper/сохранение артефактов; самостоятельных behavioral assertions нет'
    elif c=='resources':
        if p.suffix=='.png':
            r['status']='VERIFIED';r['notes']='RSI dimensions/frames/transparency; просмотр базового кадра на общем contact sheet, не игровая анимация'
        elif p.suffix=='.json':
            r['status']='VERIFIED';r['notes']='RSI manifest: state names/directions/delays/frame dimensions; rsi-validation.json'
        elif p.suffix=='.ftl':
            r['status']='VERIFIED';r['notes']='Key parity/duplicates и UI label tests; не редактура каждой локализованной строки'
        else:
            r['status']='CROSS_CHECKED';r['notes']='Prototype structure/components/parents/ports/recipes и compiler/physical tests; resource-structure/preset-structure'
    elif c=='benchmark':
        if name.startswith('test_'):
            r['status']='READ';r['notes']='Просмотр назначения/методов и выбранных assertion paths; не полный построчный аудит fixtures. '+('Python suite 107/107 PASS' if p.suffix=='.py' else 'Отдельно PowerShell test script не запускался')
        elif p.suffix=='.md':
            r['status']='READ' if name in {'RUNBOOK.md','DEFERRED_CHECKS.md','ROOM_CONTRACT.md'} else 'NOT_READ'
            r['notes']='Контракт/ограничения сопоставлены с кодом и run status' if r['status']=='READ' else 'Исторический вспомогательный текст; не использован как доказательство PASS'
        else:
            r['status']='CROSS_CHECKED' if name in cross_tools else 'READ'
            r['notes']=bench_purpose.get(name,'Логика инструмента прочитана; запуск/измерение не приписывается статическому чтению')
    elif c=='documentation':
        r['status']='READ' if name in read_docs else 'NOT_READ'
        r['notes']='Архитектура/заявленный контракт сопоставлены с текущим кодом; исторические результаты не считаются свежими' if r['status']=='READ' else 'Архивный текст/исходный art/preview не проверен целиком; продуктовые исходники и runtime RSI проверены отдельно'
        if name=='VALIDATION.md':r['notes']='Просмотр основных заявлений и текущего scope; исторические логи/план не выполнялись повторно'
    else:
        e=external[f]
        r['status']='CROSS_CHECKED' if e['native'] or f=='RobustToolbox' else 'READ'
        r['notes']=e['decision']+': '+e['change']
        if not e['native'] and f!='RobustToolbox':r['notes']+='; просмотр diff/назначения для состава PR, не сертификация всех исторических утверждений'
        if p.suffix in {'.png','.webp','.jpg','.jpeg','.aseprite','.zip'}:
            r['status']='NOT_READ';r['notes']=e['decision']+': бинарный исходный арт/пакет классифицирован по пути и потребителям; внутреннее содержимое не проверено полностью'
        if e['native'] and e['decision'] in {'KEEP','MINIMIZE','REPLACE_WITH_NATIVE_API'}:
            r['status']='ISSUES_FOUND';r['notes']+='; KIAS-10: минимизация/маркировка, не утверждение игрового бага во всём файле'
    if f in issues:
        r['status']='ISSUES_FOUND';r['notes']+='; '+issues[f]
with (OUT/'inventory.csv').open('w',encoding='utf-8-sig',newline='') as stream:
    w=csv.DictWriter(stream,fieldnames=rows[0].keys());w.writeheader();w.writerows(rows)

def link(f,label=None):return f'[{label or f}](<{(ROOT/f).as_posix()}>)'
def clean(s):return str(s).replace('|',' / ').replace('\n',' ')

report='''# KIAS — покрытие исходников, контрактов и оставшиеся проверки

Проверяемый HEAD: `5635bd2c4d1d5ba183700a535f07cfa340cd98ad`. Матрица охватывает **572 файла diff** от merge-base `32268897`. Это перечень изменённых файлов, а не всех файлов Monolith. Дополнительно читались неизменённые native owners/API и два изменённых engine файла. Все 81 существенный собственный production-файл Shared/Server/Client прочитаны и сопоставлены с их межсистемными контрактами; все 122 внешних изменения получили решение в отчёте 03.

## Как читать статусы

- **NOT_READ** — полное содержимое не просмотрено: преимущественно исторические отчёты, исходный арт и вспомогательные тексты. Назначение для состава PR может быть установлено по diff/пути/потребителю; это не выдаётся за проверку содержимого.
- **READ** — код/текст или явно указанная область прочитаны. Для тестов отдельно указаны методы/assertions; это не заявление о построчном разборе каждого fixture.
- **CROSS_CHECKED** — дополнительно сопоставлены producer/consumer, события, guards или сериализация. Не означает, что все сценарии исполнялись.
- **VERIFIED** — выполнена ограниченная указанная проверка (например, структура RSI). Не означает, что файл свободен от всех ошибок.
- **ISSUES_FOUND** — есть находка отчёта 02; её уверенность может быть VERIFIED или HIGH-CONFIDENCE. Для общего кода отсутствие маркировки — архитектурная находка, не доказательство gameplay regression.

Статусы не являются line/branch coverage и не превращают старые лог-файлы в свежий PASS. Непросмотренные полностью архивные материалы не содержат оставшегося существенного production-кода. Нового ручного игрового acceptance и полного Debug/buildAllRelease в этом аудите не было.

## Реально выполненные проверки

| Проверка | Итог | Ограничение |
|---|---|---|
| Release `FullyQualifiedName~Content.IntegrationTests.Tests._Forge.KIAS` | 167 passed, 0 failed, 0 skipped; 11m36s | Существующие assertions, не все жизненные переходы |
| ReviewEvidence, отдельный MSBuild include только из KIAS_REVIEW | 4 passed; 4m7s | Тесты утверждают **наличие дефекта**, а не правильность поведения |
| Python benchmark unittest suite | 107 passed; 32.923s | Есть negative controls/инъекции сбоев; не эквивалент реальным native/GUI прогонам |
| Locale audit | 864 EN / 864 RU; 0 duplicate, 0 keyset difference | Не лингвистическая приёмка всех текстов |
| RSI audit | 65 RSI / 128 states | Manifest/dimensions/frames + contact sheet; не визуальная приёмка анимаций в игре |
| Старый ABBA ZIP | Пересчёт 432000 измеренных тиков, hashes/source correspondence | Новый benchmark не запускался; strictReady=false сохраняется |

Два прозрачных `blank.png` в Pack/display и management намеренны: скрывают наследуемые слои. Их нельзя автоматически считать отсутствующим артом.

Точные команды и logs: `integration-tests.log`, `evidence-tests.log`, `benchmark-python-tests.log`; TRX в `test-results/` и `evidence-results/`. ReviewEvidence подключён через `CustomAfterMicrosoftCommonTargets`, tracked csproj/tests не правились. В будущем эти тесты нужно инвертировать на требуемое поведение и перенести в штатные fixtures после исправления, а не оставить assertion дефекта.

## Проверенные межсистемные контракты

| Цепочка | Что проверено в коде/существующих тестах | Остаточный пробел / при 40 кораблях |
|---|---|---|
| Startup/anchor/grid → KiasSystem → DATA registry → availability | Central directed startup, attach/detach, grid-scoped devices, core/roles, dirty rebuild, no-power/core removal | Repeated split/move/delete в середине dispatch; camera и output rebuild уже воспроизведены |
| Cable/relay → native NodeGroup/Power → control endpoint | DATA не превращается в native power cable; relay не теряет ON после OFF; native off/on tests | Смена роли при живом core оставляет deficit, KIAS-06 |
| Physical card → compiler → VM → queue → native command | Finite values, 128 nodes/256 wires/32 external nodes, typed ports/enums, bounded work, faults/epoch | Ready grid задержан dirty grid, KIAS-04; burst 40 grids требует latency/fairness assertions |
| Service Test → sensor pulse → graph timer → actuator | Текущий Testing gate проверяется при исполнении, истекает через 5 s | 6 s timer проходит позже и меняет реальный мир, KIAS-01; timer phase в готовом ABBA вообще 0 calls |
| UI message → owner/access → draft/revision → WRITE/card persistence | Company/deed/POI/claim, чужой actor, invalid revision/binding, delete, save/load, discard/eject | >256 device/profile rows; concurrent editor/topology changes; double Refresh |
| Frozen mapping → CanMapEdit → editor | Uninitialized + paused + Mapping admin + same map; runtime не включается | Сохранить negative initialized/ordinary pause/revoked-admin cases при патчах |
| Room topology → scanner coverage → selectors → native effect | Interior/exterior, doors, direction, pose, overflow bounds, explicit breach/repair | Room config не reindexes selector cache; nonempty tile replacement нуждается в отдельном native-event test |
| Crew/transponder → registration → critical/threat/fauna → output | Actual tracked crew/visitor/hostile/harmless controls; room geometry и movement | Individual DATA off у active optical camera; repeat scanner churn и retained-key accounting |
| Alarm native state → protocol distress → light/audio | Danger/normal events, role/online gates, bounded signal emit | Offline FireClear пропускается, KIAS-07; reconnect должен reconcile authoritative state |
| Native damage/shot/impact → hull/external signals | DamageChanged semantics, healing filtering, gun AmmoShot, post-impact ordering | Существующие bridges damage/gun лишние; immediate-delete target во время impact не воспроизведён |
| Actuator → fire foam/vent/AME/jammer/light | Нативные containers/charge/power/ammo/flow и actual world state; physical preset tests | Role/data transitions во время pending effect; на 40 судах не доказана каждая физическая задержка |
| Defence → FireControl/gun → projectile/interception | Friendly/hostile filtering, ammo/cassette, finite gun magazine, IFF/native reservation | 181/240 PDC interceptions в каждом B — не 100%; без подмены contract более строгим |
| Navigation → shuttle native API → FTL/dock/collision | Настоящие effects/postconditions в driver, не один mock signal | JSON rotation не попадает в driver, KIAS-12; arbitrary requested collision angles пока не доказаны |
| DeviceLink/DeviceNetwork → speaker/output | Native endpoint schema и direct default speaker path | Custom network speaker SourcePort route NEEDS-TEST; default speaker bug не подтверждён |
| BUI shared state → client window/canvas/world overlay | Headless layout/ports, finite values, readable Russian labels, pointer transforms; lab client/server code | LiveUi fixtures используют автоматические handlers/reflection; новых ручных GUI проверок нет |
| Recorder/cooldown/schedulers → bounded state | Scope и cleanup, phase/update counters, disabled measurement | Managed retained heap после длительного entity churn не измерен; working set не доказывает leak |

## Что существующие тесты действительно подтверждают

Ниже перечислены все 51 test/support файл с реальными именами методов и числом строк assertions. Полный извлечённый индекс сохранён в `test-contracts.json`; он нужен для проверки предмета assertions, а не подсчёта баллов качества. Parameterized случаи объясняют, почему число PASS больше числа методов. Helper без Assert не является отдельным behavioral test.

Сильные места suite: реальные physical cards/containers, native power off→on, topology/epoch stale work, negative access controls, реальные foam/fire/atmos эффекты, сохранение программы и геометрия. Но совпадение happy path не проверяет late diagnostic work, individual device disconnect, unchanged-output retention и независимость очередей — именно поэтому новые 4 воспроизведения проходят при зелёном исходном suite.

| Test/support файл | Методы / заявленный сценарий | Строк Assert |
|---|---|---:|
'''
for t in tests:
    report+=f"| {link(t['file'],Path(t['file']).name)} | {clean('; '.join(t['methods']) or 'Support helper')} | {len(t['assertions'])} |\n"
report+='''
## Ограничения benchmark tooling

Все игровые C# lab drivers и orchestration entrypoints прочитаны. Они не равны реальному человеку за клиентом: lab целенаправленно заряжает устройства, отключает shield fixtures, создаёт медицинские/боевые воздействия и выполняет штатные APIs от имени подготовленных акторов. Некоторые lighting/suppression воздействия в telemetry идут прямо в IO/native signal; это не доказательство прохождения каждого эффекта через пользовательский граф. Отдельные physical-card tests покрывают такой путь для presets.

`NativeLab` rotation bug требует исправить парсер и сравнивать requested/actual angle. `network_proof` использует периодическую историю, а не непрерывное наблюдение каждого сетевого кадра. Реальная клиентская активность в архиве — более сильное доказательство, чем один final counter, но не доказательство полного игрового покрытия.

Legacy `compare_native`/`load_report`/`package_analysis` описывают прежние семь пар синхронных драйверов. Их жёсткие старые формулировки не следует переносить на нынешний fullgame report; свежая серия проверяется её manifest, actual event set и current reporter. Сценарные Python tests часто проверяют JSON generation или смоделированный proof: они не поймали потерю optional поля при C# deserialization.

PowerShell wrappers прочитаны, но отдельные PS test scripts не запускались. В Python suite есть subprocess проверки fail/continue/port behavior; это не заявление, что выполнены все PS scripts. `live_ui.ps1` ещё не использует общий benchmark lease, в отличие от scanner-ui/replay; параллельный самостоятельный запуск требует отдельной защиты. Standalone physics query instrumentation следует ограничить изолированным checkout: текущий основной caller это обеспечивает, самостоятельный helper — не такой же строгий boundary. Эти замечания к инструментам не выданы за обнаруженные gameplay defects.

## Приоритетные новые регрессии

| Приоритет | Сценарий | Обязательный assertion |
|---|---|---|
| До merge | Test → 6 s timer → actuator, затем повторить с latch/counter/feedback | Ни поздний native effect, ни диагностическое загрязнение live VM state не возникают |
| До merge | Camera active/viewer → только её DATA off; core остаётся on | Native Active=false и реальные viewers сняты; reconnect допустим по правилам |
| До merge | Boolean/Number/String/Entity outputs → no-op rebuild | Valid connected state сохранён; deleted/moved/offline state очищен; pulse не повторяется |
| До merge | Dirty grid A стоит первым, ready grid B имеет event/work/command | B выполняется в заданном бюджете без ожидания A; порядок внутри каждого grid сохранён |
| До merge | Room A→B для scanner/crew/wireless с живыми selectors | ALL/ANY/SPECIFIC фильтры получают обновлённый membership без unrelated topology rebuild |
| До merge | Deficit → снять последний Power role; core остаётся live | Старый deficit не выдаётся как текущая измеренная неисправность |
| До merge | Fire Danger→disconnect→Normal→reconnect | Distress соответствует native alarm state; потеря связи не подменяет fire/clear |
| До merge | >256 device/profile rows, поиск позднего ID | Нужное конкретное устройство выбирается ограниченным UI payload |
| До утверждения rotated proof | JSON angle absent/0/37/−0.054/359.9/invalid | Requested angle прочитан, фактическая начальная rotation совпадает; malformed отвергнут |
| После минимизации native hooks | Same native event без KIAS и с KIAS | Обычный игрок получает прежнее поведение, KIAS получает ровно один корректный signal |
| Перед performance claims | 40 grids, burst/dirty/delete/restore, matched instrumentation | Latency distributions, no silent drops, одинаковый work/effect volume; measured benefit отдельно |

Для HIGH-CONFIDENCE сначала добавить regression, воспроизвести текущий дефект и только затем менять код. Не менять семантику latched alarms/outputs молча; ожидаемый контракт должен быть указан в тесте. Для гипотез Plating↔Lattice, custom network speaker, immediate deletion и long churn сначала доказать фактическую цепочку.

## Ручная игровая проверка после исправлений

1. Открыть programmer несколькими игроками: чужой доступ, stale draft, WRITE/discard/eject, потеря питания и восстановление; отдельно frozen mapping admin и обычный paused map.
2. Проверить canvas на 850×500, 1200×720 и 1600×900: zoom/pan/ports, длинные русские имена, список >256 строк, selection после network update. Автоматические layout assertions не подтверждают удобство мыши.
3. Пройти room doors/breach/repair, scanner rotate/move, DATA cut одного устройства, camera viewer и восстановление alarms; проверить звук/радио и видимую индикацию.
4. В реальном native сценарии проверить late Test, PDC friendly/occlusion/empty/reload, FTL/docking/collision и задержку фактического эффекта. Не считать accepted dispatch временем завершения эффекта.
5. Осмотреть установку и анимации спрайтов на стене/поворотах, core occlusion, cable masks и blank inherited layers. Contact sheet не заменяет эту проверку.

Ни один из этих пунктов не помечен выполненным в текущем аудите. Новая длинная ABBA не обязательна для доказательства маленького correctness fix; для заявлений об ускорении нужен отдельный согласованный matched эксперимент.

## Полная матрица diff

'''
counts=Counter(r['status'] for r in rows)
report+='Итоги: '+', '.join(f'**{k}: {v}**' for k,v in sorted(counts.items()))+'.\n\n'
for category,title in [('production','Production'),('tests','Integration tests'),('resources','Ресурсы'),('benchmark','Benchmark'),('documentation','Документация KIAS'),('external','Внешние файлы')]:
    subset=[r for r in rows if r['category']==category]
    report+=f'### {title} — {len(subset)}\n\n| Файл | Статус | Реальный объём проверки / назначение |\n|---|---|---|\n'
    for r in subset: report+=f"| {link(r['file'])} | {r['status']} | {clean(r['notes'])} |\n"
    report+='\n'
report+='''### Дополнительно просмотренные native owners

Кроме строк diff прослежены native CameraSystem/Active→viewers, SharedGun/AmmoShot/CancelShots, Damageable→DamageChanged→Destructible/QueueDel, ProjectileHit до damage, DeviceLink/source ports/DeviceNetwork, CableNode/NodeGroup и PipeNode, Atmos Alarm/Airtight/Door/GridTile event chain, company/deed/shipyard, ItemSlots/power и BUI authority. Для этих владельцев прочитаны релевантные методы/контракты, а не весь многотысячный engine/project tree.

Оба engine файла `Robust.Shared/CVars.cs` и `Robust.Shared/Network/NetManager.cs` — CROSS_CHECKED в пределах gitlink delta и причинной связи с KIAS. Это independent network fix, не Physics fix; подробности в отчёте 03 и engine.diff.

Полнота данного code review означает завершённый просмотр существенного production и внешних интеграций. Она не означает доказанную безошибочность, полную gameplay-сертификацию, просмотр каждого исторического изображения/лога или выполнение всех будущих тестов из этого документа.
'''
(OUT/'06_COVERAGE_AND_TEST_GAPS.md').write_text(report,encoding='utf8')
print(json.dumps(dict(files=len(rows),statuses=counts,production=Counter(r['status'] for r in rows if r['category']=='production')),ensure_ascii=False,indent=2))
