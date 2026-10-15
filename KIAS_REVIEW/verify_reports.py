import csv
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

root=Path(__file__).resolve().parent.parent
out=root/'KIAS_REVIEW'
reports=sorted(out.glob('0[1-6]_*.md'))
assert len(reports)==6
errors=[];links=0
for report in reports:
    content=report.read_text(encoding='utf8')
    assert len(content)>2000,report
    assert content.count('```')%2==0,report
    for target in re.findall(r'\]\(<?([A-Za-z]:/[^\n>]+?)>?\)',content):
        links+=1
        match=re.match(r'^(.*?)(?::(\d+))?$',target)
        path=Path(match[1]);line=int(match[2]) if match[2] else None
        if not path.exists(): errors.append(f'{report.name}: missing {target}')
        elif line:
            try:
                count=len(path.read_text(encoding='utf-8-sig').splitlines())
                if not 1<=line<=count: errors.append(f'{report.name}: line {line}/{count} {target}')
            except (UnicodeDecodeError,IsADirectoryError):errors.append(f'{report.name}: not text {target}')
rows=list(csv.DictReader((out/'inventory.csv').open(encoding='utf-8-sig')))
assert len(rows)==572 and len({r['file'] for r in rows})==572
matrix=(out/'06_COVERAGE_AND_TEST_GAPS.md').read_text(encoding='utf8')
assert all(f"[{r['file']}]" in matrix for r in rows)
production=[r for r in rows if r['category']=='production']
assert len(production)==81 and all(r['status'] in {'CROSS_CHECKED','ISSUES_FOUND'} for r in production)
findings=(out/'02_FINDINGS.md').read_text(encoding='utf8')
ids=re.findall(r'^## (KIAS-\d+) — (P\d)',findings,re.M)
assert len(ids)==12 and len(dict(ids))==12
results={}
for name,expected in [('test-results/kias-review.trx',167),('evidence-results/evidence.trx',4)]:
    doc=ET.parse(out/name)
    counters=doc.find('.//{*}Counters').attrib
    assert int(counters['passed'])==expected and int(counters['failed'])==0
    results[name]=counters
def git(*args,cwd=root):
    return subprocess.check_output(['git',*args],cwd=cwd,text=True,encoding='utf8').strip()
remote=git('ls-remote','upstream','refs/heads/main')
snapshot=dict(checkedAtUtc=datetime.now(timezone.utc).isoformat(),head=git('rev-parse','HEAD'),branch=git('branch','--show-current'),upstreamMainLocal=git('rev-parse','upstream/main'),upstreamMainRemote=remote,mergeBase=git('merge-base','HEAD','upstream/main'),originMain=git('rev-parse','origin/main'),status=git('status','--short'),engine=git('rev-parse','HEAD',cwd=root/'RobustToolbox'),engineStatus=git('status','--short',cwd=root/'RobustToolbox'))
assert snapshot['head']=='5635bd2c4d1d5ba183700a535f07cfa340cd98ad'
assert snapshot['status']=='?? KIAS_REVIEW/' and not snapshot['engineStatus']
archive=root/'.kias-benchmark/rerun/20261014-023045-ca39336a/KIAS_web-agent.zip'
snapshot['benchmarkZipSha256']=hashlib.file_digest(archive.open('rb'),'sha256').hexdigest()
(out/'final-repo-snapshot.json').write_text(json.dumps(snapshot,ensure_ascii=False,indent=2),encoding='utf8')
result=dict(reports=[p.name for p in reports],localLinksChecked=links,errors=errors,inventoryFiles=len(rows),statuses=Counter(r['status'] for r in rows),findings=Counter(p for _,p in ids),trx=results,repo=snapshot)
(out/'report-verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({k:v for k,v in result.items() if k!='trx'},ensure_ascii=False,indent=2))
assert not errors,errors
