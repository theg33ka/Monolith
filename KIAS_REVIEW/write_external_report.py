import csv, json, re, subprocess
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'KIAS_REVIEW'
rows = list(csv.DictReader((OUT/'inventory.csv').open(encoding='utf-8-sig')))
BASE = '3226889714d52cad147566c623dd7f18debd21ed'
catalog = {}
def entry(path, change, need, alternative, decision):
    catalog[path] = (change, need, alternative, decision)

entry('.gitignore', 'Исключения локальных lab/build/log-артефактов; не влияет на игру', 'Да, для локальной лаборатории', 'Локальный .git/info/exclude возможен, но не передаёт правила команде', 'KEEP')
entry('Content.Client/Content.Client.csproj', 'Три условных Compile Include: KiasLabVisual, KiasLabScannerUi, KiasLabLiveUi; по умолчанию выключены', 'Да, если сохранять лабораторию', 'Отдельный импортируемый targets внутри Tools/KiasBenchmark уменьшит три включения до одного; игровая сборка без флагов неизменна', 'MINIMIZE')
entry('Content.Server/Content.Server.csproj', 'Условное включение серверных ScannerUiLab и LiveUiLab', 'Да, если сохранять лабораторию', 'Вынести список Compile в KIAS targets; не включать диагностику без флага', 'MINIMIZE')
entry('Content.Client/Shuttles/UI/ShuttleNavControl.xaml.cs', 'protected virtual DrawSensorCoverage в рендере навигации; базовая реализация пустая', 'Да, для наложения KIAS на штатный радар', 'Наследник KIAS уже используется; копирование всего ShuttleNavControl хуже узкого hook. Проверить обычный радар и координаты FTL', 'KEEP')
for path in ['Content.Client/_Forge/Radio/Ui/ConfigurableEncryptionKeyMenu.xaml.cs','Content.Client/_NC/Radio/UI/HandheldRadioMenu.xaml.cs']:
    entry(path, 'Валидация пользовательского ввода радиочастоты; обычные радиоустройства меняют поведение', 'Нет для KIAS', 'Изменение из предшествующей работы Station AI/radio; отдельная доставка вместе с RadioFrequencyInputTest', 'INDEPENDENT_UPSTREAM_FIX')
entry('Content.IntegrationTests/Tests/UserInterface/RadioFrequencyInputTest.cs', 'Регрессия ввода частот для двух нативных окон', 'Нет для KIAS', 'Сохранить с независимым radio bugfix; не считать KIAS-покрытием', 'INDEPENDENT_UPSTREAM_FIX')
entry('Content.IntegrationTests/Tests/_Forge/StationAi/StationAiPrototypeTests.cs', 'Удалён assertion FixedVoice', 'Нет для KIAS', 'Поставлять вместе с удалением FixedVoice из Station AI', 'INDEPENDENT_UPSTREAM_FIX')
entry('Content.Server/_Forge/TTS/TTSSystem.cs', 'Удалена специальная обработка фиксированного голоса Station AI', 'Нет для KIAS', 'Отдельная цепочка Station AI/TTS; проверить смену голоса и обычный TTS', 'INDEPENDENT_UPSTREAM_FIX')
entry('Content.Shared/_Forge/TTS/TTSComponent.cs', 'Удалено поле FixedVoice', 'Нет для KIAS', 'Отдельный TTS-патч с соответствующими прототипами/тестом', 'INDEPENDENT_UPSTREAM_FIX')
entry('Resources/Prototypes/Entities/Mobs/Player/silicon.yml', 'Удалена настройка FixedVoice у Station AI', 'Нет для KIAS', 'Не отделять от соответствующего общего TTS-изменения', 'INDEPENDENT_UPSTREAM_FIX')
entry('Content.Client/_Mono/Audio/AudioEffectSystem.cs', 'CachedEffects из static в поле экземпляра; изоляция миров/IoC', 'Нет для игровой KIAS; полезно тестам', 'Отдельный общий bugfix. Проверить несколько клиентских миров и освобождение эффектов', 'INDEPENDENT_UPSTREAM_FIX')
entry('Content.Server/Ame/EntitySystems/AmeControllerSystem.cs', 'Событие InjectionChanged после изменения впрыска AME', 'Да, нужен источник фактически изменившегося состояния', 'Общее нейтральное событие лучше polling/дублирования AME; оставить подписчик в KiasAmeAdapterSystem', 'KEEP')
entry('Content.Server/Atmos/EntitySystems/AtmosphereSystem.RoomSafety.cs', 'Публичный ограниченный запрос безопасности комнаты, BFS до 625 клеток', 'Да, нужен доступ к авторитетным atmos tiles', 'GridAtmosphere.Tiles ограничен Access атрибутом: перенос метода в KIAS не даёт законного API. Оставить малый общий запрос, проверить границы/неполные комнаты', 'MINIMIZE')
entry('Content.Server/Atmos/Monitor/Systems/AtmosAlarmableSystem.cs', 'Source в AtmosAlarmEvent и заполнение источника при отправке', 'Да, для правильной привязки тревоги к устройству/гриду', 'Нейтральный Source предпочтителен сканированию всех сигнализаций; значение optional для старых отправителей', 'KEEP')
entry('Content.Server/Chat/Managers/ChatSanitizationManager.cs', 'Кэш compiled Regex по культуре вместо повторной сборки выражения', 'Нет для KIAS', 'Самостоятельная оптимизация chat с проверкой смены культуры/правил; общая для A и B', 'INDEPENDENT_UPSTREAM_FIX')
entry('Content.Server/DeviceLinking/Systems/DeviceLinkSystem.cs', 'InvokeSink, событие порта, SourcePort в прямом сигнале; также прямая проверка режима KIAS', 'Частично: узкий мост нужен', 'Оставить общий API/событие; KIAS test-policy перенести в подписчик нейтрального cancellable attempt. Прямой SourcePort — общий fix, network packet требует отдельной проверки', 'MINIMIZE')
entry('Content.Shared/DeviceLinking/Events/DeviceLinkPortInvokedEvent.cs', 'Нейтральное событие вызова исходящего порта', 'Да, для наблюдения реального DeviceLink сигнала', 'Оставить малое событие; слушает KIAS, нативная система не должна знать графы', 'KEEP')
entry('Content.Shared/DeviceLinking/Events/SignalReceivedEvent.cs', 'Optional SourcePort в конструкторе события', 'Да, для идентификации прямого сигнала', 'Сохранить обратно совместимый параметр; отдельно проверить network round-trip с нестандартным сетевым динамиком', 'KEEP')
entry('Content.Shared/DeviceLinking/SharedDeviceLinkSystem.cs', 'В подборе default links ранний return заменён continue', 'Нет для KIAS', 'Общий баг первого несовместимого pair: тест с несовместимой первой и совместимой второй парой, отдельный PR', 'INDEPENDENT_UPSTREAM_FIX')
entry('Content.Server/DeviceNetwork/Systems/NetworkConfiguratorSystem.cs', 'Убрана узкая проверка BoardingTeleport; режим Link доступен всем подходящим DeviceNetwork+DeviceLink устройствам', 'Текущая широта не обязательна', 'Узкий выбор режима для KIAS через общий preference hook или существующую специализацию; проверить обычные сетевые приборы. Это меняет non-KIAS UX', 'MINIMIZE')
entry('Content.Server/Power/EntitySystems/PowerMonitoringConsoleSystem.cs', 'Пропуск CableType.Data в трёхканальном мониторе мощности', 'Да при отдельном DATA CableType', 'Не включать DATA в силовые массивы; отдельный тип кабеля не должен индексировать массив длины 3', 'KEEP')
entry('Content.Server/Power/Nodes/CableNode.cs', 'Connectable обращается к KiasRelaySystem', 'Частично: отключение кабельной связности нужно', 'У CableNode нет штатного общего Enabled. Нужен минимальный нейтральный gate/ConnectionsEnabled с rebuild; PipeNode-only API не подходит. Unanchor/Delete кабеля недопустимы', 'MINIMIZE')
entry('Content.Shared/Power/SharedPower.cs', 'Новый CableType.Data', 'Да для текущей отдельной DATA-проводки', 'Можно полностью заменить кабельную модель, но это дороже и рискованнее одной enum-интеграции; сохранить фильтры power-monitor', 'KEEP')
entry('Content.Server/Radio/EntitySystems/JammerSystem.cs', 'Выделен публичный SetEnabled с прежним изменением нативного состояния', 'Да, нужен штатный command API', 'Оставить нейтральный setter; KIAS role/online-проверки внутри KIAS. Не копировать работу Jammer в адаптер', 'KEEP')
entry('Content.Server/Shuttles/Systems/ShuttleConsoleLockSystem.cs', 'Использование ShipAccessReaderSystem.HasDeedAccess вместо дублирования поиска ID/deed', 'Частично: общий доступ нужен', 'Публичный helper оставить в ShipAccessReader; изменение поведения null-deed отделить как общий bugfix и проверить обычный shuttle lock', 'MINIMIZE')
entry('Content.Shared/_Mono/Shipyard/ShipAccessReaderSystem.cs', 'FindAccessibleIdCards/HasDeedAccess; недействительные/null deeds не считаются совпадением', 'Да для единого правила владения', 'Не дублировать ID/PDA обход в KIAS; security fix null==null выделить отдельным коммитом/тестом', 'MINIMIZE')
entry('Content.Shared/_Mono/Company/CompanyAccessReaderSystem.cs', 'IsAllowed выделяет существующее правило доступа компании', 'Да для нативной проверки доступа', 'Публичный нейтральный запрос лучше копии company policy в KIAS; проверить пустые/чужие карты', 'KEEP')
entry('Content.Server/Shuttles/Systems/ShuttleSystem.Impact.cs', 'Два KIAS collision event после принятого столкновения; dedup перенесён после порога', 'Частично: нужен достоверный collision input', 'Общий post-accepted-impact event вместо ссылки на KIAS. StartCollideEvent потребует копирования критерия удара. Перестановка dedup — самостоятельный общий bugfix', 'MINIMIZE')
entry('Content.Server/Weapons/Ranged/Systems/GunSystem.cs', 'Дополнительный KiasWeaponFiredEvent после штатного AmmoShotEvent', 'Нет, сигнал уже существует', 'Глобальная фильтрованная подписка KIAS на AmmoShotEvent; не занимать уже занятый SpaceArtillery+AmmoShot directed pair. Удалить KIAS import/event из GunSystem', 'REPLACE_WITH_NATIVE_API')
entry('Content.Shared/Damage/Systems/DamageableSystem.cs', 'Положительный damage и KiasHullDamageEvent перед штатным DamageChangedEvent', 'Нет, данные уже доступны', 'Подписка KiasHullStructure+DamageChangedEvent, positive DamageDelta, порядок до DestructibleSystem; проверить уничтожение, healing, отмену и нулевой урон', 'REPLACE_WITH_NATIVE_API')
entry('Content.Shared/Projectiles/SharedProjectileSystem.cs', 'KiasHullImpactEvent после фактического положительного projectile damage', 'Частично: нужен post-damage input', 'Существующий ProjectileHitEvent возникает ДО применённого урона и может быть Handled. Минимальный общий post-damage event; захватить grid/coords до удаления, фильтровать в KIAS', 'MINIMIZE')
entry('Content.Shared/Anomaly/Components/AnomalyComponent.cs', 'PreviousStability у AnomalyStabilityChangedEvent', 'Да для перехода порога', 'Нейтральные old/new данные позволяют локальный KIAS subscriber без нового глобального кэша', 'KEEP')
entry('Content.Shared/Anomaly/SharedAnomalySystem.cs', 'Заполнение PreviousStability при повышении/снижении', 'Да совместно с контрактом события', 'Сохранить общий event; проверить рост, спад и отсутствие ложного повторного crossing', 'KEEP')
entry('Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.cs', 'Публичный CancelShots сбрасывает RemainingTime, восстанавливает ShootCoordinates и делает Dirty', 'Да, нужен корректный отказ от очереди стрельбы', 'Сохранить нейтральный native API, не править поля оружия из KIAS; тест очереди/охлаждения', 'KEEP')
entry('Content.Server/_Mono/NPC/HTN/Operators/ShipMoveToOperator.cs', 'Удалён singleton completion event; успешный выход только по InRange до Stop', 'Нет для KIAS', 'Независимый autopilot fix; тест отмены, успеха, смены target; включён одинаково в A/B', 'INDEPENDENT_UPSTREAM_FIX')
entry('Content.Server/_Mono/Shuttles/Systems/ShuttleConsoleSystem.Autopilot.cs', 'KiasAutopilotCompletedEvent при достижении target', 'Частично: completion input нужен', 'Нейтральное completion event на границе нативного успеха. Не добавлять второй занятый directed SteeringDone pair и не угадывать успех по Stop', 'MINIMIZE')
entry('Content.Shared/Shuttles/Systems/SharedShuttleSystem.IFF.cs', 'Mayday суффикс в GetLabel и SetKiasMayday с Dirty', 'Частично: отображение Mayday нужно', 'Нейтральное расширение подписи/изменения IFF; KIAS policy в своей системе, сохранить hide-IFF, dirty и клиентскую синхронизацию', 'MINIMIZE')
entry('Content.Shared/_Mono/FireControl/FireControlMessages.cs', 'Флаг KIAS reservation в UI записи орудия', 'Да для объяснения недоступности ручного огня', 'Нейтральный Reserved/ControlStatus вместо названия системы; не убирать обратную связь из UI', 'MINIMIZE')
entry('Content.Client/_Mono/FireControl/UI/FireControlWindow.xaml.cs', 'Подсказка о резервировании орудия KIAS', 'Да совместно с серверным запретом', 'Отображать общий control status; KIAS-specific текст можно передавать локализационным ключом', 'MINIMIZE')
entry('Content.Server/_Mono/FireControl/FireControlSystem.Console.cs', 'Заполнение признака резервирования', 'Да совместно с запретом ручного огня', 'Общий запрос reservation с KIAS subscriber; сохранить online/power состояния списка', 'MINIMIZE')
entry('Content.Server/_Mono/FireControl/FireControlSystem.cs', 'AttemptFire спрашивает KIAS и принимает kiasAutomatic', 'Частично: исключение ручного/автоматического огня нужно', 'Общий cancellable pre-fire attempt с источником управления. Не переносить проверку после aim/fire: это уже побочный эффект', 'MINIMIZE')
for path, change in [
 ('Resources/Prototypes/Entities/Structures/Machines/lathe.yml','Добавлены две категории/пакета KIAS recipes в штатные производители'),
 ('Resources/Prototypes/Entities/Structures/Power/cables.yml','KiasPowerCable на базовом силовом кабеле для relay'),
 ('Resources/Prototypes/Entities/Structures/Walls/walls.yml','KiasHullStructure на базовой стене'),
 ('Resources/Prototypes/Entities/Structures/Windows/window.yml','KiasHullStructure на двух базах окон'),
 ('Resources/Prototypes/_Mono/Entities/SpaceArtillery/SpaceArtillery/Kinetic/flarelauncher.yml','KiasDecoyLauncher на штатной flare launcher'),
 ('Resources/Prototypes/_Mono/Entities/SpaceArtillery/SpaceArtillery/Kinetic/standardlight_kinetic.yml','KiasPdc и KiasDevice на двух штатных лёгких орудиях')]:
    entry(path, change, 'Да для охвата штатных объектов', 'Малые component/recipe tags дешевле глобального attach на startup. Без активной KIAS native behavior должен сохраняться; проверить обычную карту', 'KEEP')
entry('Resources/Maps/_Forge/Shuttles/Archive/Mercenary/briarKIAS.yml','Копия карты Briar с KIAS для лаборатории, 24762 добавленных строки', 'Нет в общем каталоге игровых кораблей', 'Перенести fixture в каталог карт KIAS/лаборатории и обновить только test/lab пути; не объявлять ненужной саму тестовую карту', 'MOVE_TO_KIAS')
entry('RobustToolbox','Gitlink 96c6bddc → c2fc1375; два файла защиты от decryption failures', 'Нет для KIAS/Physics', 'Полностью исключить engine delta из KIAS поставки; если исправление нужно, отдельный engine PR с нагрузочными/сетевыми тестами', 'INDEPENDENT_UPSTREAM_FIX')
entry('README.md','Удалены 58 строк README проекта, добавлено 5 строк задания лабораторному агенту', 'Нет', 'Восстановить README проекта; полезный KIAS runbook в Docs/KIAS, без оперативных указаний агенту в корневом README', 'REVERT')
for path in ['Run_KIAS_Tests.bat','Run_KIAS_Intermediate_Tests.bat']:
    entry(path,'Короткая BAT-точка входа в Tools/KiasBenchmark', 'Не обязательно в корне, но полезный интерфейс владельца', 'Оставить один удобный launcher или оба осознанно; перенос в Tools ухудшит привычный запуск без заметного выигрыша', 'KEEP')

def fallback(path):
    if path.startswith('Resources/Locale/'):
        return ('Отдельная локализация KIAS '+('RU' if '/ru-RU/' in path else 'EN'), 'Да, собственный ресурс в стандартном locale namespace', 'Уже самостоятельный KIAS файл; физический перенос в подпапку необязателен', 'KEEP')
    if path.startswith(('assets/','references/')):
        return ('Исходный/референсный арт '+Path(path).name, 'Нет для загрузки игры', 'Полезные исходники в Docs/KIAS/art или отдельном art-пакете; runtime RSI уже в Resources/Textures/_Forge/KIAS', 'MOVE_TO_KIAS')
    if path.startswith('history/'):
        return ('Архив ранее применённого задания '+Path(path).name, 'Нет для игрового PR', 'Внешний архив аудита/задачи; не переносить обязательные указания из старого задания в действующий runbook', 'REVERT')
    if path in ['MANIFEST.json','MANIFEST.md','PACKAGE_MANIFEST.json','SHA256SUMS.txt','APPLY.md','COPYPASTE_PROMPT.md','PATCH_PROMPT.md','00_AGENT_MASTER_PROMPT.md','00_MASTER_PROMPT.md','CHANGELOG_V2.md','CHANGELOG_V3.md']:
        return ('Файл пакета задания/манифест '+path, 'Нет для runtime и обычного contributor', 'Исключить из PR; сохранить за пределами checkout как историю задачи. Актуальные правила перенести в один runbook', 'REVERT')
    if re.match(r'^\d\d_.*\.md$',path):
        return ('Спецификация/план '+path, 'Нет в корне проекта', 'После сверки с кодом объединить актуальное в Docs/KIAS; устаревшие задания архивировать, не сохранять несколько конкурирующих master prompts', 'MOVE_TO_KIAS')
    raise ValueError(path)

external=[]
for row in rows:
    if row['category']!='external': continue
    path=row['file']
    change,need,alt,decision=catalog.get(path) or fallback(path)
    native=path.startswith(('Content.','Resources/Prototypes/'))
    cs=path.endswith(('.cs','.csproj','.yml'))
    text=(ROOT/path).read_text(encoding='utf-8-sig') if cs else ''
    exact='#Forge-change START (KIAS):' in text and '#Forge-change END (KIAS)' in text
    marker=('НЕТ: обязательны узкие KIAS-блоки' if native and decision not in ['INDEPENDENT_UPSTREAM_FIX','REPLACE_WITH_NATIVE_API'] else
            'Не KIAS: отдельная маркировка общего fix' if decision=='INDEPENDENT_UPSTREAM_FIX' else
            'Нет; после удаления native-вставки не нужны' if decision=='REPLACE_WITH_NATIVE_API' else 'Не требуется для самостоятельного файла/артефакта')
    if exact: marker='Есть; проверить границы'
    diff=subprocess.check_output(['git','diff','--unified=0',BASE,'HEAD','--',path],cwd=ROOT).decode('utf-8')
    h=re.search(r'^@@[^\n]*\+(\d+)',diff,re.M)
    line=max(1,int(h.group(1))) if h else 1
    external.append(dict(**row,change=change,need=need,alternative=alt,decision=decision,marker=marker,line=line,native=native))

counts=Counter(r['decision'] for r in external)
native=[r for r in external if r['native']]
retained=[r for r in native if r['decision'] in ('KEEP','MINIMIZE')]
eliminable=[r for r in external if r['decision'] in ('MOVE_TO_KIAS','REVERT','REPLACE_WITH_NATIVE_API','INDEPENDENT_UPSTREAM_FIX')]
lines=lambda rs:sum(int(r[k]) for r in rs for k in ('added','deleted') if r[k].isdigit())
summary=dict(externalFiles=len(external),textChangedLines=lines(external),binaryFiles=sum(r['added']=='-' for r in external),decisions=dict(counts),nativeFiles=len(native),retainedNativeFiles=len(retained),potentiallyEliminableFiles=len(eliminable),retainedNativeChangedLines=lines(retained),potentiallyEliminableChangedLines=lines(eliminable),missingKiasMarkers=sum(r['marker'].startswith('НЕТ:') for r in external))
(OUT/'external-decisions.json').write_text(json.dumps(dict(summary=summary,files=external),ensure_ascii=False,indent=2),encoding='utf-8')
def link(path,line=1):
    suffix = f':{line}' if (ROOT/path).is_file() and not any(r['file']==path and r['added']=='-' for r in external) else ''
    return f'[{path}](<{ROOT.as_posix()}/{path}{suffix}>)'
report='''# Аудит внешних изменений KIAS

Снимок: HEAD `5635bd2c4d1d5ba183700a535f07cfa340cd98ad`, merge-base `3226889714d52cad147566c623dd7f18debd21ed`. Таблица исчерпывает буквальный список файлов за пределами собственных каталогов из inventory.csv. Отдельные FTL, BAT и документы здесь считаются внешними по пути, хотя не создают зависимости нативной игры от KIAS.

`KEEP` сохраняет необходимую малую интеграцию; `MINIMIZE` сохраняет требуемую возможность, сужая coupling/изменённый участок; `MOVE_TO_KIAS` относится к месту поставки; `REPLACE_WITH_NATIVE_API` означает найденный существующий API, а не воображаемый новый; `INDEPENDENT_UPSTREAM_FIX` нужно поставлять отдельно от KIAS, если владелец хочет сохранить исправление. Никаких удалений в рамках аудита не выполнено.

## Полная таблица

| Файл (+/−) | Что изменено и зачем | Обязательно? | Альтернатива внутри KIAS / нативный API / риск | Решение | #Forge-change |
|---|---|---|---|---|---|
'''
for r in external:
    report+=f"| {link(r['file'],r['line'])} (+{r['added']}/−{r['deleted']}) | {r['change']} | {r['need']} | {r['alternative']} | **{r['decision']}** | {r['marker']} |\n"
report+='''
## Где вмешательство действительно необходимо

Рисковая граница — не каждый новый публичный метод. `JammerSystem.SetEnabled`, `SharedGunSystem.CancelShots`, запросы ShipAccessReader/CompanyAccessReader, old/new anomaly event и bounded atmos query дают KIAS авторитетные нативные действия/данные без копии правил игры. Эти расширения нужно оставить узкими и покрыть обычные non-KIAS сценарии. Вызовы после валидации DeviceLink и принятого столкновения тоже имеют смысл; названия событий и policy не должны заставлять нативные системы знать внутреннюю VM.

Особенно важно не предлагать несуществующий API: у CableNode нет общего переключателя связности; PipeNode.ConnectionsEnabled не решает задачу силового кабеля. Atmos tiles имеют ограничение Access; перенос partial-файла в другую папку не устраняет зависимость. ProjectileHitEvent приходит до damage и не эквивалентен подтверждённому положительному попаданию. Для этих трёх мест узкая native-интеграция обоснована.

Напротив, `AmmoShotEvent` и `DamageChangedEvent` уже дают достаточный контракт для weapon-fired и hull-damage при правильном фильтре и порядке исполнения. Эти две native KIAS-вставки устранимы. FireControl должен сохранять reservation до изменения aim/fire; убрать проверку без замены означает регрессию. IFF должен сохранять Dirty и hidden/label semantics.

Правки `_NF` отсутствуют в буквальном diff. `_NC` затронут radio UX, `_Mono` — Audio, FireControl, autopilot, Company/Shipyard и орудийные прототипы. Их решения перечислены отдельно для каждого файла. Основной риск обновлений upstream сосредоточен в DeviceLink, FireControl, Damageable, Projectile, CableNode, Shuttle/IFF, а не в отдельных новых KIAS файлах.

## RobustToolbox: полностью отделимая поставка

Gitlink меняет `96c6bddcebab253182a0e0eeec8e39d6a87e8264` на `c2fc1375fc8647f4a29d2e9c8a8ae86dfb35d66e`. Внутри движка ровно два изменённых файла, **80 добавленных и 10 удалённых строк**:

| Файл | Изменение | Решение и риск |
|---|---|---|
| Robust.Shared/CVars.cs | CVar окна ошибок дешифрования | INDEPENDENT_UPSTREAM_FIX. Default 60 секунд, нижний предел 1; не связано с KIAS или Physics |
| Robust.Shared/Network/NetManager.cs | Два временных bucket, IP/subnet счётчики, очистка старых записей, обработка смены времени | INDEPENDENT_UPSTREAM_FIX. Нужны отдельные тесты границы окна, expiry, большого числа адресов, NAT/subnet и разрешённого трафика |

Ни KIAS-компилятор, ни устройства, ни измеренный Physics не используют этот новый API. Engine delta можно целиком исключить из KIAS PR; после такого будущего изменения всё равно нужно заново собрать и прогнать релевантные проверки на базовом движке. Старые ABBA использовали один и тот же c2fc в обеих ветвях, поэтому не измеряют пользу этого fix. Движок не изменялся в ходе аудита. Файл engine.diff сохраняет точные hunks. Существующая старая Forge-маркировка engine fix не является требуемой KIAS-маркировкой и не должна притворяться ею.

## Маркировка и минимальность

Ни одна из оставляемых KIAS-вставок в native C#/YAML не имеет требуемой пары `#Forge-change START (KIAS): ...` / `#Forge-change END (KIAS)`. В lathe.yml есть старые Forge-комментарии о других изменениях — они не маркируют новые recipes. Для отдельных KIAS FTL, карт, BAT и документации такие блоки не нужны. Для independent fixes нужен собственный контекст, а не ложная надпись KIAS. Предлагаемые конкретные блоки находятся в 05_MINIMAL_PATCH_PLAN.md.

До удаления независимых изменений нужно учитывать ancestry: Station AI/radio изменения уже присутствуют в родительской истории KIAS, но не входили в проверенный upstream/main. Это не новые функции KIAS и не доказательство, что upstream уже их принял. Нельзя исправлять поверхность PR слепым revert файлов, теряя другой согласованный патч: отделить поставку/основание будущего PR.

## Количественный итог

'''
report+=f"- Внешних файлов по буквальному пути: **{len(external)}**; текстовых изменённых строк (+ и −): **{lines(external)}**; бинарных файлов без строкового размера: **{summary['binaryFiles']}**. Gitlink учтён как 1+1, внутренние 90 engine-строк отдельно и не добавлены второй раз.\n"
report+=f"- Файлов с изменением существующего C#/csproj или чужих прототипов: **{len(native)}**. Для сохранения текущих возможностей обосновано оставить узкую внешнюю поверхность в **{len(retained)}** из них (KEEP/MINIMIZE, сейчас {lines(retained)} изменённых строк). Это число файлов, не утверждение о необходимости каждой текущей строки.\n"
report+=f"- Потенциально исключаются из внешней KIAS-поставки/переносятся/заменяются API: **{len(eliminable)}** файлов, сейчас **{lines(eliminable)}** текстовых строк. Это оценка поверхности, не готовый безопасный deletion patch; включает документацию, карту, отдельные исправления и бинарные источники.\n"
report+=f"- Обязательные KIAS-блоки отсутствуют в **{summary['missingKiasMarkers']}** оставляемых native файлах.\n"
report+='- Решения по всем 122 строкам: '+', '.join(f'{k} — {v}' for k,v in sorted(counts.items()))+'.\n'
report+='- Объективно необходимого изменения движка для KIAS не обнаружено: **0**. Оба engine файла и gitlink следует рассматривать вне KIAS PR.\n'
(OUT/'03_EXTERNAL_CHANGES_AUDIT.md').write_text(report,encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False,indent=2))
