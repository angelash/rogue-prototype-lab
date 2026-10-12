"""Read-only Unity inventory and local-only extraction; never writes to the input build."""
from __future__ import annotations
import argparse, collections, csv, hashlib, json, re, sys, time
from pathlib import Path
import UnityPy
from UnityPy.tools import extractor
from PIL import Image, ImageDraw

def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''): h.update(block)
    return h.hexdigest()

def write(path, data):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')

def compact(value):
    if isinstance(value, bytes): return {'binaryBytes':len(value),'sha256':hashlib.sha256(value).hexdigest()}
    if isinstance(value, dict): return {k:('[redacted build metadata]' if 'token' in k.lower() else compact(v)) for k,v in value.items()}
    if isinstance(value, (list,tuple)):
        if len(value)>2048 and all(isinstance(x,int) and 0<=x<=255 for x in value):
            return {'byteArrayLength':len(value),'sha256':hashlib.sha256(bytes(value)).hexdigest()}
        return [compact(v) for v in value]
    return value

def pointers(value, field=''):
    if isinstance(value,dict):
        if 'm_FileID' in value and 'm_PathID' in value:
            if value['m_PathID']: yield (field,value['m_FileID'],value['m_PathID'])
        else:
            for k,v in value.items(): yield from pointers(v,field+'.'+k)
    elif isinstance(value,(list,tuple)):
        if len(value)>2048 and isinstance(value[0],(int,float,str,bool)): return
        for i,v in enumerate(value): yield from pointers(v,f'{field}[{i}]')

def export_legacy_sprite(obj, fp):
    """Unity 4 sprite mesh predates UnityPy's current MeshHandler vertexData path."""
    from UnityPy.export.SpriteHelper import get_image
    from PIL.Image import Transpose
    sprite=obj.parse_as_object();rd=sprite.m_RD
    image=get_image(sprite,rd.texture,rd.alphaTexture);rect=rd.textureRect
    image=image.crop((rect.x,rect.y,rect.x+rect.width,rect.y+rect.height))
    raw=obj.read_typetree()['m_RD'];verts=[v['pos'] for v in raw['vertices']]
    # Original 4.x tight mesh coordinates in world units; match the stock mask convention.
    if verts:
        minx=min(v['x'] for v in verts);miny=min(v['y'] for v in verts);factor=sprite.m_PixelsToUnits
        coords=[((v['x']-minx)*factor,(v['y']-miny)*factor) for v in verts]
        indices=raw['indices'];mask=Image.new('1',image.size,0);draw=ImageDraw.Draw(mask)
        for i in range(0,len(indices)-2,3):draw.polygon([coords[indices[j]] for j in range(i,i+3)],fill=1)
        if image.mode=='RGBA':image=Image.composite(image,Image.new('RGBA',image.size,0),mask)
        else:image.putalpha(mask)
    image.transpose(Transpose.FLIP_TOP_BOTTOM).save(fp+'.png')

def export_legacy_shader(obj, fp):
    script=obj.read_typetree()['m_Script']
    if isinstance(script,bytes):script=script.decode('utf-8',errors='replace')
    Path(fp+'.shader').write_text(script,encoding='utf-8')

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--source',required=True);ap.add_argument('--copy',required=True);ap.add_argument('--project',required=True);args=ap.parse_args()
    source=Path(args.source).resolve();copy=Path(args.copy).resolve();project=Path(args.project).resolve();local=project/'local'
    if not copy.is_relative_to(local) or source==copy: raise ValueError('Copy must be within project/local and differ from source')
    manifest=[]
    for p in sorted(source.rglob('*')):
        if p.is_file():
            r=p.relative_to(source);other=copy/r; original=sha(p); copied=sha(other)
            manifest.append({'path':r.as_posix(),'bytes':p.stat().st_size,'sha256':original,'copyMatches':original==copied})
    write(project/'evidence/build-files.json',manifest)
    if not all(x['copyMatches'] for x in manifest): raise ValueError('Copy differs from source')
    sys.path.insert(0,str(local/'tools/python'))
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    generator=TypeTreeGenerator('4.5.2f1');generator.load_local_game(str(copy))
    env=UnityPy.load(str(copy/'CrunchTime_Data'));env.typetree_generator=generator
    records=[]; edges=[]; trees={}; readers={}; failures=[]; containers=[]
    for asset in env.assets:
        externals=[{'path':x.path,'type':x.type} for x in asset.externals]
        containers.append({'name':asset.name,'unityVersion':asset.unity_version,'objects':len(asset.objects),'externals':externals})
    for obj in env.objects:
        container=obj.assets_file.name;oid=f'{container}:{obj.path_id}';typ=obj.type.name
        raw=obj.get_raw_data();raw_path=local/'raw-objects'/container/f'{obj.path_id}_{typ}.bin';raw_path.parent.mkdir(parents=True,exist_ok=True);raw_path.write_bytes(raw)
        rec={'id':oid,'container':container,'pathID':obj.path_id,'type':typ,'bytes':obj.byte_size,'sha256':hashlib.sha256(raw).hexdigest(),'name':'','treeStatus':'','exportStatus':'metadata','exportFiles':[]}
        try:
            node=obj._get_typetree_node()
            if typ=='MonoBehaviour':
                # Generator 0.0.10 omits alignment after the Unity 4.x base UInt8.
                # Use the versioned base schema and retain generated game fields.
                # A fresh node is required: the native reader caches node flags.
                from UnityPy.helpers.TypeTreeNode import TypeTreeNode
                from UnityPy.helpers.Tpk import get_typetree_node
                base_node=get_typetree_node(114,obj.version)
                assert [c.m_Name for c in node.m_Children[:4]]==[c.m_Name for c in base_node.m_Children]
                node=TypeTreeNode(m_Level=0,m_Type=node.m_Type,m_Name=node.m_Name,m_ByteSize=node.m_ByteSize,m_Version=node.m_Version,m_MetaFlag=node.m_MetaFlag,m_Children=base_node.m_Children+node.m_Children[4:])
            tree=obj.read_typetree(nodes=node)
            if typ=='MonoBehaviour':
                base=obj.read_typetree(nodes=base_node,check_read=False)
                assert tree['m_Script']==base['m_Script'], 'MonoScript pointer differs from original base schema'
            trees[oid]=compact(tree);readers[oid]=obj;rec['name']=tree.get('m_Name','');rec['treeStatus']='ok'
            write(local/'typetrees'/container/f'{obj.path_id}_{typ}.json',compact(tree))
            for field,file_id,path_id in pointers(tree):
                dst_container=container if file_id==0 else Path(obj.assets_file.externals[file_id-1].path.replace('\\','/')).name if 0<file_id<=len(obj.assets_file.externals) else f'unknown-file-{file_id}'
                edges.append({'from':oid,'field':field,'to':f'{dst_container}:{path_id}'})
            if typ in ('Texture2D','Sprite'): rec['width']=tree.get('m_Width',tree.get('m_Rect',{}).get('width'));rec['height']=tree.get('m_Height',tree.get('m_Rect',{}).get('height'))
            if typ=='MonoScript':rec.update({k:tree.get(k) for k in ('m_ClassName','m_Namespace','m_AssemblyName','m_ExecutionOrder')})
        except Exception as ex:
            rec['treeStatus']='failed';rec['treeError']=str(ex);failures.append({'id':oid,'stage':'typetree','error':str(ex)})
        records.append(rec)
    known={r['id'] for r in records}
    for edge in edges: edge['resolved']=edge['to'] in known
    byid={r['id']:r for r in records}
    exporters={'Texture2D':extractor.exportTexture2D,'Sprite':export_legacy_sprite,'AudioClip':extractor.exportAudioClip,'Mesh':extractor.exportMesh,'Font':extractor.exportFont,'TextAsset':extractor.exportTextAsset,'Shader':export_legacy_shader}
    for i,rec in enumerate(records):
        typ=rec['type'];oid=rec['id']
        if typ not in exporters or oid not in readers:continue
        name=re.sub(r'[^\w .-]','_',rec['name']).strip(' .')[:120] or 'unnamed';out=local/'exports'/typ/rec['container']/f"{rec['pathID']}_{name}"
        out.parent.mkdir(parents=True,exist_ok=True)
        try:
            exporters[typ](readers[oid],str(out))
            outputs=[p for p in out.parent.glob(out.name+'*') if p.is_file()]
            if out.is_dir():outputs+=list(out.rglob('*'))
            outputs=[p for p in outputs if p.is_file() and p.stat().st_size>0]
            rec['exportFiles']=[{'path':p.relative_to(project).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in outputs]
            rec['exportStatus']='ok' if outputs else 'empty'
        except Exception as ex:
            rec['exportStatus']='failed';rec['exportError']=str(ex);failures.append({'id':oid,'stage':'export','error':str(ex)})
        if i%100==0:print(f'Exported through object {i}/{len(records)}',flush=True)
    hierarchy=[];behaviours=[];settings={};resource_map={};texts=[]
    for oid,tree in trees.items():
        rec=byid[oid];typ=rec['type']
        if typ=='GameObject':
            components=[e for e in edges if e['from']==oid and 'm_Component' in e['field']]
            entry={'id':oid,'name':rec['name'],'active':tree.get('m_IsActive'),'components':[{'id':e['to'],'type':byid.get(e['to'],{}).get('type')} for e in components]}
            trans=next((trees.get(c['id']) for c in entry['components'] if c['type']=='Transform'),None)
            if trans:
                entry.update({'position':trans.get('m_LocalPosition'),'rotation':trans.get('m_LocalRotation'),'scale':trans.get('m_LocalScale'),'father':trans.get('m_Father'),'children':trans.get('m_Children')})
            hierarchy.append(entry)
        if typ=='MonoBehaviour':
            script_edge=next((e for e in edges if e['from']==oid and e['field']=='.m_Script'),None)
            script=byid.get(script_edge['to'],{}) if script_edge else {}
            behaviours.append({'id':oid,'class':script.get('m_ClassName'),'script':script_edge['to'] if script_edge else None,'fields':compact(tree)})
        if typ.endswith('Settings') or typ in ('BuildSettings','InputManager','TagManager','ResourceManager','Camera','TimeManager','AudioManager','PlayerSettings'):settings[oid]=compact(tree)
        if typ=='ResourceManager':resource_map[oid]=compact(tree)
        if typ=='TextAsset':texts.append({'id':oid,'name':rec['name'],'bytes':rec['bytes'],'exports':rec['exportFiles']})
    write(project/'evidence/objects.json',records);write(project/'evidence/dependencies.json',edges);write(project/'evidence/containers.json',containers)
    write(local/'scene-hierarchy.json',hierarchy);write(local/'behaviours.json',behaviours);write(local/'settings.json',settings);write(local/'resource-map.json',resource_map);write(local/'text-assets.json',texts)
    write(project/'evidence/asset-summary.json',{'tool':f'UnityPy {UnityPy.__version__}; TypeTreeGeneratorAPI 0.0.10; legacy Sprite/Shader exporters','fileCount':len(manifest),'buildBytes':sum(x['bytes'] for x in manifest),'copiesMatch':all(x['copyMatches'] for x in manifest),'objectCount':len(records),'types':dict(collections.Counter(r['type'] for r in records)),'treeStatuses':dict(collections.Counter(r['treeStatus'] for r in records)),'exportStatuses':{t:dict(collections.Counter(r['exportStatus'] for r in records if r['type']==t)) for t in exporters},'edges':len(edges),'unresolvedEdges':sum(not e['resolved'] for e in edges),'failures':failures})
    print(json.dumps({'objects':len(records),'errors':len(failures),'types':dict(collections.Counter(r['type'] for r in records))},ensure_ascii=False))

if __name__=='__main__':main()
