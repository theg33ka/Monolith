import xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parent.parent
out=root/'KIAS_REVIEW'
for name in ('kias-standalone-release-final.trx','kias-standalone-debug-serial.trx'):
    c=ET.parse(out/'fix-test-results'/name).find('.//{*}Counters').attrib
    assert int(c['passed'])==188 and int(c['failed'])==0 and int(c.get('notExecuted',0))==0,(name,c)
p=out/'07_FIXES_AND_VALIDATION.md'
s=p.read_text(encoding='utf8')
s=s.replace('Окончательные прогоны автономного дерева: **ожидают завершения**. Ранее 178/178 Release и 10/10 native-контрактов прошли до исключения сторонних изменений; они не заменяют окончательную проверку.', 'Окончательные прогоны автономного дерева: **Release 188/188 PASS, Debug 188/188 PASS**, без пропусков. Каждый включает 178 тестов KIAS и 10 нативных DeviceLink-контрактов с прототипами KIAS. Артефакты: `fix-test-results/kias-standalone-release-final.trx`, `fix-test-results/kias-standalone-debug-serial.trx`; полные логи имеют те же имена. Дополнительные проверки прибытия и физического адаптера: `standalone-adapters.trx`, 2/2 PASS.')
s=s.replace('Точный состав — `standalone-delivery.json`.', 'Точный состав — `standalone-delivery.json`: 545 файлов, 40 вне каталогов KIAS, 33 нативных файла с пометками. Патчи восстановления также прошли apply --check на соответствующих базах.')
p.write_text(s,encoding='utf8')
p=root/'Docs/KIAS/PULL_REQUEST.md';s=p.read_text(encoding='utf8')
s=s.replace('Проверки и точные результаты окончательных прогонов записываются в локальный', 'Проверки: Release 188/188, Debug 188/188, Python 111/111; отдельные PowerShell проверки блокировок и логов пройдены. Интеграционные тесты запускаются последовательно из-за штатного статического аудиокэша тестовых миров. Подробности записаны в локальный')
p.write_text(s,encoding='utf8')
p=root/'Docs/KIAS/VALIDATION.md';s=p.read_text(encoding='utf8')
if '## 2026-10-15 — независимое ревью' not in s:
    title,rest=s.split('\n',1)
    s=title+'\n\n## 2026-10-15 — независимое ревью\n\nАвтономное дерево без 11 сторонних изменений и без engine patch: Release **188/188**, Debug **188/188**, Python **111/111**. Интеграционные прогоны последовательные; каждый включает 178 тестов KIAS и 10 нативных DeviceLink-контрактов. RU/EN: 866 ключей, без дублей и расхождений. Точные TRX, изменения по 12 находкам и ограничения описаны в локальном `KIAS_REVIEW/07_FIXES_AND_VALIDATION.md`. Новый A–B–B–A и ручной игровой smoke не выполнялись. Ниже сохранена история прежних проверок.\n'+rest
    p.write_text(s,encoding='utf8')
print('Final report updated from both successful TRX files.')
