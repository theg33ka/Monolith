import csv
import re
import subprocess
import zipfile
from pathlib import Path

root=Path(__file__).resolve().parent.parent
out=root/'KIAS_REVIEW'
snapshot=out/'baseline-source'
rows=list(csv.DictReader((out/'inventory.csv').open(encoding='utf-8-sig')))
paths=['Content.Server/_Forge/KIAS','Content.Shared/_Forge/KIAS','Content.Client/_Forge/KIAS',
       'Content.IntegrationTests/Tests/_Forge/KIAS','Tools/KiasBenchmark','Docs/KIAS',
       'Resources/Prototypes/_Forge/KIAS','Resources/Textures/_Forge/KIAS',
       'Resources/Locale/ru-RU/_Forge/KIAS','Resources/Locale/en-US/_Forge/KIAS']
paths += [r['file'] for r in rows if r['category']=='external']
archive=out/'baseline-source.zip'
assert not archive.exists() and not snapshot.exists()
subprocess.run(['git','archive','--format=zip','--output='+str(archive),'5635bd2c4d1d5ba183700a535f07cfa340cd98ad','--',*paths],cwd=root,check=True)
with zipfile.ZipFile(archive) as data:
    for name in data.namelist():
        assert (snapshot/name).resolve().is_relative_to(snapshot.resolve())
    data.extractall(snapshot)
files={r['file'] for r in rows}
prefix=root.as_posix()+'/'
for report in sorted(out.glob('0[1-6]_*.md')):
    content=report.read_text(encoding='utf8')
    def replace(match):
        target=match.group(1)
        relative=target[len(prefix):]
        file=re.sub(r':\d+$','',relative)
        if file in files and file!='RobustToolbox':
            target=snapshot.as_posix()+'/'+relative
        return '](<'+target+'>)'
    content=re.sub(r'\]\(<?('+re.escape(prefix)+r'[^\n>]+?)>?\)',replace,content)
    notice='> Исторический аудит исходного HEAD `5635bd2c`: ссылки на проверявшиеся исходники ведут в неизменяемый `baseline-source/`. Текущие исправления и результаты проверок описаны в [07_FIXES_AND_VALIDATION.md](<'+(out/'07_FIXES_AND_VALIDATION.md').as_posix()+'>).\n\n'
    split=content.find('\n')+1
    content=content[:split]+'\n'+notice+content[split:].lstrip('\n')
    report.write_text(content,encoding='utf8')
print('Preserved original reviewed sources and repaired historical review links.')
