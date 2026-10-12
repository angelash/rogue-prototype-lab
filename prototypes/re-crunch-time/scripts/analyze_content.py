"""Join local decompiled metadata, XML content and Unity object references."""
from __future__ import annotations
import argparse, collections, json, re, struct, xml.etree.ElementTree as ET
from pathlib import Path
from PIL import Image

def load(p): return json.loads(p.read_text(encoding='utf-8'))
def save(p,v):
    p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')

def pe_info(path):
    """Read PE headers and import names without loading or executing the binary."""
    data=path.read_bytes(); pe=struct.unpack_from('<I',data,0x3c)[0]
    assert data[pe:pe+4]==b'PE\0\0'
    machine,sections=struct.unpack_from('<HH',data,pe+4)
    optional_size=struct.unpack_from('<H',data,pe+20)[0]; opt=pe+24
    magic=struct.unpack_from('<H',data,opt)[0]; directory=opt+(112 if magic==0x20b else 96)
    section_data=[]
    for i in range(sections):
        at=opt+optional_size+40*i
        vsize,rva,size,raw=struct.unpack_from('<IIII',data,at+8)
        section_data.append((rva,max(vsize,size),raw))
    def offset(rva):
        for start,size,raw in section_data:
            if start<=rva<start+size:return raw+rva-start
        return rva
    def cstring(at):return data[at:data.index(b'\0',at)].decode('ascii',errors='replace')
    import_rva=struct.unpack_from('<I',data,directory+8)[0];imports=[]
    if import_rva:
        at=offset(import_rva)
        while any(data[at:at+20]):
            lookup,_,_,name_rva,thunk=struct.unpack_from('<IIIII',data,at)
            funcs=[]; pos=offset(lookup or thunk);stride=8 if magic==0x20b else 4
            while True:
                value=int.from_bytes(data[pos:pos+stride],'little');pos+=stride
                if not value:break
                funcs.append('ordinal:'+str(value&0xffff) if value>>(stride*8-1) else cstring(offset(value)+2))
            imports.append({'library':cstring(offset(name_rva)),'functions':funcs});at+=20
    return {'machine':hex(machine),'architecture':{0x14c:'x86',0x8664:'x64'}.get(machine,'other'),'optionalMagic':hex(magic),'imports':imports}

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);a=ap.parse_args();p=Path(a.project).resolve();local=p/'local'
    objects=load(p/'evidence/objects.json');byid={o['id']:o for o in objects};edges=load(p/'evidence/dependencies.json')
    resources=load(local/'resource-map.json');maps={}
    for oid,tree in resources.items():
        for i,(key,ptr) in enumerate(tree['m_Container']):
            edge=next((e for e in edges if e['from']==oid and e['field']==f'.m_Container[{i}][1]'),None)
            if edge:maps[key]=edge['to']
    texts={o['name']:p/o['exportFiles'][0]['path'] for o in objects if o['type']=='TextAsset'}
    translations=[]
    for name in ('Texts','TextsProjects','TextsCards'):
        for node in ET.parse(texts[name]).getroot().findall('Text'):
            translations.append({'source':name,'id':node.findtext('ID') or '','ENG':node.findtext('ENG') or '','SPA':node.findtext('SPA') or ''})
    strings={r['id']:r for r in translations};assert len(strings)==len(translations),'Duplicate localization IDs'
    defaults={'ID':1,'Type':0,'Turns':0,'Target':-1,'Function':0,'Num':0,'Repair':'','Picture':'Default'}
    cards=[]
    for node in ET.parse(texts['CardsPlay']).getroot().findall('Card'):
        raw={n.tag:n.text or '' for n in node};row=dict(defaults)
        for k,v in raw.items():row[k]=int(v) if k not in ('Repair','Picture') else v
        row['raw']=raw;row['texts']={k:{lang:strings[k+str(row['ID'])][lang] for lang in ('ENG','SPA')} for k in ('Name','Quote','Description')}
        row['spriteID']=maps.get(row['Picture'].lower());row['spriteExports']=byid.get(row['spriteID'],{}).get('exportFiles',[])
        cards.append(row)
    assert len(cards)==95 and sorted(c['ID'] for c in cards)==list(range(1,96))
    assert all(c['spriteID'] and byid[c['spriteID']]['type']=='Sprite' for c in cards),'Missing card picture'
    projects=[]
    for node in ET.parse(texts['CardsProject']).getroot().findall('Card'):
        raw={n.tag:n.text or '' for n in node};ident=int(raw['ID'])
        effective={k:int(raw.get(k,'2')) for k in ('Sound','Programming','Design','Art','Testing')}
        projects.append({'ID':ident,'platform':raw['Platform'],'raw':raw,'effectiveTasks':effective,'totalTasks':sum(effective.values()),'ignoredAnimation':int(raw['Animation']),'testingDiffersFromAnimation':effective['Testing']!=int(raw['Animation']),'texts':{k:{lang:strings[k+str(ident)][lang] for lang in ('ENG','SPA')} for k in ('Title','SubTitle')}})
    assert len(projects)==28 and sorted(c['ID'] for c in projects)==list(range(1,29))
    metadata=load(local/'code-metadata.json');assert not metadata['failures']
    code=[]
    for t in metadata['types']:
        path=local/'decompiled'/(t['name'].split('+')[0]+'.cs')
        code.append({**t,'source':path.relative_to(p).as_posix() if path.exists() else None})
    public_code={k:metadata[k] for k in ('assembly','version','references','typeCount','methodCount','fieldCount')}
    public_code['types']=[{k:v for k,v in t.items() if k!='methods'}|{'methods':[{k:v for k,v in m.items() if k!='literals'} for m in t['methods']]} for t in code]
    public_code['methodsWithIL']=sum(m['ilLength']>0 for t in code for m in t['methods'])
    public_code['instructionCount']=sum(m['instructionCount'] for t in code for m in t['methods'])
    save(p/'evidence/code-index.json',public_code)
    assemblies=[]
    for file in [local/'code-metadata.json',*sorted((local/'support-metadata').glob('*.json'))]:
        m=load(file);name=m['assembly'];folder=local/('decompiled' if name=='Assembly-CSharp' else 'decompiled-support/'+name)
        assemblies.append({k:m[k] for k in ('assembly','version','machine','corFlags','references','typeCount','methodCount','fieldCount','failures')}|{'methodsWithIL':sum(mm['ilLength']>0 for t in m['types'] for mm in t['methods']),'decompiledCSFiles':len(list(folder.rglob('*.cs'))),'decompiledCSBytes':sum(f.stat().st_size for f in folder.rglob('*.cs'))})
    save(p/'evidence/assemblies.json',assemblies)
    natives=[]
    for f in (local/'build-copy/CrunchTime.exe',local/'build-copy/CrunchTime_Data/Mono/mono.dll'):
        natives.append({'path':f.relative_to(local/'build-copy').as_posix(),**pe_info(f)})
    save(p/'evidence/native-index.json',natives)
    build_structure=[]
    for f in load(p/'evidence/build-files.json'):
        name=f['path'];file=local/'build-copy'/name;entry={'path':name,'bytes':f['bytes']}
        if name.endswith('.exe') or name.endswith('/mono.dll'):entry['role']='Native engine / runtime PE'
        elif '/Managed/' in name:entry['role']='Managed assembly; fully decompiled and indexed'
        elif name.endswith(('.assets','/mainData','unity default resources','unity_builtin_extra')):entry['role']='Unity serialized container; all objects parsed'
        elif '/Mono/etc/' in name:
            entry['role']='Bundled Mono framework configuration or ASPX helper'
            raw=file.read_bytes()
            try:raw.decode('utf-8-sig');entry['textViewEncoding']='utf-8-sig'
            except UnicodeDecodeError:raw.decode('latin-1');entry['textViewEncoding']='latin-1 lossless byte view (source encoding unconfirmed)'
            if file.suffix in ('.config','.xml','.map','.browser') or file.name=='config':
                try:entry['xmlRoot']=ET.parse(file).getroot().tag
                except ET.ParseError as ex:entry['xmlNote']=str(ex)
        elif name.endswith('.bmp'):
            entry['role']='Engine startup screen selector bitmap'
            with Image.open(file) as im:im.load();entry['dimensions']=[im.width,im.height]
        else:entry['role']='Unity player connection configuration; raw preserved'
        build_structure.append(entry)
    save(p/'evidence/build-structure.json',build_structure)
    states=[]
    src=(local/'decompiled/hGameState.cs').read_text('utf-8')
    for name,value in re.findall(r'public const int (ST_\w+) = (-?\d+);',src):states.append({'name':name,'id':int(value)})
    bindings=load(local/'behaviours.json');scene=load(local/'scene-hierarchy.json')
    summary={'cards':len(cards),'projects':len(projects),'cardTypes':dict(collections.Counter(c['Type'] for c in cards)),'workerTurns':dict(collections.Counter(c['Turns'] for c in cards if c['Type']<5)),'platforms':dict(collections.Counter(c['platform'] for c in projects)),'projectTotalTasks':dict(collections.Counter(c['totalTasks'] for c in projects)),'ignoredAnimationValues':dict(collections.Counter(c['ignoredAnimation'] for c in projects)),'testingDiffersFromAnimationIDs':[c['ID'] for c in projects if c['testingDiffersFromAnimation']],'localizationSources':dict(collections.Counter(r['source'] for r in translations)),'localizationKeys':len(strings),'translationCells':2*len(strings),'emptyTranslationCells':sum(not r[l] for r in translations for l in ('ENG','SPA')),'cardSpriteLinks':len(cards),'uniqueCardPictures':len(set(c['spriteID'] for c in cards)),'resourceKeys':len(maps),'sceneObjects':sum(c['id'].startswith('mainData:') for c in scene),'sceneRoots':[{'id':c['id'],'name':c['name']} for c in scene if c['id'].startswith('mainData:') and c.get('father',{}).get('m_PathID')==0],'behaviourClasses':dict(collections.Counter(c['class'] for c in bindings)),'states':states}
    save(p/'evidence/content-summary.json',summary)
    save(p/'evidence/content-index.json',{'cards':[{k:v for k,v in c.items() if k not in ('texts','raw','spriteExports')} for c in cards],'projects':[{k:v for k,v in c.items() if k not in ('texts','raw')} for c in projects]})
    catalog={'summary':summary,'cards':cards,'projects':projects,'translations':translations,'resources':maps,'assets':objects,'code':code,'scene':scene,'behaviours':bindings}
    save(local/'content-catalog.json',catalog)
    template=(p/'scripts/explorer.html').read_text('utf-8')
    (local/'explorer.html').write_text(template.replace('__CATALOG_JSON__',json.dumps(catalog,ensure_ascii=False).replace('<','\\u003c')),encoding='utf-8')
    print(json.dumps({'cards':len(cards),'projects':len(projects),'resourceKeys':len(maps),'translationKeys':len(strings),'assemblies':len(assemblies),'scriptPointersResolved':all(c['class'] for c in bindings)},ensure_ascii=False))

if __name__=='__main__':main()
