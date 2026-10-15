import csv
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'KIAS_REVIEW'

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT).decode('utf-8')

base = git('merge-base', 'HEAD', 'upstream/main').strip()
rows = []
for line in git('diff', '--numstat', base, 'HEAD').splitlines():
    added, deleted, name = line.split('\t', 2)
    path = ROOT / name
    category = ('production' if name.startswith(('Content.Server/_Forge/KIAS/', 'Content.Shared/_Forge/KIAS/', 'Content.Client/_Forge/KIAS/'))
                else 'tests' if '/Tests/_Forge/KIAS/' in name
                else 'benchmark' if name.startswith('Tools/KiasBenchmark/')
                else 'resources' if '/_Forge/KIAS/' in name
                else 'documentation' if name.startswith('Docs/KIAS/')
                else 'external')
    rows.append(dict(file=name, category=category, added=added, deleted=deleted, status='NOT_READ', notes=''))
with (OUT / 'inventory.csv').open('w', newline='', encoding='utf-8-sig') as f:
    writer = csv.DictWriter(f, fieldnames=rows[0].keys())
    writer.writeheader()
    writer.writerows(rows)
(OUT / 'repo_snapshot.json').write_text(json.dumps({
    'head':git('rev-parse','HEAD').strip(), 'branch':git('branch','--show-current').strip(),
    'upstream_main':git('rev-parse','upstream/main').strip(), 'origin_main':git('rev-parse','origin/main').strip(),
    'merge_base':base, 'submodule':git('submodule','status').strip(), 'files':len(rows),
    'categories':{c:sum(r['category']==c for r in rows) for c in sorted({r['category'] for r in rows})},
}, indent=2), encoding='utf-8')
(OUT / 'external.diff').write_text(git('diff',base,'HEAD','--',*[r['file'] for r in rows if r['category']=='external']),encoding='utf-8')
(OUT / 'engine.diff').write_text(subprocess.check_output(['git','-C',str(ROOT/'RobustToolbox'),'diff','96c6bddcebab253182a0e0eeec8e39d6a87e8264','c2fc1375fc8647f4a29d2e9c8a8ae86dfb35d66e']).decode('utf-8'),encoding='utf-8')
print((OUT/'repo_snapshot.json').read_text())
