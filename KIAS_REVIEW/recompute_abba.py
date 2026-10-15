import csv,io,json,zipfile,pathlib,statistics,math,hashlib,subprocess
root=pathlib.Path(__file__).resolve().parent.parent
out=root/'KIAS_REVIEW'
archive=root/'.kias-benchmark/rerun/20261014-023045-ca39336a/KIAS_web-agent.zip'
def stats(v):
    s=sorted(v)
    def q(p):
        x=(len(s)-1)*p;i=int(x);return s[i]+(s[min(i+1,len(s)-1)]-s[i])*(x-i)
    return dict(n=len(v),mean=statistics.mean(v),p50=statistics.median(v),p95=q(.95),p99=q(.99),p999=q(.999),max=max(v),over1667=sum(x>1000/60 for x in v))
report={'archive':str(archive),'archiveSha256':hashlib.file_digest(archive.open('rb'),'sha256').hexdigest(),'runs':{},'pooled':{}}
all_rows={'A':[],'B':[]}
with zipfile.ZipFile(archive) as z:
    for name in ['1-A','2-B','3-B','4-A']:
        prefix=f'runs/{name}/'
        rows=list(csv.DictReader(io.StringIO(z.read(prefix+'ticks.csv').decode('utf-8-sig'))))
        assert len(rows)==108000
        assert [int(r['relative_tick']) for r in rows]==list(range(108000))
        assert all(int(rows[i]['engine_tick'])==int(rows[0]['engine_tick'])+i for i in range(len(rows)))
        all_rows[name[-1]].extend(rows)
        run={key:stats([float(r[key]) for r in rows]) for key in ['whole_tick_ms','content_engine_ms','physics_ms','driver_ms','observer_ms','working_set_bytes','whole_allocated_bytes']}
        run['gcCollections']={k:int(rows[-1][k])-int(rows[0][k]) for k in ['gc0','gc1','gc2']}
        run['gcPauseMs']=sum(float(r['gc_pause_ms']) for r in rows)
        run['manifest']=json.loads(z.read(prefix+'run-manifest.json'))
        run['completed']=json.loads(z.read(prefix+'completed.json'))
        if name.endswith('B'):
            d=json.loads(z.read(prefix+'final-telemetry.json'))
            run['telemetry']={k:v for k,v in d.items() if k in ['activeGrids','runningCards','activeMachines','faultedCards','queues']}
            for key in ['queueLatency','actuatorDispatchLatency','phases']:
                run[key]=[{k:({kk:vv for kk,vv in v.items() if kk!='buckets'} if isinstance(v,dict) else v) for k,v in row.items() if k!='buckets'} for row in d[key]]
            run['acceptedCommandLatency']={k:v for k,v in d['acceptedCommandLatency'].items() if k!='buckets'}
        report['runs'][name]=run
    for variant,rows in all_rows.items():
        report['pooled'][variant]={key:stats([float(r[key]) for r in rows]) for key in ['whole_tick_ms','content_engine_ms','physics_ms','driver_ms','observer_ms','whole_allocated_bytes','working_set_bytes']}
    report['deltaMean']={key:report['pooled']['B'][key]['mean']-report['pooled']['A'][key]['mean'] for key in report['pooled']['A']}
    report['deltaMean']['residual_ms']=report['deltaMean']['whole_tick_ms']-sum(report['deltaMean'][k] for k in ['content_engine_ms','driver_ms','observer_ms'])
    for name in ['pair/BASELINE_MANIFEST.json','series/results.json','series/status.json']:
        d=json.loads(z.read(name));report[name]={k:v for k,v in d.items() if k not in ['builds','runs','attemptedRuns','completedRuns']}
    fingerprint=json.loads(z.read('pair/source-fingerprint.json'))
    report['fingerprintMismatches']=[p for p,h in fingerprint['files'].items() if not (root/p).exists() or hashlib.sha256((root/p).read_bytes()).hexdigest()!=h]
(out/'ABBA_RECOMPUTED.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
for n,r in report['runs'].items():print(n,r['whole_tick_ms'],r['gcCollections'])
print('POOLED',report['pooled'])
print('DELTA',report['deltaMean'])
print('CHANGED_FINGERPRINT_FILES',len(report['fingerprintMismatches']))
