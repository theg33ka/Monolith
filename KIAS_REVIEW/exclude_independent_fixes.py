import json
import subprocess
from pathlib import Path

root=Path(__file__).resolve().parent.parent
out=root/'KIAS_REVIEW'
patch=out/'independent-content-fixes.patch'
assert patch.exists() and patch.stat().st_size>1000
entries=json.loads((out/'external-decisions.json').read_text(encoding='utf8'))['files']
paths=[r['file'] for r in entries if r['decision']=='INDEPENDENT_UPSTREAM_FIX' and r['file']!='RobustToolbox']
expected=subprocess.check_output(['git','diff','--binary','3226889714d52cad147566c623dd7f18debd21ed','HEAD','--',*paths],cwd=root)
assert patch.read_bytes()==expected
for relative in paths:
    baseline=subprocess.run(['git','show',f'3226889714d52cad147566c623dd7f18debd21ed:{relative}'],cwd=root,capture_output=True)
    if baseline.returncode == 0:
        (root/relative).write_bytes(baseline.stdout)
    else:
        assert not subprocess.check_output(['git','ls-tree','3226889714d52cad147566c623dd7f18debd21ed','--',relative],cwd=root)
        (root/relative).unlink(missing_ok=True)
assert not subprocess.check_output(['git','diff','3226889714d52cad147566c623dd7f18debd21ed','--',*paths],cwd=root)
(out/'independent-fixes-excluded.json').write_text(json.dumps(dict(files=paths,patch=str(patch),baseline='3226889714d52cad147566c623dd7f18debd21ed',restoredExactly=True),ensure_ascii=False,indent=2),encoding='utf8')
print('Restored all 11 content files exactly to merge-base; recoverable patch retained.')
