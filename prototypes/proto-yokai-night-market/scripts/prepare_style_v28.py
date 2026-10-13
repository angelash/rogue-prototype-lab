"""Declare v2.8 presentation deltas; never restore or rebuild scene geometry.

Use NIGHT_MARKET_PROJECT_PATH for an isolated source snapshot while Unity is busy.
Raster production is performed by NightAssetExport, not this script.
"""
from pathlib import Path
import os, json, hashlib, shutil
from datetime import datetime
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PROJECT = Path(os.environ.get('NIGHT_MARKET_PROJECT_PATH', ROOT/'local/UnityProject')).resolve()
assert PROJECT.is_relative_to((ROOT/'local').resolve())
def read(p): return json.loads(p.read_text('utf-8'))
def write(p,v):
    p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text((json.dumps(v,ensure_ascii=False,indent=2) if not isinstance(v,str) else v).rstrip()+'\n',encoding='utf-8',newline='\n')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def color(c): return dict(zip('rgba',[*c,1]))

def main():
    art=ROOT/'fidelity/art-direction-v2.8'
    palette=read(art/'palette.json'); colors={k:v['rgb'] for k,v in palette['colors'].items()}
    manifest=read(PROJECT/'Assets/Editor/RecoverySource/manifest.json'); objects={o['id']:o for o in manifest['objects']}
    history=ROOT/'history/presentation-v2.7'
    for name in ('ui-surfaces.json','presentation.json'):
        if not (history/name).exists():
            history.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/'fidelity/data'/name,history/name)
    ledger=read(art/'generation-register.json')
    raw=art/'materials/card-lower-master-v1.raw.png';prompt=raw.with_name('card-lower-master-v1.prompt.txt')
    write(prompt,prompt.read_text('utf-8'))
    if not any(r['name']=='card-lower-master-v1' for r in ledger['images']):
        with Image.open(raw) as im: size=list(im.size);alpha=list(im.convert('RGBA').getchannel('A').getextrema())
        ref=art/'samples/style-proof-v2.png'
        ledger['images'].append(dict(name='card-lower-master-v1',status='production_source_pending_integration',raw=raw.relative_to(ROOT).as_posix(),rawSHA256=sha(raw),rawSize=size,alphaRange=alpha,prompt=prompt.relative_to(ROOT).as_posix(),promptSHA256=sha(prompt),references=[dict(path=ref.relative_to(ROOT).as_posix(),sha256=sha(ref))],transparentRequested=False,tool='image_gen.imagegen',registeredAt=datetime.now().astimezone().isoformat(),review='单层靛蓝纸底与薄旧金边；等比裁切派生，不携带功能孔位或烘焙文字。'))
    write(art/'generation-register.json',ledger)
    v2=art/'materials/card-lower-master-v2.raw.png';v2prompt=v2.with_name('card-lower-master-v2.prompt.txt');write(v2prompt,v2prompt.read_text('utf-8'))
    if not any(r['name']=='card-lower-master-v2' for r in ledger['images']):
        with Image.open(v2) as im:size=list(im.size);alpha=list(im.convert('RGBA').getchannel('A').getextrema())
        ledger['images'].append(dict(name='card-lower-master-v2',status='production_source_pending_integration',raw=v2.relative_to(ROOT).as_posix(),rawSHA256=sha(v2),rawSize=size,alphaRange=alpha,prompt=v2prompt.relative_to(ROOT).as_posix(),promptSHA256=sha(v2prompt),references=[dict(path=raw.relative_to(ROOT).as_posix(),sha256=sha(raw))],transparentRequested=False,tool='image_gen.imagegen',registeredAt=datetime.now().astimezone().isoformat(),review='实机发现分隔条与旧金线相交，右边24–33%和底边20–82%留断口；不修改分隔条或 alpha。'))
    write(art/'generation-register.json',ledger)
    sources={k:('fidelity/art-direction-v2.8/materials/'+k+'.raw.png') for k in ('indigo-paper-v1','neutral-paper-v1','card-lower-master-v2')}
    plan=read(art/'replacements.json');specs={}
    for e in plan['entries']:
        if e['action']=='retain':continue
        role=e['role'];s=dict(mode='paper',role=role,source=sources['indigo-paper-v1'],sampling='uniform_cover',sourceSHA256=sha(ROOT/sources['indigo-paper-v1']))
        if role=='card_lower':s.update(mode='framed',source=sources['card-lower-master-v2'],maxAspectError=.005)
        if role in ('tintable_dividers','tintable_header','tintable_token','token','option_token','status_icon','navigation','phase_outline','selection_outline','native_badge'):
            s.update(source=sources['neutral-paper-v1'])
        if role in ('navigation','phase_outline','selection_outline'):s['rgb']=colors['oldGold']
        if e['name']=='PRJ_Dot 1':s['rgb']=colors['paper']
        if role=='option_token':s['rgb']=colors['negative' if e['name']=='OptionRed' else 'positive']
        if role=='status_icon':s['rgb']=colors[{'PRJ_Ok':'successText','PRJ_No':'dangerText','PRJ_WIP':'oldGold'}[e['name']]]
        if role in ('glow','technical_shading'):s.update(mode='flat',rgb=colors['hover'])
        if role in ('button','picture_surround'):
            s.update(mode='outlined_paper',borderRGB=colors['oldGold'],borderPixels=2 if role=='button' else 7)
        if role=='ledger':
            # These five source RGB partitions are functional work grooves, not decoration.
            s['functionalSlots']={'xMin':600,'xMax':1200,'topRows':[[347,415],[442,511],[538,607],[635,703],[731,798]],'sourceMaxRGB':.02,'rgb':[.065,.09,.105]}
        if role=='native_badge':s.update(mode='label',label='中' if e['name']=='English' else 'EN',rgb=colors['paper'],labelRGB=colors['darkText'],languageSlot=0 if e['name']=='English' else 1)
        s['sourceSHA256']=sha(ROOT/s['source']);specs[e['name']]=s
    assert len(specs)==38
    write(ROOT/'fidelity/data/ui-surfaces.json',dict(version=2,date='2026-10-13',scope='38 night-market layers. Uniform cover sampling, source alpha byte-for-byte, unchanged sprites/import settings/scene geometry. Only five declared ledger grooves retain source functional RGB partitions.',textures=specs))
    presentation=read(ROOT/'fidelity/data/presentation.json')
    reference=ROOT.parent/'re-crunch-time/local/typetrees'
    for o in objects.values():
        if o['type']!='TextMesh':continue
        original=read(reference/o['container']/(str(o['pathID'])+'_TextMesh.json'))
        n=original['m_Color']['rgba'];r,g,b,a=[(n>>(i*8))&255 for i in range(4)]
        if g>180 and b<100:
            go=objects[o['container']+':'+str(o['tree']['m_GameObject']['m_PathID'])]
            presentation['textColorOverrides'][o['id']]=dict(object=go['name'],key=o['tree']['m_Text'],**dict(zip('rgba',[*colors['paper'],a/255])))
    for id,v in presentation['textColorOverrides'].items():
        c=colors['blueText' if id=='mainData:1211' else 'successText' if id=='mainData:1224' else 'lightText'];v.update(dict(zip('rgb',c)))
    materials={}
    def resolve(container,ref):
        container=container.split(':')[0]
        if ref['m_FileID']:
            table=next(c for c in manifest['containers'] if c['name']==container)
            container=table['externals'][ref['m_FileID']-1]['path'].split('/')[-1]
        return container+':'+str(ref['m_PathID'])
    resources={resolve(container,pair[1]):pair[0] for container,tree in manifest['resourceMap'].items() for pair in tree.get('m_Container',[])}
    for t in palette['materialTargets']:
        resource=resources.get(t['id'],t['name'].lower()).replace('/','_');path='Assets/Resources/'+resource+'.asset'
        if not (PROJECT/path).exists():path='Assets/Recovery/Generated/'+t['id'].replace(':','_')+'.asset'
        assert (PROJECT/path).exists(),path
        materials[t['id']]=dict(assetPath=path,name=t['name'],palette=t['palette'],**color(colors[t['palette']]))
    presentation['materialColorOverrides']=materials
    def go_path(goid):
        go=objects[goid];components=go['tree']['m_Component']
        tr=next(objects[goid.split(':')[0]+':'+str(v[1]['m_PathID'])] for v in components if objects[goid.split(':')[0]+':'+str(v[1]['m_PathID'])]['type']=='Transform')
        parent=tr['tree']['m_Father']['m_PathID']
        return (go_path(goid.split(':')[0]+':'+str(objects[goid.split(':')[0]+':'+str(parent)]['tree']['m_GameObject']['m_PathID']))+'/' if parent else '')+go['name']
    semantic={}
    for o in objects.values():
        t=o['tree'];field=None;ctype=None;rgb=None
        if o['type']=='SpriteRenderer':t=read(reference/o['container']/(str(o['pathID'])+'_SpriteRenderer.json'))
        if o['type']=='SpriteRenderer' and t.get('m_Color',{}).get('g',0)>.85 and t['m_Color'].get('b',1)<.06:
            field='m_Color';ctype='SpriteRenderer';rgb=colors['hover']
        if o.get('scriptClass')=='TextOutline':field='outlineColor';ctype='TextOutline';rgb=colors['darkText']
        if field:
            path=go_path(o['container']+':'+str(t['m_GameObject']['m_PathID']))
            semantic[o['id']]=dict(path=path,component=ctype,field=field,**dict(zip('rgba',[*rgb,t[field]['a']])))
    assert len(semantic)==35,len(semantic)
    presentation['componentColorOverrides']=semantic
    replacements=[]
    def replace(file,before,paletteKey,count=1,alpha=None):
        rgb=colors[paletteKey];after='new Color('+', '.join(f'{c:.6f}f' for c in rgb)+(', '+str(alpha)+'f' if alpha is not None else '')+')'
        replacements.append(dict(file=file,before=before,after=after,count=count,palette=paletteKey))
    for a,k in [('new Color(0.9254902f, 0f, 0f)','dangerText'),('new Color(32f / 51f, 73f / 85f, 0.050980393f)','successText'),('new Color(0.85f, 0.85f, 0.85f)','lightText'),('new Color(12f / 85f, 12f / 85f, 12f / 85f)','darkText'),('new Color(0f, 0f, 0.9254902f)','blueText')]:replace('TitleManager.cs',a,k)
    for a,k,n in [('new Color(12f / 85f, 12f / 85f, 12f / 85f)','darkText',1),('new Color(0f, 0f, 0f)','darkText',1),('new Color(0.8f, 0.8f, 0.8f)','lightText',1),('new Color(0.29f, 0.93f, 0.4f)','successText',1),('Color.red','dangerText',2)]:replace('ProjectCard.cs',a,k,n)
    replace('PlayCard.cs','new Color(0.80784315f, 0.80784315f, 0.80784315f)','lightText')
    replace('PlayCard.cs','new Color(0.5882353f, 44f / 51f, 46f / 51f, 0.5f)','positive',alpha=.5)
    # Continue uses color equality as its flash-state sentinel; replace both sides together.
    replace('Continue.cs','Color.green','successText',count=2)
    presentation.update(version=2,codeColorReplacements=replacements,scope='28 material colors, 35 serialized component colors, 17 TextMesh colors, 14 explicit runtime color literals (15 occurrences), existing subtitle-size exception. No geometry, callbacks, numeric rules, timing or alpha changes.')
    write(ROOT/'fidelity/data/presentation.json',presentation)
    for id,v in semantic.items():objects[id]['tree'][v['field']]={c:v[c] for c in 'rgba'}
    write(PROJECT/'Assets/Editor/RecoverySource/manifest.json',manifest)
    for file in set(v['file'] for v in replacements):
        path=PROJECT/'Assets/Recovery/Scripts'/file;text=path.read_text('utf-8')
        for d in (v for v in replacements if v['file']==file):
            if text.count(d['before'])==d['count']:text=text.replace(d['before'],d['after'])
            else:assert text.count(d['after'])>=d['count'],d
        write(path,text)
    print(json.dumps(dict(project=str(PROJECT),surfaces=len(specs),materials=len(materials),components=len(semantic),colorLiterals=sum(x['count'] for x in replacements)),ensure_ascii=False))

if __name__=='__main__':main()
