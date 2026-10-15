import csv,re,json,sys
from pathlib import Path
root=Path(__file__).resolve().parent.parent
out=root/'KIAS_REVIEW'
rows=list(csv.DictReader((out/'inventory.csv').open(encoding='utf-8-sig')))
tests=[]
for row in rows:
 if row['category']!='tests':continue
 p=root/row['file'];t=p.read_text(encoding='utf-8-sig')
 names=re.findall(r'public (?:async )?(?:Task|void) (\w+)\(',t)
 assertions=[s.strip() for s in t.splitlines() if re.search(r'Assert\.(That|Multiple|Throws|DoesNotThrow|Fail)',s)]
 tests.append(dict(file=row['file'],methods=names,assertions=assertions))
(out/'test-contracts.json').write_text(json.dumps(tests,ensure_ascii=False,indent=2),encoding='utf8')
for test in tests:
 print(Path(test['file']).name+': '+', '.join(test['methods']))
 print('  Assertions ('+str(len(test['assertions']))+'): '+'; '.join(test['assertions'][:2]+test['assertions'][-2:]))
sys.path.insert(0,str(root/'Tools/KiasBenchmark'))
sys.dont_write_bytecode=True
from world_smoke_scenario import generate
scenario=generate(40,'collision_rotated')
source=(root/'Tools/KiasBenchmark/Server/NativeLab.cs').read_text(encoding='utf8')
constructor=next(x.strip() for x in source.splitlines() if 'return new Input(' in x)
assert 'fixtureRotationDegrees' not in constructor
evidence=dict(requested=[r['fixtureRotationDegrees'] for r in scenario if 'fixtureRotationDegrees' in r],loaderConstructor=constructor,optionalParameterDefault='null',runtimeEffect='ExerciseCollision cannot enter requested-rotation branch for JSON inputs',verification='Static call-site/default-parameter contract; no new collision run')
(out/'rotation-input-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),encoding='utf8')
print('ROTATION',len(evidence['requested']), 'requested; first=', evidence['requested'][:4])
