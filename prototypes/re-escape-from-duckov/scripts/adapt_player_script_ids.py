"""Relink old serialized script identities to the local Unity Player registry.

The source type-tree/layout hashes and every original object payload are retained.
Only script identity metadata is adapted to the local compiler's registry.
"""
import argparse,collections,copy,json
from pathlib import Path
import UnityPy
from native_bundle import write_fs,digest

def hash_bytes(value):return bytes(value['bytes['+str(i)+']'] for i in range(16))
def key(tree):return (tree['m_AssemblyName'].removesuffix('.dll'),tree['m_Namespace'],tree['m_ClassName'])
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,required=True);ap.add_argument('--source',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);a=ap.parse_args()
    local=a.project.resolve()/'local';player=local/'NativeProbe/Player/DuckovNativeProbe_Data'
    manager=UnityPy.load(str(player/'globalgamemanagers')).assets[0]
    hashes=next(o for o in manager.objects.values() if o.type.name=='MonoManager').read_typetree()['m_ScriptHashes']
    by_hash=collections.defaultdict(list)
    for identity,layout in hashes:by_hash[hash_bytes(layout)].append(hash_bytes(identity))
    globals=UnityPy.load(str(player/'globalgamemanagers.assets')).assets[0];identities={};unresolved=[]
    for obj in globals.objects.values():
        if obj.type.name!='MonoScript':continue
        tree=obj.read_typetree();matches=by_hash[hash_bytes(tree['m_PropertiesHash'])]
        if len(matches)==1:identities[key(tree)]=matches[0]
    source=a.source.resolve();target=a.output.resolve();target.mkdir(exist_ok=True)
    asset=UnityPy.load(str(source/'duckov-native.assets')).assets[0];changes=[]
    for index,t in enumerate(asset.types):
        if t.script_type_index<0 or not t.script_id:continue
        st=asset.script_types[t.script_type_index]
        if st.local_serialized_file_index!=0:raise RuntimeError('Merged script reference is not local')
        script=asset.objects[st.local_identifier_in_file];tree=script.read_typetree();identity=identities.get(key(tree))
        if identity is None:
            unresolved.append({'typeIndex':index,'class':key(tree),'scriptID':t.script_id.hex()});continue
        if t.script_id!=identity:
            changes.append({'typeIndex':index,'class':key(tree),'before':t.script_id.hex(),'after':identity.hex()});t.script_id=identity
    raw=target/'duckov-native.assets';raw.write_bytes(asset.save())
    bundle=target/'duckov-native-assets.bundle';write_fs(bundle,[('duckov-native-assets',raw,4)],engine='2022.3.62f3c1')
    proof={'metadataOnly':True,'identitiesAvailable':len(identities),'changes':changes,'unresolved':unresolved,
        'bundle':{'bytes':bundle.stat().st_size,'sha256':digest(bundle)}}
    (target/'script-identities.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Native script identity relinks:',len(changes),'unresolved types:',len(unresolved),flush=True)

if __name__=='__main__':main()
