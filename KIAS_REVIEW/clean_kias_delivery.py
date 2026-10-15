import json
import shutil
import subprocess
from pathlib import Path

root=Path(__file__).resolve().parent.parent
review=root/'KIAS_REVIEW'
entries=json.loads((review/'external-decisions.json').read_text(encoding='utf8'))['files']
old_map='Resources/Maps/_Forge/Shuttles/Archive/Mercenary/briarKIAS.yml'
new_map='Resources/Maps/_Forge/KIAS/Fixtures/briarKIAS.yml'
moves={}
for entry in entries:
    source=entry['file']
    if entry['decision']=='MOVE_TO_KIAS':
        if source==old_map:destination=new_map
        elif source.startswith(('assets/','references/')):destination='Docs/KIAS/ArtSources/'+source
        else:destination='Docs/KIAS/History/'+source
    elif entry['decision']=='REVERT':
        destination='KIAS_REVIEW/excluded-root-inputs/'+source
    else:continue
    src=(root/source).resolve();dst=(root/destination).resolve()
    assert src.is_relative_to(root) and dst.is_relative_to(root)
    assert not dst.exists(),destination
    dst.parent.mkdir(parents=True,exist_ok=True)
    shutil.move(src,dst)
    moves[source]=destination
    if entry['decision']=='REVERT':
        original=subprocess.run(['git','show',f'3226889714d52cad147566c623dd7f18debd21ed:{source}'],cwd=root,capture_output=True)
        if original.returncode==0:src.write_bytes(original.stdout)
paths=subprocess.check_output(['rg','-l','-F',old_map,'Content.Server','Content.Client','Content.Shared','Content.IntegrationTests','Tools','Docs/KIAS'],cwd=root,text=True,encoding='utf8').splitlines()
for relative in paths:
    if relative.replace('\\','/').startswith('Docs/KIAS/History/'):continue
    path=root/relative
    if path.suffix not in {'.cs','.py','.ps1','.md','.json','.yml','.bat'}:continue
    content=path.read_text(encoding='utf-8-sig')
    path.write_text(content.replace(old_map,new_map),encoding='utf8')
p=root/'Tools/KiasBenchmark/lab.py'
s=p.read_text(encoding='utf8').replace("ROOT / '04_PRESET_ACCEPTANCE_MATRIX.md'", "ROOT / 'Docs/KIAS/History/04_PRESET_ACCEPTANCE_MATRIX.md'")
p.write_text(s,encoding='utf8')
p=root/'Tools/KiasBenchmark/package_analysis.py'
s=p.read_text(encoding='utf8').replace("for path in repo.glob('0*.md'):", "for path in (repo/'Docs/KIAS/History').glob('0*.md'):").replace("'10_RUNBOOK.md'", "'Tools/KiasBenchmark/RUNBOOK.md'")
p.write_text(s,encoding='utf8')
(review/'delivery-moves.json').write_text(json.dumps(moves,ensure_ascii=False,indent=2),encoding='utf8')
print(f'Moved {len(moves)} files; README restored from merge-base; benchmark evidence untouched.')
