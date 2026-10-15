import json
import os
import subprocess
from pathlib import Path

root=Path(__file__).resolve().parent.parent
out=root/'KIAS_REVIEW'
base='3226889714d52cad147566c623dd7f18debd21ed'
entries=json.loads((out/'external-decisions.json').read_text(encoding='utf8'))['files']
independent=[r['file'] for r in entries if r['decision']=='INDEPENDENT_UPSTREAM_FIX' and r['file']!='RobustToolbox']
def git(*args,env=None,cwd=root):
    return subprocess.check_output(['git',*args],cwd=cwd,env=env)
original_index=git('diff','--cached','--binary')
common=out/'independent-content-fixes.patch'
common.write_bytes(git('diff','--binary',base,'HEAD','--',*independent))
(out/'independent-engine-fix.patch').write_bytes(git('format-patch','-1','c2fc1375fc8647f4a29d2e9c8a8ae86dfb35d66e','--stdout',cwd=root/'RobustToolbox'))
index=out/'standalone-validation.index'
index.unlink(missing_ok=True)
env=os.environ.copy();env['GIT_INDEX_FILE']=str(index)
subprocess.run(['git','read-tree',base],cwd=root,env=env,check=True)
subprocess.run(['git','apply','--cached','--check',str(common)],cwd=root,env=env,check=True)
subprocess.run(['git','apply','--check',str(out/'independent-engine-fix.patch')],cwd=root/'RobustToolbox',check=True)
common_tree=git('write-tree',env=env).decode().strip()
patch=git('diff','--binary',common_tree)
untracked=git('ls-files','--others','--exclude-standard','-z').decode('utf8').split('\0')
prefixes=('Content.Server/_Forge/KIAS/','Content.Shared/_Forge/KIAS/','Content.Client/_Forge/KIAS/',
          'Content.IntegrationTests/Tests/_Forge/KIAS/','Tools/KiasBenchmark/','Docs/KIAS/',
          'Resources/Maps/_Forge/KIAS/','Resources/Prototypes/_Forge/KIAS/','Resources/Textures/_Forge/KIAS/')
included=[]
for relative in untracked:
    if not relative.startswith(prefixes) or not (root/relative).is_file():continue
    diff=subprocess.run(['git','diff','--no-index','--binary','--','/dev/null',relative],cwd=root,capture_output=True)
    assert diff.returncode in (0,1),diff.stderr.decode('utf8','replace')
    patch+=diff.stdout;included.append(relative)
standalone=out/'standalone-kias.patch';standalone.write_bytes(patch)
subprocess.run(['git','apply','--cached','--check',str(standalone)],cwd=root,env=env,check=True)
subprocess.run(['git','apply','--cached',str(standalone)],cwd=root,env=env,check=True)
result_tree=git('write-tree',env=env).decode().strip()
files=[]
for line in git('diff','--numstat',common_tree,result_tree).decode('utf8').splitlines():
    added,deleted,relative=line.split('\t',2)
    owned=relative.startswith(prefixes) or '/_Forge/KIAS/' in relative
    files.append(dict(file=relative,added=added,deleted=deleted,external=not owned))
external=[r for r in files if r['external']]
assert len(external)==40,external
assert 'RobustToolbox' not in {r['file'] for r in files}
assert not set(independent)&{r['file'] for r in files}
assert git('diff','--cached','--binary')==original_index
result=dict(base=base,commonContentFiles=independent,commonContentTree=common_tree,standaloneResultTree=result_tree,
            files=len(files),externalFiles=len(external),externalChangedLines=sum(int(r['added'])+int(r['deleted']) for r in external if r['added']!='-'),
            newFilesIncluded=included,applyCheckPassed=True,realIndexUnchanged=True,enginePatchRequiredForKias=False,recoveryPatchesApplyCheckPassed=True,
            note='Standalone KIAS patch applies directly to merge-base; independent engine fix is excluded. No commits, branches or real-index staging were created.',
            external=external)
(out/'standalone-delivery.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({k:v for k,v in result.items() if k not in ('external','newFilesIncluded','commonContentFiles')},ensure_ascii=False,indent=2))
