"""Independent input, byte-range, reference, content and decoded-media checks."""
import argparse,collections,json,sqlite3,time,wave,zlib
from pathlib import Path
from PIL import Image
from prepare import sha,write

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);a=ap.parse_args()
    p=Path(a.project).resolve();local=p/'local';started=time.time();checks=[]
    def check(name,ok,details):
        checks.append(dict(name=name,passed=bool(ok),details=details));print(f'{name}: {ok}',flush=True)
    files=json.loads((p/'evidence/build-files.json').read_text('utf-8'));source=Path(json.loads((p/'evidence/build-summary.json').read_text('utf-8'))['source'])
    mismatch=[]
    for rec in files:
        for root in (source,local/'build-copy'):
            f=root/rec['path']
            if not f.is_file() or f.stat().st_size!=rec['bytes'] or sha(f)!=rec['sha256']:mismatch.append(str(f))
    current={f.relative_to(source).as_posix() for f in source.rglob('*') if f.is_file()}
    check('installation-and-copy-sha256',not mismatch and current=={r['path'] for r in files},dict(files=len(files),mismatches=mismatch))
    assemblies=json.loads((p/'evidence/assemblies.json').read_text('utf-8'));bad=[]
    for r in assemblies:
        m=json.loads((local/'code-metadata'/f'{r["name"]}.json').read_text('utf-8-sig'))
        if m['failures'] or len(m['types'])!=r['typeCount'] or r['metadata']!='ok' or r['il']!='ok' or not any((local/'il'/r['name']).glob('*.il')):bad.append(r['name'])
    check('all-managed-metadata-and-il',not bad,dict(assemblies=len(assemblies),failures=bad))
    db=sqlite3.connect((local/'assets.sqlite').as_uri()+'?mode=ro',uri=True)
    integrity=db.execute('PRAGMA integrity_check').fetchone()[0];check('sqlite-integrity',integrity=='ok',integrity)
    bad=[];count=0
    for name,path in db.execute('SELECT name,path FROM containers'):
        with (local/'build-copy/Duckov_Data'/path).open('rb') as f:
            for oid,size,offset,digest in db.execute('SELECT id,bytes,offset,sha256 FROM objects WHERE container=? ORDER BY offset',(name,)):
                import hashlib
                f.seek(offset);raw=f.read(size);count+=1
                if len(raw)!=size or hashlib.sha256(raw).hexdigest()!=digest:bad.append(oid)
        print(f'Byte ranges verified: {name} ({count})',flush=True)
    check('all-object-raw-byte-ranges',not bad,dict(objects=count,failures=bad))
    missing=db.execute('SELECT count(*) FROM edges e LEFT JOIN objects o ON o.id=e.dst WHERE o.id IS NULL').fetchone()[0]
    missing_src=db.execute('SELECT count(*) FROM edges e LEFT JOIN objects o ON o.id=e.src WHERE o.id IS NULL').fetchone()[0]
    check('nonzero-pptr-targets-and-sources',missing==missing_src==0,dict(edges=db.execute('SELECT count(*) FROM edges').fetchone()[0],missingTargets=missing,missingSources=missing_src))
    states=dict(db.execute('SELECT status,count(*) FROM objects GROUP BY status'));check('known-tree-limitations',states.get('ok')==1601350 and states.get('base-only')==2,states)
    catalog=json.loads((local/'content-catalog.json').read_text('utf-8'));known={r['id'] for r in catalog['items']};bad=[]
    for category in ['items','quests','recipes','buildings','merchants','sceneInfos']:
        ids=[r['id'] for r in catalog[category]]
        if len(ids)!=len(set(ids)):bad.append(category+' duplicate IDs')
    for r in catalog['items']:
        if not db.execute('SELECT 1 FROM objects WHERE id=?',(r['objectID'],)).fetchone():bad.append('Item '+str(r['id']))
        if r.get('iconObject') and not db.execute('SELECT 1 FROM objects WHERE id=?',(r['iconObject'],)).fetchone():bad.append('Icon '+str(r['id']))
    null_slots=[]
    for r in catalog['quests']:
        for kind in ('tasks','rewards'):
            for i,component in enumerate(r[kind]):
                if component is None:
                    ptr=r['data'][kind][i]
                    if ptr['m_PathID']!=0:bad.append('Unrecovered Quest '+str(r['id']))
                    null_slots.append(dict(quest=r['id'],kind=kind,index=i))
                elif component.get('status')!='ok':bad.append('Quest '+str(r['id']))
    summary=json.loads((p/'evidence/content-summary.json').read_text('utf-8'))
    check('registered-content-and-cross-references',not bad and not summary['issues'] and null_slots==[dict(quest=939,kind='rewards',index=2)],dict(categories=summary['categories'],failures=bad,recipeIssues=summary['issues'],sourceNullSlots=null_slots))
    bad=[];decoded=collections.Counter();exported=list(db.execute('SELECT path,bytes,sha256 FROM exports'))
    samples=json.loads((local/'fmod-samples.json').read_text('utf-8'))
    for rec in [dict(path=r[0],bytes=r[1],sha256=r[2]) for r in exported]+samples:
        f=local/rec['path']
        try:
            assert f.stat().st_size==rec['bytes'] and sha(f)==rec['sha256'],'size/hash'
            if f.suffix=='.png':
                with Image.open(f) as im:im.verify()
            elif f.suffix=='.wav':
                with wave.open(str(f),'rb') as w:assert w.getnframes()>0 and w.getframerate()>0
            elif f.suffix=='.obj':
                with f.open('r',encoding='utf-8') as inp:assert any(line.startswith('v ') for line in inp),'no vertices'
            decoded[f.suffix]+=1
        except Exception as ex:bad.append(dict(path=rec['path'],error=str(ex)))
    check('all-export-hashes-and-media-structure',not bad,dict(files=len(exported)+len(samples),formats=dict(decoded),failures=bad))
    rawbad=[]
    for oid,digest in db.execute('SELECT o.id,o.sha256 FROM objects o JOIN media_status s ON s.object_id=o.id WHERE s.status="raw-only"'):
        rows=db.execute('SELECT path,sha256 FROM exports WHERE object_id=?',(oid,)).fetchall()
        if len(rows)!=1 or rows[0][1]!=digest:rawbad.append(oid)
    check('raw-only-shaders-exact-payload',not rawbad,dict(objects=7,failures=rawbad))
    db.close();write(p/'evidence/verification.json',dict(date='2026-10-13',seconds=round(time.time()-started,2),passed=all(r['passed'] for r in checks),checks=checks,runtimePlaytest=False,subjectiveAudioReview=False))
    if not all(r['passed'] for r in checks):raise SystemExit(1)

if __name__=='__main__':main()
