"""Verify extraction coverage, hashes, references and decoded media, not gameplay."""
from __future__ import annotations
import argparse, collections, hashlib, json, subprocess, wave, xml.etree.ElementTree as ET
from pathlib import Path
from PIL import Image
from fontTools.ttLib import TTFont

def read(p):return json.loads(p.read_text('utf-8'))
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
    return h.hexdigest()

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);ap.add_argument('--source',required=True);a=ap.parse_args();p=Path(a.project).resolve();src=Path(a.source).resolve();local=p/'local'
    checks=[];media=[];failures=[]
    def check(name,result,details=None):
        checks.append({'check':name,'passed':bool(result),'details':details})
        if not result:failures.append(name)
    build=read(p/'evidence/build-files.json');check('25 original files inventoried',len(build)==25)
    check('Original file set unchanged',set(x['path'] for x in build)=={f.relative_to(src).as_posix() for f in src.rglob('*') if f.is_file()})
    check('Original and copy SHA256 match inventory after analysis',all(sha(src/x['path'])==x['sha256']==sha(local/'build-copy'/x['path']) for x in build))
    objects=read(p/'evidence/objects.json');check('1985 unique objects',len(objects)==len({o['id'] for o in objects})==1985)
    check('All object trees parsed',all(o['treeStatus']=='ok' for o in objects))
    check('Raw objects retain original hashes',all(sha(local/'raw-objects'/o['container']/f"{o['pathID']}_{o['type']}.bin")==o['sha256'] for o in objects))
    check('All serialized trees saved',all((local/'typetrees'/o['container']/f"{o['pathID']}_{o['type']}.json").is_file() for o in objects))
    deps=read(p/'evidence/dependencies.json');known={o['id'] for o in objects}
    check('4443 pointer edges resolve',len(deps)==4443 and all(e['from'] in known and e['to'] in known for e in deps))
    behaviours=read(local/'behaviours.json');check('46 MonoBehaviour scripts resolve',len(behaviours)==46 and all(b['class'] and b['script'] in known for b in behaviours))
    scene=read(local/'scene-hierarchy.json');check('All 462 GameObjects have Transform components',len(scene)==462 and all(any(c['type']=='Transform' for c in s['components']) for s in scene))
    summary=read(p/'evidence/content-summary.json');check('95 cards, 28 projects, 420 texts, 18 states',summary['cards']==95 and summary['projects']==28 and summary['localizationKeys']==420 and len(summary['states'])==18)
    catalog=read(local/'content-catalog.json');check('All card pictures link to Sprite',all(c['spriteID'] in known and next(o['type'] for o in objects if o['id']==c['spriteID'])=='Sprite' for c in catalog['cards']))
    byid={o['id']:o for o in objects};check('All resource keys resolve',all(x in byid for x in catalog['resources'].values()))
    code=read(local/'code-metadata.json');check('Game metadata 40/663/959, no IL failures',code['typeCount']==40 and code['methodCount']==663 and code['fieldCount']==959 and not code['failures'])
    assemblies=read(p/'evidence/assemblies.json');check('All 6 managed assemblies indexed and decompiled',len(assemblies)==6 and all(not x['failures'] and x['decompiledCSFiles']>0 for x in assemblies))
    check('Every top-level game class has decompiled C#',all((local/'decompiled'/(t['name']+'.cs')).exists() for t in code['types'] if not t['declaringType'] and not t['name'].startswith(('<','$'))))
    exports=[(o,f) for o in objects for f in o['exportFiles']];check('All nonempty export file hashes match',all((p/f['path']).is_file() and sha(p/f['path'])==f['sha256'] and (p/f['path']).stat().st_size==f['bytes'] for o,f in exports))
    for o,f in exports:
        path=p/f['path'];entry={'id':o['id'],'path':f['path'],'type':o['type'],'verified':False}
        try:
            if o['type'] in ('Texture2D','Sprite'):
                with Image.open(path) as im:im.verify()
                with Image.open(path) as im:im.load();entry.update(width=im.width,height=im.height,mode=im.mode)
            elif o['type']=='AudioClip':
                probe=subprocess.run(['ffprobe','-v','error','-show_streams','-show_format','-of','json',str(path)],capture_output=True,check=True,text=True,encoding='utf-8')
                info=json.loads(probe.stdout);s=info['streams'][0];entry.update(codec=s['codec_name'],channels=s['channels'],sampleRate=int(s['sample_rate']),seconds=float(info['format']['duration']))
                subprocess.run(['ffmpeg','-v','error','-nostdin','-i',str(path),'-f','null','-'],capture_output=True,check=True)
            elif o['type']=='Font':
                with TTFont(path) as font:
                    entry.update(glyphs=len(font.getGlyphOrder()),tables=sorted(font.keys()))
                    for tag in font.keys():_ = font[tag] # Materialize every table.
            elif o['type']=='Mesh':
                lines=path.read_text('utf-8').splitlines();entry.update(vertices=sum(l.startswith('v ') for l in lines),faces=sum(l.startswith('f ') for l in lines))
                assert entry['vertices']>0 and entry['faces']>0
            elif o['type']=='TextAsset':
                tree=ET.parse(path);entry.update(root=tree.getroot().tag,elements=len(list(tree.getroot())))
            elif o['type']=='Shader':
                text=path.read_text('utf-8');assert 'Shader' in text;entry['chars']=len(text)
            entry['verified']=True
        except Exception as ex:entry['error']=str(ex);failures.append(f['path'])
        media.append(entry)
    empty=[o for o in objects if o['exportStatus']=='empty']
    def expected_empty(o):
        tree=read(local/'typetrees'/o['container']/f"{o['pathID']}_{o['type']}.json")
        return (o['type']=='Texture2D' and tree['m_Width']==tree['m_Height']==0 and tree['image data']['binaryBytes']==0) or (o['type']=='Font' and o['name']=='Arial' and tree['m_FontData']==[])
    check('14 empty exports are dynamic font textures and builtin Arial without payload',len(empty)==14 and all(expected_empty(o) for o in empty))
    check('All 345 nonempty exports decode',len(media)==345 and all(x['verified'] for x in media))
    audio=[m for m in media if m['type']=='AudioClip']
    result={'date':'2026-10-12','scope':'Static extraction and file decoding; no native gameplay, listening or original project rebuild','checks':checks,'passed':sum(c['passed'] for c in checks),'failed':len(failures),'failures':failures,'mediaFiles':len(media),'verifiedMediaTypes':dict(collections.Counter(m['type'] for m in media if m['verified'])),'audioTotalSeconds':round(sum(m.get('seconds',0) for m in audio),6),'audioSecondsRange':[min(m.get('seconds',0) for m in audio),max(m.get('seconds',0) for m in audio)],'audioSampleRates':dict(collections.Counter(m.get('sampleRate') for m in audio))}
    (p/'evidence/verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    (local/'media-verification.json').write_text(json.dumps(media,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))
    if failures:raise SystemExit(1)

if __name__=='__main__':main()
