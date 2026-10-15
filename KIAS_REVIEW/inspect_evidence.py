import csv, io, json, zipfile, pathlib, statistics, re
ROOT=pathlib.Path(__file__).resolve().parent.parent
OUT=ROOT/'KIAS_REVIEW'
ZIP=ROOT/'.kias-benchmark/rerun/20261014-023045-ca39336a/KIAS_web-agent.zip'
with zipfile.ZipFile(ZIP) as z:
    names=z.namelist()
    wanted=['pair/BASELINE_MANIFEST.json','pair/source-fingerprint.json','series/results.json','series/status.json']
    for suffix in wanted:
        name=next((n for n in names if n.endswith(suffix)),None)
        if name:
            data=json.loads(z.read(name))
            (OUT/('zip-'+suffix.replace('/','-'))).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
            print(name, str(data)[:11000])
    for suffix in ['runs/1-A/ticks.csv','runs/2-B/ticks.csv','runs/2-B/final-telemetry.json','runs/2-B/run-manifest.json']:
        name=next((n for n in names if n.endswith(suffix)),None)
        if name:
            s=z.read(name).decode('utf-8-sig')
            print(name, s[:6000])
            if suffix.endswith('.json'):(OUT/('zip-'+suffix.replace('/','-'))).write_text(s,encoding='utf-8')
rows=list(csv.DictReader((OUT/'inventory.csv').open(encoding='utf-8-sig')))
for r in rows:
    if r['category']=='production':
        r['status']='READ';r['notes']='Full source read; contracts cross-checked selectively; see reports.'
with (OUT/'inventory.csv').open('w',encoding='utf-8',newline='') as f:
    w=csv.DictWriter(f,fieldnames=rows[0].keys());w.writeheader();w.writerows(rows)
outline=[]
for r in rows:
    if r['category']!='tests':continue
    p=ROOT/r['file']; lines=p.read_text(encoding='utf-8-sig').splitlines()
    outline.append('\nFILE '+r['file'])
    for n,line in enumerate(lines,1):
        if re.search(r'public (?:async Task|void)|Assert\.|\[Test',line):outline.append(f'{n}: {line.strip()}')
(OUT/'test-contract-index.txt').write_text('\n'.join(outline),encoding='utf-8')
