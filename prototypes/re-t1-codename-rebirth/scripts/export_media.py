"""Export all supported textures, meshes and AudioClips for local inspection.

Serialized originals remain authoritative: OBJ does not preserve skinning,
blend shapes, Unity materials or scene transforms; WAV is a listening copy.
"""
import collections, gzip, hashlib, json, re, shutil, time
from pathlib import Path
import UnityPy
from PIL import Image, ImageDraw
from UnityPy.helpers.ResourceReader import get_resource_data

ROOT=Path(__file__).resolve().parents[1]

def main():
    t=time.monotonic(); rows=[]; errors=[]; totals=collections.Counter()
    index=json.loads((ROOT/'local/decoded/bundle-index.json').read_text('utf8'))
    out=ROOT/'local/assets/export'; out.mkdir(parents=True,exist_ok=True)
    for b in index:
        env=UnityPy.load(str(ROOT/'local/decoded/bundles'/b['filename']))
        named={p.path_id:a for a,p in env.container.items()}
        for obj in env.objects:
            kind=obj.type.name
            if kind not in ('Texture2D','Mesh','AudioClip'):continue
            row={'bundleID':b['id'],'pathID':obj.path_id,'type':kind,'address':named.get(obj.path_id,''),'files':[]}
            try:
                assert shutil.disk_usage(ROOT).free>2_000_000_000,'Preserve free space for local version backup'
                a=obj.read(); row['name']=a.m_Name
                prefix=f"{b['id']:04}_{obj.path_id}_"+re.sub(r'[^\w.-]','_',a.m_Name)[:80]
                folder=out/kind;folder.mkdir(exist_ok=True)
                if kind=='Texture2D':
                    im=a.image; dest=folder/(prefix+'.png');im.save(dest)
                    with Image.open(dest) as check:check.verify()
                    row.update(width=im.width,height=im.height,format=int(a.m_TextureFormat))
                    targets=[dest]
                elif kind=='Mesh':
                    dest=folder/(prefix+'.obj.gz')
                    text=a.export()
                    assert text and '\nv ' in '\n'+text, 'No OBJ vertices'
                    with gzip.open(dest,'wt',encoding='utf8') as f:f.write(text)
                    row['vertices']=sum(l.startswith('v ') for l in text.splitlines())
                    row['faces']=sum(l.startswith('f ') for l in text.splitlines())
                    targets=[dest]
                else:
                    raw=bytes(a.m_AudioData) if a.m_AudioData else get_resource_data(a.m_Resource.m_Source,obj.assets_file,a.m_Resource.m_Offset,a.m_Resource.m_Size)
                    dest=folder/(prefix+'.native');dest.write_bytes(raw);targets=[dest]
                    row.update(channels=a.m_Channels,frequency=a.m_Frequency,duration=a.m_Length,sourceHeader=raw[:16].hex())
                    samples=a.samples; assert samples,'No decoded samples'
                    for si,(name,data) in enumerate(samples.items()):
                        ext=Path(name).suffix or '.wav'; sample=folder/(prefix+f'_{si}'+ext); sample.write_bytes(data); targets.append(sample)
                    row['samples']=len(samples)
                for f in targets:row['files'].append({'path':f.relative_to(ROOT/'local').as_posix(),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
                row['status']='ok';totals[kind]+=1
            except Exception as ex:
                row['status']='failed';row['error']=type(ex).__name__+': '+str(ex);errors.append(row.copy())
            rows.append(row)
        if (b['id']+1)%50==0:print(f"bundle {b['id']+1}: {dict(totals)}, failures {len(errors)}",flush=True)
    (out/'index.json').write_text(json.dumps(rows,ensure_ascii=False,separators=(',',':')),'utf8')
    (out/'errors.json').write_text(json.dumps(errors,ensure_ascii=False,indent=2),'utf8')
    # Representative contact sheet, with identifiers rather than guessed labels.
    candidates=[x for x in rows if x['type']=='Texture2D' and x['status']=='ok' and 32<=x['width']<=4096 and 32<=x['height']<=4096]
    selected=[]
    for pattern in ('weapon','icon','map','hero','ui','terrain','head','character'):
        selected.extend([x for x in candidates if pattern in (x['name']+' '+x['address']).lower()][:4])
    selected=list({(x['bundleID'],x['pathID']):x for x in selected}.values())[:24]
    if not selected:selected=candidates[:24]
    sheet=Image.new('RGB',(1200,240*((len(selected)+5)//6)),(30,33,40));draw=ImageDraw.Draw(sheet)
    for i,row in enumerate(selected):
        im=Image.open(ROOT/'local'/row['files'][0]['path']).convert('RGBA');im.thumbnail((184,195))
        x=(i%6)*200+(200-im.width)//2;y=(i//6)*240+10;sheet.paste(im,(x,y),im)
        draw.text(((i%6)*200+8,(i//6)*240+207),f"{row['bundleID']} / {row['pathID']}"[:29],fill='white')
        draw.text(((i%6)*200+8,(i//6)*240+222),row['name'].encode('ascii','replace').decode()[:29],fill=(170,185,205))
    sheet.save(out/'contact-sheet.png')
    (out/'contact-sheet-index.json').write_text(json.dumps(selected,ensure_ascii=False,indent=2),'utf8')
    evidence={'attempted':len(rows),'passed':dict(totals),'failed':len(errors),'exportedBytes':sum(f['bytes'] for x in rows for f in x['files']),'seconds':round(time.monotonic()-t,2),'textures':'PNG verified by Pillow; visual contact sheet is a sample','meshes':'OBJ gzip; source bundles preserve complete skinning, blend shapes and materials','audio':'Original encoded clip payload plus decoded samples; no listening claim','APKExecuted':False}
    (ROOT/'evidence/media-export.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),'utf8')
    print(json.dumps(evidence,ensure_ascii=False),flush=True)

if __name__=='__main__':main()
