import json
import re
import subprocess
from pathlib import Path

root=Path(__file__).resolve().parent.parent
old='Maps/_Forge/Shuttles/Archive/Mercenary/briarKIAS.yml'
new='Maps/_Forge/KIAS/Fixtures/briarKIAS.yml'
files=subprocess.check_output(['rg','-l','-F',old,'Content.IntegrationTests','Content.Server','Content.Client','Tools'],cwd=root,text=True,encoding='utf8').splitlines()
for relative in files:
    p=root/relative
    if p.suffix in {'.cs','.py','.ps1','.md','.json','.yml','.bat'}:
        p.write_text(p.read_text(encoding='utf-8-sig').replace(old,new),encoding='utf8')
p=root/'README.md'
p.write_text('\n'.join(line.rstrip() for line in p.read_text(encoding='utf-8-sig').splitlines()).rstrip()+'\n',encoding='utf8')

reasons={
'Content.Client.csproj':'Диагностические команды компилируются только с явными лабораторными флагами.',
'Content.Server.csproj':'Серверные UI fixtures включаются сборкой только по лабораторному флагу.',
'ShuttleNavControl.xaml.cs':'Наследник рисует покрытие через штатную матрицу радара без копии его реализации.',
'FireControlWindow.xaml.cs':'Штатное окно отображает переданную сервером блокировку автоматического управления.',
'AmeControllerSystem.cs':'Адаптеру нужен сигнал изменения впрыска, которым владеет штатная система AME.',
'AtmosphereSystem.RoomSafety.cs':'Ограниченный запрос читает внутренние atmospheric tiles без раскрытия коллекции.',
'AtmosAlarmableSystem.cs':'Адаптер получает фактическое состояние после изменения штатной атмосферной тревоги.',
'DeviceLinkSystem.cs':'Источник и приёмник передают адаптеру фактический порт; доставка остаётся штатной.',
'NetworkConfiguratorSystem.cs':'Только KIAS сохраняет выбранный Link/List режим при совмещённых штатных компонентах.',
'PowerMonitoringConsoleSystem.cs':'Штатный монитор пропускает отдельный тип DATA, не являющийся силовой сетью.',
'CableNode.cs':'Силовое реле разрывает штатную связность узла; DATA-топология остаётся внутри KIAS.',
'JammerSystem.cs':'Адаптер меняет jammer через штатный setter с прежними ограничениями устройства.',
'ShuttleConsoleLockSystem.cs':'Адаптер использует ту же проверку deed, что и штатная консоль шаттла.',
'ShuttleSystem.Impact.cs':'Адаптер получает принятый нативной физикой удар без повторения collision solver.',
'FireControlSystem.Console.cs':'Штатная консоль передаёт состояние автоматики и согласует ручное управление.',
'FireControlSystem.cs':'Проверка автоматики выполняется до нативного резервирования цели и выстрела.',
'ShuttleConsoleSystem.Autopilot.cs':'Адаптер получает завершение штатного автопилота без отдельного polling цикла.',
'AnomalyComponent.cs':'Адаптер отслеживает изменение growth, которым владеет штатная система аномалии.',
'SharedAnomalySystem.cs':'Событие передаёт старый и новый growth после штатного изменения аномалии.',
'DeviceLinkPortInvokedEvent.cs':'Штатный DeviceLink публикует фактически вызванный порт для адаптеров.',
'SignalReceivedEvent.cs':'Получатель знает порт источника без дублирования маршрутизации DeviceLink.',
'SharedPower.cs':'DATA получает отдельный enum; значения существующих силовых типов сохраняются.',
'SharedProjectileSystem.cs':'Адаптер получает результат после нативного damage, а не до попадания.',
'SharedShuttleSystem.IFF.cs':'Штатная IFF-метка учитывает Mayday и синхронизируется через Dirty.',
'SharedGunSystem.cs':'Штатный CancelShots согласованно сбрасывает очередь и синхронизирует оружие.',
'CompanyAccessReaderSystem.cs':'Адаптер использует штатную проверку company без копии правил доступа.',
'FireControlMessages.cs':'Штатное BUI передаёт состояние автоматики без отдельного окна управления.',
'ShipAccessReaderSystem.cs':'Адаптер использует штатную проверку ship deed без копии правил доступа.',
'lathe.yml':'Рецепты KIAS добавляются в штатные списки производства без замены прототипов станков.',
'cables.yml':'Штатные кабели получают данные для измерения сети и силового реле.',
'walls.yml':'Штатный корпус предоставляет адаптеру маркер контроля повреждений.',
'window.yml':'Штатное окно предоставляет адаптеру маркер контроля повреждений.',
'flarelauncher.yml':'Штатная пусковая установка предоставляет адаптеру разрешённые порты управления.',
'standardlight_kinetic.yml':'Штатное орудие предоставляет адаптеру разрешённые порты управления.',
}
entries=json.loads((root/'KIAS_REVIEW/external-decisions.json').read_text(encoding='utf8'))['files']
marked=[]
for entry in entries:
    if not entry['native'] or entry['decision'] not in {'KEEP','MINIMIZE'}:continue
    p=root/entry['file'];text=p.read_text(encoding='utf-8-sig')
    assert '#Forge-change START (KIAS):' not in text, p
    diff=subprocess.check_output(['git','diff','--unified=0','3226889714d52cad147566c623dd7f18debd21ed','--',entry['file']],cwd=root,text=True,encoding='utf8')
    lines=text.splitlines(keepends=True);ranges=[]
    for start,count in re.findall(r'^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@',diff,re.M):
        start=int(start);count=int(count) if count else 1
        left=max(0,start-1);right=min(len(lines),left+max(1,count))
        if ranges and left<=ranges[-1][1]:ranges[-1]=(ranges[-1][0],max(right,ranges[-1][1]))
        else:ranges.append((left,right))
    reason=reasons[p.name]
    for left,right in reversed(ranges):
        indent=re.match(r'\s*',lines[left]).group().replace('\n','') if left<len(lines) else ''
        if p.suffix=='.csproj':
            opening=f'{indent}<!-- #Forge-change START (KIAS): {reason} -->\n';closing=f'{indent}<!-- #Forge-change END (KIAS) -->\n'
        else:
            prefix='//' if p.suffix=='.cs' else '#'
            opening=f'{indent}{prefix} #Forge-change START (KIAS): {reason}\n';closing=f'{indent}{prefix} #Forge-change END (KIAS)\n'
        lines[right:right]=[closing];lines[left:left]=[opening]
    p.write_text(''.join(lines),encoding='utf8')
    marked.append(dict(file=entry['file'],blocks=len(ranges)))
(root/'KIAS_REVIEW/native-markers-applied.json').write_text(json.dumps(marked,ensure_ascii=False,indent=2),encoding='utf8')
print(f'Updated fixture references; marked {len(marked)} native files.')
