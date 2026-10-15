import re,json
from pathlib import Path
from collections import Counter
root=Path(__file__).resolve().parent.parent
result={}
for language in ('en-US','ru-RU'):
    paths=list((root/'Resources/Locale'/language/'_Forge').glob('kias*.ftl'))
    pairs=[(p.name,k) for p in paths for k in re.findall(r'^([A-Za-z][A-Za-z0-9_-]*)\s*=',p.read_text(encoding='utf-8-sig'),re.M)]
    counts=Counter(k for _,k in pairs)
    result[language]=dict(files=[p.name for p in paths],count=len(pairs),duplicates=[k for k,v in counts.items() if v>1],keys=sorted(counts))
    assert len(pairs)>500
result['enOnly']=sorted(set(result['en-US']['keys'])-set(result['ru-RU']['keys']))
result['ruOnly']=sorted(set(result['ru-RU']['keys'])-set(result['en-US']['keys']))
(root/'KIAS_REVIEW/locale-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print({k:({kk:vv for kk,vv in v.items() if kk!='keys'} if isinstance(v,dict) else v) for k,v in result.items()})
