"""Prepare isolated common-base sources and prove non-KIAS map inventory parity."""
from pathlib import Path
import hashlib,json,shutil,subprocess,yaml
from lab import ROOT,OUT,MAP,MapLoader,canonical,digest

def git(*args,cwd=ROOT):
    return subprocess.run(['git',*args],cwd=cwd,check=True,capture_output=True).stdout

class Tagged(dict):
    pass
class Loader(yaml.SafeLoader):
    pass
class Dumper(yaml.SafeDumper):
    pass

def tagged(loader,suffix,node):
    value=Tagged(loader.construct_mapping(node,deep=True))
    value.tag='!type:'+suffix
    return value
Loader.add_multi_constructor('!type:',tagged)
Dumper.add_representer(Tagged,lambda dumper,value:dumper.represent_mapping(value.tag,value.items()))

def vanilla(source):
    value=yaml.load(source,Loader=Loader)
    removed={e['uid'] for g in value['entities'] if 'Kias' in g.get('proto','') for e in g['entities']}
    # Берём удалённые устройства, исключаем также их вложенные предметы.
    while True:
        children={e['uid'] for g in value['entities'] for e in g['entities']
                  if any(c['type']=='Transform' and c.get('parent') in removed for c in e.get('components',[]))}
        added=children-removed
        if not added: break
        removed.update(added)
    groups=[]
    for group in value['entities']:
        if 'Kias' in group.get('proto',''): continue
        group['entities']=[e for e in group['entities'] if e['uid'] not in removed]
        if not group['entities']: continue
        for entity in group['entities']:
            entity['components']=[c for c in entity.get('components',[]) if not c['type'].startswith('Kias')]
            for component in entity['components']:
                if component['type']=='ContainerContainer':
                    for name,container in component.get('containers',{}).items():
                        if container.get('ent') in removed: container['ent']=None
                        if 'ents' in container: container['ents']=[uid for uid in container['ents'] if uid not in removed]
        groups.append(group)
    value['entities']=groups
    value['meta']['entityCount']=sum(len(g['entities']) for g in groups)
    return value,removed

def main():
    OUT.mkdir(exist_ok=True)
    head=git('rev-parse','HEAD').decode().strip()
    base=json.loads((OUT/'fingerprint.json').read_text(encoding='utf-8'))['commonBase']
    git('merge-base','--is-ancestor',base,'HEAD')
    engine=git('rev-parse','HEAD',cwd=ROOT/'RobustToolbox').decode().strip()
    manifest={'status':'SOURCES_PREPARED_NOT_BUILT','base':base,'branchHead':head,'engine':engine,'builds':[],'runtimeParity':'NOT_TESTED'}
    bpatch=git('diff','--binary',base,'HEAD')
    (OUT/'matched-overlay.patch').write_bytes(bpatch)
    dirty=git('diff','--binary','HEAD')
    (OUT/'matched-current-fixes.patch').write_bytes(dirty)
    paths=[]
    for label in ('A','B'):
        target=OUT/'builds'/label
        if target.exists(): raise RuntimeError(f'Refusing to overwrite existing sources: {target}')
        target.parent.mkdir(exist_ok=True)
        git('worktree','add','--detach',str(target),base)
        paths.append(target)
        if label=='B':
            subprocess.run(['git','apply','--binary','-'],input=bpatch,cwd=target,check=True,capture_output=True)
            subprocess.run(['git','apply','--binary','-'],input=dirty,cwd=target,check=True,capture_output=True)
            for name in ('Content.Server/_Forge/KIAS/KiasAmeAdapterSystem.cs',):
                shutil.copy2(ROOT/name,target/name)
        lab_source=ROOT/'Tools/KiasBenchmark/Server/NativeLab.cs'
        lab_target=target/'Tools/KiasBenchmark/Server/NativeLab.cs'
        lab_target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(lab_source,lab_target)
        project=target/'Content.Server/Content.Server.csproj'
        source=project.read_text(encoding='utf-8-sig').replace('</Project>', '  <ItemGroup>\n    <Compile Include="../Tools/KiasBenchmark/Server/NativeLab.cs" Link="NativeLab.cs" />\n  </ItemGroup>\n</Project>')
        project.write_text(source,encoding='utf-8')
        entry=target/'Content.Server/Entry/EntryPoint.cs'
        source=entry.read_text(encoding='utf-8-sig')
        hook='            base.Update(level, frameEventArgs);'
        assert source.count(hook)==1
        source=source.replace(hook,'            if (level == ModUpdateLevel.PreEngine) Content.Server.Benchmark.NativeLab.PreTick();\n'+hook+'\n            if (level == ModUpdateLevel.PostEngine) Content.Server.Benchmark.NativeLab.PostTick();')
        entry.write_text(source,encoding='utf-8')
        # The unrelated audio cache fix is identical in both variants.
        audio='Content.Client/_Mono/Audio/AudioEffectSystem.cs'
        shutil.copy2(ROOT/audio,target/audio)
        engine_path=target/'RobustToolbox'
        engine_path.rmdir()
        git('worktree','add','--detach',str(engine_path),engine,cwd=ROOT/'RobustToolbox')
        modules=git('submodule','status',cwd=ROOT/'RobustToolbox').decode().splitlines()
        for line in modules:
            relative=line.strip().split()[1]
            empty=engine_path/relative
            if empty.exists(): empty.rmdir()
            command = "New-Item -ItemType Junction -Path '" + str(empty).replace("'", "''") + "' -Target '" + str(ROOT/'RobustToolbox'/relative).replace("'", "''") + "' | Out-Null"
            subprocess.run(['powershell','-NoProfile','-Command',command],check=True,capture_output=True)
        manifest['builds'].append({'label':label,'source':str(target),'base':base,'engine':engine,'compiled':False})
    amap,removed=vanilla(MAP.read_text(encoding='utf-8'))
    map_relative='Resources/Maps/_Forge/Shuttles/Archive/Mercenary/labBriar.yml'
    for target in paths: (target/map_relative).parent.mkdir(parents=True,exist_ok=True)
    (paths[0]/map_relative).write_text(yaml.dump(amap,Dumper=Dumper,allow_unicode=True,sort_keys=False),encoding='utf-8')
    shutil.copy2(MAP,paths[1]/map_relative)
    apart=yaml.load((paths[0]/map_relative).read_text(encoding='utf-8'),Loader=MapLoader)
    bpart,_=vanilla((paths[1]/map_relative).read_text(encoding='utf-8'))
    bpart=yaml.load(yaml.dump(bpart,Dumper=Dumper,allow_unicode=True,sort_keys=False),Loader=MapLoader)
    assert canonical(apart)==canonical(bpart),'Non-KIAS content parity failed'
    parity={'status':'STATIC_VANILLA_PARITY_PASS_RUNTIME_NOT_TESTED','nativeEntityCount':amap['meta']['entityCount'],'removedKiasEntities':len(removed),'nonKiasMapSha256':digest(canonical(apart)),'aMapSha256':digest((paths[0]/map_relative).read_bytes()),'bMapSha256':digest((paths[1]/map_relative).read_bytes())}
    (OUT/'static-parity.json').write_text(json.dumps(parity,indent=2),encoding='utf-8')
    manifest.update(overlaySha256=digest(bpatch),currentFixesSha256=digest(dirty),staticParity=parity)
    (OUT/'BASELINE_MANIFEST.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print(json.dumps(parity,indent=2))

if __name__=='__main__': main()
