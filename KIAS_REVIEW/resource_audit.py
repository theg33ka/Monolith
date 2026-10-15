from pathlib import Path
import yaml,json,csv,re
from collections import Counter
class Loader(yaml.SafeLoader): pass
Loader.yaml_implicit_resolvers={k:[x for x in v if x[0]!='tag:yaml.org,2002:bool'] for k,v in Loader.yaml_implicit_resolvers.items()}
Loader.add_implicit_resolver('tag:yaml.org,2002:bool',re.compile(r'^(?:true|false|True|False|TRUE|FALSE)$'),list('tTfF'))
def unknown(loader,tag,node):
 if isinstance(node,yaml.MappingNode): return loader.construct_mapping(node)
 if isinstance(node,yaml.SequenceNode): return loader.construct_sequence(node)
 return loader.construct_scalar(node)
Loader.add_multi_constructor('',unknown)
root=Path('.'); out=Path('KIAS_REVIEW')
files=list(Path('Resources/Prototypes/_Forge/KIAS').glob('*.yml'))
all=[]
for f in files:
 data=yaml.load(f.read_text(encoding='utf-8'),Loader=Loader)
 all += [(str(f).replace('\\','/'),d) for d in data]
with (out/'resource-structure.txt').open('w',encoding='utf-8') as o:
 for f,d in all:
  if d['type']=='kiasControllerProgram': continue
  o.write(f"{f} {d.get('type')} {d.get('id')} parent={d.get('parent')} abstract={d.get('abstract',False)}\n")
  o.write(json.dumps({k:v for k,v in d.items() if k not in ['id','type','parent','abstract']},ensure_ascii=False,separators=(',',':'))+'\n')
with (out/'preset-structure.txt').open('w',encoding='utf-8') as o:
 for f,d in all:
  if d['type']!='kiasControllerProgram': continue
  p=d['program']; nodes={n['id']:n for n in p['nodes']}
  o.write(f"\n{d['id']} enabled={d['enabled']} nodes={len(nodes)} wires={len(p['wires'])}\n")
  for n in p['nodes']: o.write(f"{n['id']} {n['kind']} {n.get('profile','')} {json.dumps(n.get('config',{}),ensure_ascii=False)}\n")
  for w in p['wires']: o.write(f"  {w['fromNode']}.{w['fromPort']} -> {w['toNode']}.{w['toPort']}\n")
print(Counter(d['type'] for f,d in all))
print('structure generated')
