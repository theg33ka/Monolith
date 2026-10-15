import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parent.parent
out=root/'KIAS_REVIEW'
base='3226889714d52cad147566c623dd7f18debd21ed'
def git(*args,cwd=root):
    return subprocess.check_output(['git',*args],cwd=cwd).decode('utf8').strip()
reports=sorted(out.glob('0[1-7]_*.md'))
assert len(reports)==7
links=0
for report in reports:
    content=report.read_text(encoding='utf8')
    assert content.count('```')%2==0
    for target in re.findall(r'\]\(<?([A-Za-z]:/[^\n>]+?)>?\)',content):
        match=re.match(r'^(.*?)(?::(\d+))?$',target)
        path=Path(match[1]);assert path.exists(),target
        if match[2]:assert 1<=int(match[2])<=len(path.read_text(encoding='utf-8-sig').splitlines()),target
        links+=1
independent=json.loads((out/'independent-fixes-excluded.json').read_text(encoding='utf8'))['files']
assert len(independent)==11
assert not git('diff',base,'--',*independent)
assert not git('diff',base,'--','RobustToolbox','README.md','Content.Server/Weapons/Ranged/Systems/GunSystem.cs','Content.Shared/Damage/Systems/DamageableSystem.cs','Content.Server/_Mono/Shuttles/Systems/ShuttleConsoleSystem.Autopilot.cs')
assert git('rev-parse','HEAD',cwd=root/'RobustToolbox')=='96c6bddcebab253182a0e0eeec8e39d6a87e8264'
assert not git('status','--porcelain',cwd=root/'RobustToolbox')
assert not git('diff','--cached','--binary')
assert git('rev-parse','HEAD')=='5635bd2c4d1d5ba183700a535f07cfa340cd98ad'
archive=root/'.kias-benchmark/rerun/20261014-023045-ca39336a/KIAS_web-agent.zip'
with archive.open('rb') as stream: sha=hashlib.file_digest(stream,'sha256').hexdigest()
assert sha=='4f6723d74055899672b024bb0c100ef8a7c5e63552468c51a5def2c436c4cca9'
markers=json.loads((out/'native-markers-applied.json').read_text(encoding='utf8'))
assert len(markers)==33
for row in markers:
    text=(root/row['file']).read_text(encoding='utf-8-sig')
    assert text.count('#Forge-change START (KIAS)')==text.count('#Forge-change END (KIAS)'),row
    assert text.count('#Forge-change START (KIAS)')==row['blocks'],row
locale=json.loads((out/'fixed-locale-validation.json').read_text(encoding='utf8'))
assert not locale['enOnly'] and not locale['ruOnly']
assert all(not locale[lang]['duplicates'] and locale[lang]['count']==866 for lang in ('en-US','ru-RU'))
trx={}
for name,expected in [('kias-standalone-release-final.trx',188),('kias-standalone-debug-serial.trx',188)]:
    counters=ET.parse(out/'fix-test-results'/name).find('.//{*}Counters').attrib
    assert int(counters['passed'])==expected and int(counters['failed'])==0 and int(counters.get('notExecuted',0))==0,(name,counters)
    trx[name]=counters
package=json.loads((out/'standalone-delivery.json').read_text(encoding='utf8'))
assert package['applyCheckPassed'] and package['realIndexUnchanged'] and package['externalFiles']==40 and package['recoveryPatchesApplyCheckPassed']
assert package['commonContentTree']==git('rev-parse',base+'^{tree}')
result=dict(reports=len(reports),localLinksChecked=links,independentFilesExcluded=11,engineUnmodified=True,nativeFilesMarked=33,externalFiles=40,locales=866,trx=trx,originalZipSha256=sha,realIndexEmpty=True)
(out/'fix-verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(result,ensure_ascii=False,indent=2))
