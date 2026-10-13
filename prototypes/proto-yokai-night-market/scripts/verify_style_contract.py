"""Compare current presentation to a frozen Git input, including every serialized field.

This is a geometry/provenance check, not a substitute for Player visual inspection.
"""
from pathlib import Path
import os, json, re, subprocess, hashlib, copy, tarfile
import yaml
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[1];REPO=ROOT.parents[1]
PROJECT=Path(os.environ.get('NIGHT_MARKET_PROJECT_PATH',ROOT/'local/UnityProject')).resolve()
BASE=os.environ.get('NIGHT_MARKET_STYLE_BASE','749b4e530a2598e2c2fb5f6852b4afbbe15d7e38')
CANONICAL='prototypes/proto-yokai-night-market/local/UnityProject/'
def read(p):return json.loads(p.read_text('utf-8'))
def sha(b):return hashlib.sha256(b).hexdigest()
_baseline_archive=None
def baseline(rel):
    global _baseline_archive
    if _baseline_archive is None:
        archive=ROOT/'local'/('style-baseline-'+BASE+'.tar')
        if not archive.exists():subprocess.run(['git','archive','--format=tar','--output='+str(archive),BASE,CANONICAL],cwd=REPO,check=True)
        _baseline_archive=tarfile.open(archive,'r:')
    return _baseline_archive.extractfile(CANONICAL+rel).read()
def docs(data):
    text=data.decode('utf-8');parts=re.split(r'(?m)^--- !u!(\d+) &(\d+)(?: stripped)?\r?\n',text)
    return {parts[i+1]:(parts[i],yaml.safe_load(parts[i+2])) for i in range(1,len(parts),3)}
def path_for(nodes,id):
    go=nodes[str(id)][1]['GameObject'];trid=next(c['component']['fileID'] for c in go['m_Component'] if nodes[str(c['component']['fileID'])][0]=='4')
    tr=nodes[str(trid)][1]['Transform'];parent=tr['m_Father']['fileID']
    return (path_for(nodes,nodes[str(parent)][1]['Transform']['m_GameObject']['fileID'])+'/' if parent else '')+str(go['m_Name'])
def near(a,b):return all(abs(a[c]-b[c])<.000002 for c in 'rgba')

def verify():
    failures=[];stats={};presentation=read(ROOT/'fidelity/data/presentation.json');surfaces=read(ROOT/'fidelity/data/ui-surfaces.json')['textures']
    files=subprocess.check_output(['git','ls-tree','-r','--name-only',BASE,'--',CANONICAL],cwd=REPO,text=True).splitlines()
    byte_locked=[f[len(CANONICAL):] for f in files if f.endswith('.meta') or '/ProjectSettings/' in f or '/Packages/' in f]
    sprites=0
    for file in files:
        rel=file[len(CANONICAL):]
        if file.endswith('.asset') and b'\nSprite:\n' in baseline(rel):byte_locked.append(rel);sprites+=1
    for rel in byte_locked:
        if not (PROJECT/rel).exists() or baseline(rel)!=(PROJECT/rel).read_bytes():failures.append('Byte-locked field changed: '+rel)
    stats.update(lockedFiles=len(byte_locked),sprites=sprites)
    semantic=list(presentation['componentColorOverrides'].values());textspec=list(presentation['textColorOverrides'].values());scene_changes=[];prefab_changes=[]
    for rel in [f[len(CANONICAL):] for f in files if f.endswith(('.unity','.prefab'))]:
        before=docs(baseline(rel));after=docs((PROJECT/rel).read_bytes());changes=[]
        if set(before)!=set(after):failures.append('Node/component identity changed: '+rel);continue
        for id,(type,data) in after.items():
            original=copy.deepcopy(before[id][1]);current=copy.deepcopy(data)
            if before[id][0]!=type:failures.append('Component type changed: '+rel+':'+id);continue
            key=next(iter(current));body=current[key];old=original[key];path=path_for(after,body['m_GameObject']['fileID']) if 'm_GameObject' in body else ''
            for spec in semantic:
                if spec['path']==path and spec['field'] in body and ((spec['component']=='SpriteRenderer' and key=='SpriteRenderer') or (spec['component']=='TextOutline' and key=='MonoBehaviour')):
                    field=spec['field']
                    if not near(body[field],spec):failures.append('Wrong component color: '+rel+':'+path)
                    if old[field]['a']!=body[field]['a']:failures.append('Alpha changed: '+rel+':'+path)
                    changes.append((path,field));old[field]=body[field]
            if key=='TextMesh':
                for spec in textspec:
                    if path.split('/')[-1]==spec['object'] and (str(body['m_Text']) if body['m_Text'] is not None else '')==spec['key']:
                        packed=sum(round(spec[c]*255)<<(8*i) for i,c in enumerate('rgba'))
                        if body['m_Color'].get('rgba')!=packed:failures.append('Wrong text color: '+rel+':'+path)
                        if old['m_Color']['rgba']>>24!=packed>>24:failures.append('Text alpha changed: '+rel+':'+path)
                        changes.append((path,'m_Color'));old['m_Color']=body['m_Color']
            if old!=body:failures.append('Undeclared serialized delta: '+rel+':'+id+':'+key)
        (scene_changes if rel.endswith('.unity') else prefab_changes).extend(changes)
    expected=len(semantic)+len(textspec)
    if len(scene_changes)!=expected or len(prefab_changes)!=expected:failures.append('Incomplete color application in scene/prefabs')
    stats.update(sceneDeclaredColors=len(scene_changes),prefabDeclaredColors=len(prefab_changes),prefabs=sum(f.endswith('.prefab') for f in files))
    for spec in presentation['materialColorOverrides'].values():
        rel=spec['assetPath'];old=docs(baseline(rel));new=docs((PROJECT/rel).read_bytes())
        for key in new:
            a=old[key][1]['Material'];b=new[key][1]['Material']
            ac=next(v['_Color'] for v in a['m_SavedProperties']['m_Colors'] if '_Color' in v);bc=next(v['_Color'] for v in b['m_SavedProperties']['m_Colors'] if '_Color' in v)
            if not near(bc,spec) or ac['a']!=bc['a']:failures.append('Material value/alpha: '+rel)
            ac.update(bc)
        if old!=new:failures.append('Undeclared material delta: '+rel)
    stats['materials']=len(presentation['materialColorOverrides'])
    m=read(PROJECT/'Assets/Editor/RecoverySource/manifest.json');objects={o['id']:o for o in m['objects']};source=REPO/'prototypes/re-crunch-time';jobs=read(ROOT/'fidelity/art/jobs.json')['jobs'];rows=[]
    for j in jobs:
        obj=objects[j['id']];actual=PROJECT/obj['assetPath'];reference=source/obj['exportFiles'][0]['path']
        with Image.open(actual) as im,Image.open(reference) as original:
            if im.size!=(j['width'],j['height']) or original.size!=im.size:failures.append('Wrong dimensions: '+j['name'])
            if j['name'] in surfaces:
                alpha=np.array_equal(np.asarray(im.convert('RGBA'))[:,:,3],np.asarray(original.convert('RGBA'))[:,:,3])
                if not alpha:failures.append('Functional alpha changed: '+j['name'])
                spec=surfaces[j['name']]
                if sha((ROOT/spec['source']).read_bytes())!=spec['sourceSHA256'] or spec['sampling']!='uniform_cover':failures.append('Material provenance/sampling: '+j['name'])
                with Image.open(ROOT/spec['source']) as raw:
                    scale=max(im.width/raw.width,im.height/raw.height)
                    error=abs(raw.width/raw.height/(im.width/im.height)-1)
                    if spec['mode']=='framed' and error>spec['maxAspectError']:failures.append('Frame would distort/crop: '+j['name'])
                    rows.append(dict(name=j['name'],size=list(im.size),exactAlpha=alpha,sampling=spec['sampling'],uniformScale=scale,sourceSize=list(raw.size),aspectError=error,runtimeSHA256=sha(actual.read_bytes())))
            else:
                if actual.read_bytes()!=baseline(obj['assetPath']):failures.append('Retained artwork changed: '+j['name'])
    stats.update(textures=len(jobs),replaced=len(rows),retained=len(jobs)-len(rows))
    report=dict(version='2.8',baseline=BASE,project=PROJECT.name,passed=not failures,statistics=stats,failures=failures,surfaces=rows,scope='Every scene/prefab serialized field; 78 Sprite binaries, importer/meta/package/settings byte locks; 82 dimensions and 38 alpha planes. Visual quality requires actual Player review.')
    out=ROOT/'local/style-contract-v2.8.json';out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print(json.dumps({k:report[k] for k in ('passed','statistics','failures')},ensure_ascii=False));return report['passed']
if __name__=='__main__':raise SystemExit(0 if verify() else 2)
