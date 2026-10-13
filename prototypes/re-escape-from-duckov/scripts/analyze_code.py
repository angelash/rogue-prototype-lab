"""Full managed metadata, C# and IL; target assemblies are never executed."""
import argparse, json, subprocess, time
from pathlib import Path
from prepare import write, sha

GAME = {'TeamSoda.Duckov.Core', 'TeamSoda.Duckov.Utilities', 'TeamSoda.MiniLocalizor',
        'Assembly-CSharp', 'ItemStatsSystem', 'SodaLocalization', 'PackageTools',
        'Plugins', 'FlexReader', 'Bilibili.BDS'}

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--project',required=True)
    ap.add_argument('--ilspy',required=True); ap.add_argument('--metadata',required=True)
    args=ap.parse_args(); p=Path(args.project).resolve(); local=p/'local'
    managed=local/'build-copy/Duckov_Data/Managed'; status_path=p/'evidence/assemblies.json'
    prior={x['name']:x for x in json.loads(status_path.read_text('utf-8'))} if status_path.exists() else {}
    files=sorted(managed.glob('*.dll'), key=lambda x:(x.stem not in GAME, x.stem!='TeamSoda.Duckov.Core', x.stem))
    status=[]; index=[]
    for i,dll in enumerate(files):
        name=dll.stem; digest=sha(dll)
        out=local/'decompiled'/name; meta=local/'code-metadata'/f'{name}.json'; il=local/'il'/name
        rec=dict(name=name,sha256=digest,bytes=dll.stat().st_size,category='game-or-integration' if name in GAME else 'engine-or-middleware')
        for stage, cmd in [
            ('metadata',['dotnet',args.metadata,str(dll),str(meta)]),
            ('csharp',[args.ilspy,'--disable-updatecheck','-p','-o',str(out),'-r',str(managed),str(dll)]),
            ('il',[args.ilspy,'--disable-updatecheck','-il','-o',str(il),str(dll)])]:
            log=local/'logs/code'/f'{name}-{stage}.log';log.parent.mkdir(parents=True,exist_ok=True)
            if prior.get(name,{}).get('sha256')==digest and prior[name].get(stage)=='ok':
                rec[stage]='ok';continue
            meta.parent.mkdir(parents=True,exist_ok=True)
            start=time.time()
            with log.open('w',encoding='utf-8') as f:
                result=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT)
            rec[stage]='ok' if result.returncode==0 else 'failed'
            rec[stage+'Exit']=result.returncode; rec[stage+'Seconds']=round(time.time()-start,2)
        if meta.exists():
            m=json.loads(meta.read_text('utf-8-sig'))
            rec.update({k:m[k] for k in ('typeCount','methodCount','fieldCount','version','machine')})
            rec['ilFailures']=len(m['failures']);rec['references']=m['references']
            for t in m['types']:
                index.append(dict(assembly=name,name=t['name'],baseType=t['baseType'],token=t['token'],
                                  methods=len(t['methods']),fields=len(t['fields'])))
        rec['sourceFiles']=len(list(out.rglob('*.cs'))) if out.exists() else 0
        rec['decompilerErrors']=[]
        if out.exists():
            for f in out.rglob('*.cs'):
                if '/* Error: ' in f.read_text('utf-8',errors='replace'):
                    rec['decompilerErrors'].append(f.relative_to(local).as_posix())
        status.append(rec);write(status_path,status)
        if name in GAME or (i+1)%10==0:print(f'Code {i+1}/{len(files)}: {name}, C#={rec["csharp"]}, IL={rec["il"]}',flush=True)
    write(local/'code-index.json',index)
    write(p/'evidence/code-summary.json',dict(assemblies=len(status),
          gameAssemblies=[r for r in status if r['category']=='game-or-integration'],
          types=sum(r.get('typeCount',0) for r in status),methods=sum(r.get('methodCount',0) for r in status),
          fields=sum(r.get('fieldCount',0) for r in status),
          failures=[r['name'] for r in status if any(r.get(s)!='ok' for s in ('metadata','csharp','il'))],
          decompilerErrorFiles=sum(len(r['decompilerErrors']) for r in status),
          ilFailures=sum(r.get('ilFailures',0) for r in status)))
    write(p/'evidence/game-code-index.json',[r for r in index if r['assembly'] in GAME])
    print(f'Completed all {len(status)} assemblies',flush=True)

if __name__=='__main__':main()
