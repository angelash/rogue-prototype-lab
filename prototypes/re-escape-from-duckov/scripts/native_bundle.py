"""Wrap unchanged player serialized files in an uncompressed UnityFS archive.

This is a local interoperability experiment, not an AssetBundle conversion guarantee.
Only a Unity-generated AssetBundle directory object is rewritten; original payload
files are copied byte-for-byte and their hashes are recorded separately.
"""
import argparse, copy, hashlib, json, struct
from pathlib import Path
import UnityPy

def digest(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(4*1024*1024),b''):h.update(b)
    return h.hexdigest()

def write_fs(output, files, engine='2022.3.62f2'):
    # UnityFS v7, uncompressed blocks and directory at the front, 16-byte alignment.
    sizes=[len(v) if isinstance(v,bytes) else v.stat().st_size for _,v,_ in files]
    total=sum(sizes); block_limit=16*1024*1024
    blocks=[min(block_limit,total-i) for i in range(0,total,block_limit)]
    info=bytearray(bytes(16)+struct.pack('>I',len(blocks)))
    for n in blocks:info.extend(struct.pack('>IIH',n,n,0))
    info.extend(struct.pack('>I',len(files))); offset=0
    for (name,_,flags),size in zip(files,sizes):
        info.extend(struct.pack('>qqI',offset,size,flags));info.extend(name.encode()+b'\0');offset+=size
    prefix=b'UnityFS\0'+struct.pack('>I',7)+b'5.x.x\0'+engine.encode()+b'\0'
    header_size=len(prefix)+20; aligned=(header_size+15)//16*16
    header=prefix+struct.pack('>qIII',aligned+len(info)+total,len(info),len(info),64)
    with output.open('xb') as out:
        out.write(header);out.write(bytes(aligned-len(header)));out.write(info)
        for _,value,_ in files:
            if isinstance(value,bytes):out.write(value)
            else:
                with value.open('rb') as f:
                    for b in iter(lambda:f.read(4*1024*1024),b''):out.write(b)

def probe(project, retag=False, types=False, plain=False, merged=False, native_only=False, shader_id=None, archive_externals=False):
    local=project/'local'; root=local/'NativeProbe'; data=local/'build-copy/Duckov_Data'
    seed=UnityPy.load(str(root/'seed-assets')); wrapper=seed.assets[0]
    bundle=next(x for x in wrapper.objects.values() if x.type.name=='AssetBundle')
    wrapper.objects={bundle.path_id:bundle}
    ext=copy.deepcopy(wrapper.externals[0]);ext.path='resources.assets' if plain else 'archive:/duckov-native-probe/resources.assets';ext.type=0
    wrapper.externals=[ext]
    # Select exact original objects with native GPU/mesh/animation data.
    source=UnityPy.load(str(data/'resources.assets')).assets[0]
    selected=[]
    for kind in ['Texture2D','Shader','Mesh','AnimationClip']:
        for obj in source.objects.values():
            if obj.type.name!=kind:continue
            if kind=='Shader' and shader_id is not None and obj.path_id!=shader_id:continue
            tree=obj.read_typetree()
            if kind=='Texture2D' and (tree['m_Width']<=16 or tree['m_Height']<=16):continue
            if kind=='Shader' and not tree.get('m_ParsedForm',{}).get('m_Name'):continue
            selected.append((kind,obj.path_id,tree.get('m_Name') or tree.get('m_ParsedForm',{}).get('m_Name')));break
    t=bundle.read_typetree();t.update(m_Name='duckov-native-probe',m_AssetBundleName='duckov-native-probe',m_Dependencies=[],
        m_PreloadTable=[{'m_FileID':1,'m_PathID':pid} for _,pid,_ in selected],
        m_Container=[('native/'+kind.lower(),{'preloadIndex':i,'preloadSize':1,'asset':{'m_FileID':1,'m_PathID':pid}}) for i,(kind,pid,_) in enumerate(selected)])
    bundle.save_typetree(t)
    payload_names=['resources.assets','resources.assets.resS','globalgamemanagers.assets','sharedassets0.assets']
    files=[] if merged else [('duckov-native-probe',wrapper.save(),4)]; changes=[]
    generator=None
    if types:
        import sys
        sys.path.insert(0,str(local/'tools/python'))
        from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
        generator=TypeTreeGenerator('2022.3.62f2');generator.load_local_game(str(local/'build-copy'))
        from analyze_assets import corrected_tree
    for n in payload_names:
        if (retag or types) and not n.endswith('.resS'):
            env=UnityPy.load(str(data/n));env.typetree_generator=generator;a=env.assets[0]
            before={pid:hashlib.sha256(o.get_raw_data()).hexdigest() for pid,o in a.objects.items()}
            appended=None
            if merged and n=='resources.assets':
                ab=copy.copy(bundle);ab.assets_file=a;ab.path_id=1 if native_only else max(a.objects)+1
                ab.type_id=len(a.types);ab.serialized_type=copy.copy(bundle.serialized_type)
                a.types.append(ab.serialized_type);a.objects[ab.path_id]=ab
                directory=copy.deepcopy(t)
                for pointer in directory['m_PreloadTable']:pointer['m_FileID']=0
                for _,entry in directory['m_Container']:entry['asset']['m_FileID']=0
                ab.save_typetree(directory);appended=ab.path_id
            original_version=a.unity_version
            if archive_externals:
                for external in a.externals:
                    if external.path.endswith('.assets'):external.path='archive:/resources.assets/'+Path(external.path).name
            if retag:a.unity_version='2022.3.62f3c1'
            if native_only:
                wanted=({pid for _,pid,_ in selected}|{appended}) if n=='resources.assets' else {pid for pid,o in a.objects.items() if o.type.name not in ('MonoBehaviour','MonoScript')}
                a.objects={pid:o for pid,o in a.objects.items() if pid in wanted}
                before={pid:h for pid,h in before.items() if pid in wanted and pid!=appended}
                retained=[]
                for o in a.objects.values():
                    if o.serialized_type not in retained:retained.append(o.serialized_type)
                    o.type_id=retained.index(o.serialized_type)
                a.types=retained;a.script_types=[];a.ref_types=[]
            failures=[]
            if types:
                cache={};a._enable_type_tree=True
                for tid,t in enumerate(a.types):
                    if t.node:continue
                    representative=next(o for o in a.objects.values() if o.type_id==tid)
                    if representative.type.name=='MonoBehaviour':
                        try:
                            corrected_tree(representative,cache)
                            t.node=cache[(a.name,tid)]
                        except Exception as ex:
                            from UnityPy.helpers.Tpk import get_typetree_node
                            t.node=get_typetree_node(114,representative.version)
                            failures.append({'typeIndex':tid,'object':representative.path_id,'error':str(ex)})
                    else:t.node=representative._get_typetree_node()
                    for index,node in enumerate(t.node.traverse()):
                        if node.m_Index is None:node.m_Index=index
                        if node.m_TypeFlags is None:node.m_TypeFlags=0
                        if node.m_RefTypeHash is None:node.m_RefTypeHash=0
                    if t.type_dependencies is None:t.type_dependencies=()
            raw=a.save();reloaded=UnityPy.load(raw).assets[0]
            after={pid:hashlib.sha256(o.get_raw_data()).hexdigest() for pid,o in reloaded.objects.items()}
            if appended is not None:after.pop(appended)
            assert before==after, 'Retag changed an original object payload'
            files.append((n,raw,4));changes.append({'file':n,'originalVersion':original_version,'metadataVersion':a.unity_version,'unchangedObjectPayloads':len(before),'typeTrees':types,'typeTreeFailures':failures})
        else:files.append((n,data/n,0 if n.endswith('.resS') else 4))
    variant=('typed-retagged' if types and retag else 'typed' if types else 'retagged' if retag else 'original')+('-plain' if plain else '')+('-merged' if merged else '')+('-native-dependencies' if native_only else '')+('-shader'+str(shader_id) if shader_id is not None else '')+('-archive' if archive_externals else '')
    target=root/('original-resources-'+variant+'.bundle' if variant!='original' else 'original-resources.bundle');write_fs(target,files,engine='2022.3.62f3c1' if retag else '2022.3.62f2')
    manifest={'experiment':'unchanged serialized player files in UnityFS','selected':selected,
              'payloads':[{'file':n,'bytes':(data/n).stat().st_size,'sha256':digest(data/n)} for n in payload_names],
              'versionMetadataChanges':changes,
              'bundle':{'path':str(target),'bytes':target.stat().st_size,'sha256':digest(target)}}
    (root/(variant+'-payload-manifest.json' if variant!='original' else 'payload-manifest.json')).write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'selected':selected,'bundleBytes':target.stat().st_size}),flush=True)

if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,required=True)
    ap.add_argument('--retag-version',action='store_true')
    ap.add_argument('--add-type-trees',action='store_true')
    ap.add_argument('--plain-external-path',action='store_true')
    ap.add_argument('--merge-directory',action='store_true')
    ap.add_argument('--native-only',action='store_true')
    ap.add_argument('--shader-id',type=int)
    ap.add_argument('--archive-externals',action='store_true')
    args=ap.parse_args();probe(args.project.resolve(),args.retag_version,args.add_type_trees,args.plain_external_path,args.merge_directory,args.native_only,args.shader_id,args.archive_externals)
