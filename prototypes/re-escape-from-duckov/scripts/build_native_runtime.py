"""Preserve original player objects and rebuild native bundle directories/metadata."""
import argparse, copy, gc, hashlib, json, sqlite3, sys
from pathlib import Path
import UnityPy
from UnityPy.helpers.Tpk import get_typetree_node
from native_bundle import write_fs, digest
from analyze_assets import corrected_tree

def fill_types(asset, generator, diagnostics):
    asset._enable_type_tree=True
    representatives={o.type_id:o for o in asset.objects.values()};cache={}
    for tid,t in enumerate(asset.types):
        if t.node:continue
        o=representatives[tid]
        if o.type.name=='MonoBehaviour':
            try:
                corrected_tree(o,cache);t.node=cache[(asset.name,tid)]
            except Exception as ex:
                t.node=cache.get((asset.name,tid),get_typetree_node(114,o.version))
                diagnostics.append({'container':asset.name,'typeIndex':tid,'object':o.path_id,'error':str(ex),'status':'partial-type-tree; original object body retained'})
        else:t.node=o._get_typetree_node()
        for i,node in enumerate(t.node.traverse()):
            if node.m_Index is None:node.m_Index=i
            if node.m_TypeFlags is None:node.m_TypeFlags=0
            if node.m_RefTypeHash is None:node.m_RefTypeHash=0
        if t.type_dependencies is None:t.type_dependencies=()
    # Stripped files contain a null managed-reference sentinel with no node/names.
    # Preserve the descriptor and explicitly encode the empty sentinel metadata.
    from UnityPy.helpers.TypeTreeNode import TypeTreeNode
    for t in asset.ref_types or []:
        if t.node is None:
            assert t.old_type_hash==bytes(16) and t.class_id==-1, 'Unknown non-null managed reference descriptor'
            t.node=TypeTreeNode(m_Level=0,m_Type='ReferencedObject',m_Name='Base',m_ByteSize=0,m_Version=1,m_Index=0,m_TypeFlags=0,m_MetaFlag=0,m_RefTypeHash=0,m_Children=[])
            t.m_ClassName=t.m_ClassName or '';t.m_NameSpace=t.m_NameSpace or '';t.m_AssemblyName=t.m_AssemblyName or ''
            diagnostics.append({'container':asset.name,'status':'null managed-reference descriptor has an explicit empty sentinel node'})

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,required=True);a=ap.parse_args()
    project=a.project.resolve();local=project/'local';data=local/'build-copy/Duckov_Data'
    sys.path.insert(0,str(local/'tools/python'))
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    gen=TypeTreeGenerator('2022.3.62f2');gen.load_local_game(str(local/'build-copy'))
    db=sqlite3.connect('file:'+str(local/'assets.sqlite')+'?mode=ro',uri=True)
    work=local/'NativeRuntime';work.mkdir(exist_ok=True);metadata=work/'serialized';metadata.mkdir(exist_ok=True)
    containers={r[0]:r[1] for r in db.execute('SELECT name,path FROM containers')}
    names=['resources.assets']+sorted(n for n in containers if n.endswith('.assets') and n!='resources.assets')
    seed=UnityPy.load(str(local/'NativeProbe/seed-assets')).assets[0]
    directory_source=next(o for o in seed.objects.values() if o.type.name=='AssetBundle')
    # Include every true Resources root, including prefabs, and all render-pipeline entry objects.
    resource_entries=[(key,oid) for key,oid in db.execute('SELECT key,object_id FROM resources') if not oid.endswith(':0')]
    expose=[('resources/'+key.lower(),oid) for key,oid in resource_entries]
    expose += [('objects/'+r[0].lower(),r[0]) for r in db.execute('SELECT id FROM objects WHERE type IN ("Shader","ComputeShader") OR id IN ("globalgamemanagers.assets:5150","globalgamemanagers.assets:5151")')]
    root_externals=[];entries=[];pointers=[];seen=set()
    for key,oid in expose:
        if key in seen:continue
        seen.add(key);container,pid=oid.rsplit(':',1)
        if container=='resources.assets':fileid=0
        else:
            if container not in root_externals:root_externals.append(container)
            fileid=root_externals.index(container)+1
        pointer={'m_FileID':fileid,'m_PathID':int(pid)}
        pointers.append(pointer);entries.append((key,{'preloadIndex':len(pointers)-1,'preloadSize':1,'asset':pointer.copy()}))
    # Preserve the original external-file indices first, then add any new directory targets.
    original_root=UnityPy.load(str(data/'resources.assets')).assets[0]
    base_externals=[Path(e.path).name for e in original_root.externals]
    extra=[n for n in root_externals if n not in base_externals]
    all_externals=base_externals+extra
    for pointer in pointers:
        if pointer['m_FileID']:pointer['m_FileID']=all_externals.index(root_externals[pointer['m_FileID']-1])+1
    for _,entry in entries:
        if entry['asset']['m_FileID']:entry['asset']['m_FileID']=all_externals.index(root_externals[entry['asset']['m_FileID']-1])+1
    del original_root
    diagnostics=[];records=[];payloads=[];remap=None
    for index,name in enumerate(names):
        env=UnityPy.load(str(data/containers[name]));env.typetree_generator=gen
        asset=next(x for x in env.assets if x.name==name)
        before={pid:hashlib.sha256(o.get_raw_data()).hexdigest() for pid,o in asset.objects.items()}
        changed={};added=[]
        if name=='resources.assets':
            original=asset.objects.pop(1);remap=max(asset.objects)+2;original.path_id=remap;asset.objects[remap]=original
            before[remap]=before.pop(1)
            # Original material #1 is used only by this TMP SpriteAsset in asset containers.
            mono=asset.objects[76401];mono_cache={};tree,_=corrected_tree(mono,mono_cache)
            assert tree['material']=={'m_FileID':0,'m_PathID':1}
            tree['material']['m_PathID']=remap;mono.save_typetree(tree,nodes=mono_cache[(asset.name,mono.type_id)])
            changed[76401]={'reason':'original material pathID 1 relocated to reserve native AssetBundle directory ID 1','oldMaterialID':1,'newMaterialID':remap}
            for container in extra:
                ext=copy.copy(asset.externals[0]);ext.path=container;asset.externals.append(ext)
            ab=copy.copy(directory_source);ab.assets_file=asset;ab.path_id=1;ab.type_id=len(asset.types);ab.serialized_type=copy.copy(directory_source.serialized_type)
            asset.types.append(ab.serialized_type);asset.objects[1]=ab;added.append(1)
            directory=directory_source.read_typetree();directory.update(m_Name='duckov-native-assets',m_AssetBundleName='duckov-native-assets',m_Dependencies=[],m_PreloadTable=pointers,m_Container=entries)
            ab.save_typetree(directory)
        for external in asset.externals:
            if external.path.endswith('.assets'):external.path='archive:/resources.assets/'+Path(external.path).name
        fill_types(asset,gen,diagnostics);asset.unity_version='2022.3.62f3c1'
        raw=asset.save();check=UnityPy.load(raw).assets[0]
        after={pid:hashlib.sha256(o.get_raw_data()).hexdigest() for pid,o in check.objects.items() if pid not in added}
        expected={pid:h for pid,h in before.items() if pid not in changed}
        assert expected=={pid:h for pid,h in after.items() if pid not in changed},name+' changed unapproved original payloads'
        target=metadata/name
        if target.exists():raise FileExistsError(target)
        target.write_bytes(raw);payloads.append((name,target,4))
        records.append({'container':name,'objects':len(before),'unchangedOriginalPayloads':len(expected),'changes':changed,'appendedDirectoryObjects':added,'bytes':len(raw),'sha256':digest(target)})
        print(f'Native serialization {index+1}/{len(names)} {name}: {len(before)} original objects; changes={len(changed)}',flush=True)
        del env,asset,raw,check;gc.collect()
    target=work/'duckov-native-assets.bundle';write_fs(target,payloads,engine='2022.3.62f3c1')
    manifest={'unity':'2022.3.62f3c1','originalUnity':'2022.3.62f2','containers':records,'partialTypeTrees':diagnostics,
        'resourceRoots':len(resource_entries),'bundleEntries':len(entries),'resourceMap':[{'key':key,'object':oid,'bundleKey':'resources/'+key.lower()} for key,oid in resource_entries],
        'originalStreams':[{'file':f.name,'bytes':f.stat().st_size,'sha256':digest(f)} for f in data.glob('*.resS')],
        'materialPathIDRemap':{'container':'resources.assets','old':1,'new':remap},
        'bundle':{'path':str(target),'bytes':target.stat().st_size,'sha256':digest(target)}}
    (work/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Native runtime asset bundle complete',target,flush=True)

if __name__=='__main__':main()
