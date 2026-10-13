"""Merge player asset files into one native AB serialized file.

Unity's regular AB loader binds scripts successfully when the MonoScript and
component share its serialized file. All pointer relocations and payload hashes
are recorded. Compiled shader and other native data stays in its native format.
"""
import argparse,copy,gc,hashlib,json,struct
from pathlib import Path
import UnityPy
from native_bundle import write_fs,digest

def sha(raw):return hashlib.sha256(raw).hexdigest()

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);a=ap.parse_args()
    local=a.project.resolve()/'local';source=local/'NativeRuntime';target=a.output.resolve();target.mkdir(exist_ok=True)
    manifest=json.loads((source/'manifest.json').read_text(encoding='utf-8'));names=[r['container'] for r in manifest['containers']]
    envs={n:UnityPy.load(str(source/'serialized'/n)) for n in names};assets={n:e.assets[0] for n,e in envs.items()}
    root=copy.copy(assets['resources.assets']);root.objects={};root.types=[];root.script_types=[];root.ref_types=[];root.externals=[]
    ids={(n,pid):(pid if n=='resources.assets' else index*1000000+pid) for index,n in enumerate(names) for pid in assets[n].objects if not(n=='resources.assets' and pid==1)}
    external_paths=[];external_template=assets['resources.assets'].externals[0]
    changes=[];all_records=[];diagnostics=[]
    def pointer(value,current):
        pid=value['m_PathID'];fid=value['m_FileID']
        if not pid:return value
        file=current if fid==0 else Path(assets[current].externals[fid-1].path).name
        if (file,pid) in ids:return {'m_FileID':0,'m_PathID':ids[(file,pid)]}
        path=assets[current].externals[fid-1].path if fid else current
        if path not in external_paths:
            external_paths.append(path);ext=copy.copy(external_template);ext.path=path;root.externals.append(ext)
        return {'m_FileID':external_paths.index(path)+1,'m_PathID':pid}
    def rewrite(value,current):
        if isinstance(value,dict):
            if set(value)=={'m_FileID','m_PathID'}:return pointer(value,current)
            return {k:rewrite(v,current) for k,v in value.items()}
        if isinstance(value,list):return [rewrite(v,current) for v in value]
        if isinstance(value,tuple):return tuple(rewrite(v,current) for v in value)
        return value
    for index,name in enumerate(names):
        asset=assets[name];type_offset=len(root.types);script_offset=len(root.script_types)
        for st in asset.script_types:
            st=copy.copy(st);pt=pointer({'m_FileID':st.local_serialized_file_index,'m_PathID':st.local_identifier_in_file},name)
            st.local_serialized_file_index=pt['m_FileID'];st.local_identifier_in_file=pt['m_PathID'];root.script_types.append(st)
        for t in asset.types:
            t=copy.copy(t)
            if t.script_type_index>=0:t.script_type_index+=script_offset
            root.types.append(t)
        for pid,obj in asset.objects.items():
            if name=='resources.assets' and pid==1:continue
            raw=obj.get_raw_data();modified=raw;reason=[]
            try:
                tree=obj.read_typetree();updated=rewrite(tree,name)
                if obj.type.name=='MonoScript' and updated.get('m_AssemblyName') and not updated['m_AssemblyName'].endswith('.dll'):
                    updated['m_AssemblyName']+='.dll';reason.append('player assembly name to AB assembly name')
                if updated!=tree:
                    # Preserve object framing and all non-reference fields; record full hashes.
                    clone=copy.copy(obj);clone.set_raw_data(raw);modified=clone.save_typetree(updated)
                    reason.append('PPtr relocation into single serialized file')
            except Exception as ex:
                # The known Shapes editor-only state lacks its editor assembly. Keep its
                # unknown tail and relocate only validated native MB header pointers.
                if name=='resources.assets' and pid==76733 and obj.type.name=='MonoBehaviour':
                    header=obj.read_typetree(nodes=UnityPy.helpers.Tpk.get_typetree_node(114,obj.version),check_read=False)
                    b=bytearray(raw)
                    for offset,key in [(0,'m_GameObject'),(16,'m_Script')]:
                        old=header[key];assert struct.unpack_from('<iq',raw,offset)==(old['m_FileID'],old['m_PathID'])
                        new=pointer(old,name);struct.pack_into('<iq',b,offset,new['m_FileID'],new['m_PathID'])
                    modified=bytes(b);reason=['only native MB header pointers relocated; unknown editor-only tail preserved']
                    diagnostics.append({'object':name+':'+str(pid),'error':str(ex),'status':'editor-only unresolved type retained'})
                else:raise RuntimeError(name+':'+str(pid)+' cannot safely relocate: '+str(ex)) from ex
            clone=copy.copy(obj);clone.assets_file=root;clone.path_id=ids[(name,pid)];clone.type_id=obj.type_id+type_offset
            clone.serialized_type=root.types[clone.type_id];clone.set_raw_data(modified);root.objects[clone.path_id]=clone
            record={'object':name+':'+str(pid),'newPathID':clone.path_id,'before':sha(raw),'after':sha(modified)}
            all_records.append(record)
            if raw!=modified:changes.append(dict(record,reasons=reason))
        print(f'Merged {index+1}/{len(names)} {name}',flush=True)
        gc.collect()
    directory_source=assets['resources.assets'].objects[1]
    ab=copy.copy(directory_source);ab.assets_file=root;ab.path_id=1;ab.type_id=len(root.types);ab.serialized_type=copy.copy(directory_source.serialized_type)
    root.types.append(ab.serialized_type);root.objects[1]=ab
    expose=[(r['bundleKey'],r['object']) for r in manifest['resourceMap']]
    for n,asset in assets.items():
        expose.extend(('objects/'+n.lower()+':'+str(pid),n+':'+str(pid)) for pid,o in asset.objects.items() if o.type.name in ['Shader','ComputeShader'])
    expose.extend(('objects/'+oid.lower(),oid) for oid in ['globalgamemanagers.assets:5150','globalgamemanagers.assets:5151'])
    preload=[];script_count=sum(o.type.name=='MonoScript' for asset in assets.values() for o in asset.objects.values());entries=[];seen=set()
    for key,oid in expose:
        if key in seen:continue
        seen.add(key);n,pid=oid.rsplit(':',1);pt={'m_FileID':0,'m_PathID':ids[(n,int(pid))]}
        entries.append((key,{'preloadIndex':len(preload),'preloadSize':1,'asset':pt.copy()}));preload.append(pt)
    tree=directory_source.read_typetree();tree.update(m_Name='duckov-native-assets',m_AssetBundleName='duckov-native-assets',m_Dependencies=[],m_PreloadTable=preload,m_Container=entries)
    ab.save_typetree(tree);root.name='duckov-native-assets';root.unity_version='2022.3.62f3c1'
    output=target/'duckov-native.assets';output.write_bytes(root.save())
    # ObjectReader.get_raw_data() reads the source reader even after edits. Verify
    # actual persisted bytes by reopening the output, rather than reading the writer.
    reopened=UnityPy.load(str(output)).assets[0]
    assert all(sha(reopened.objects[r['newPathID']].get_raw_data())==r['after'] for r in all_records)
    bundle=target/'duckov-native-assets.bundle';write_fs(bundle,[('duckov-native-assets',output,4)],engine='2022.3.62f3c1')
    proof={'source':str(source),'objects':len(all_records),'relocatedPointersOrScriptNames':len(changes),'unchangedPayloads':len(all_records)-len(changes),
        'resourceRoots':len(manifest['resourceMap']),'bundleEntries':len(entries),'localMonoScripts':script_count,'persistedObjectHashesVerified':True,'partialTypes':diagnostics,
        'bundle':{'bytes':bundle.stat().st_size,'sha256':digest(bundle)},'changes':changes,'objectMap':all_records}
    (target/'manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8');print('Merged native asset bundle ready',flush=True)

if __name__=='__main__':main()
