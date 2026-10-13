"""Isolate native MonoScript binding from external-file resolution."""
import argparse,copy,json
from pathlib import Path
import UnityPy
from native_bundle import write_fs,digest

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,required=True);a=ap.parse_args()
    local=a.project.resolve()/'local';source=local/'NativeRuntime';target=local/'NativeRuntimeLocalScriptProbe';target.mkdir(exist_ok=True)
    env=UnityPy.load(str(source/'serialized/resources.assets'));asset=env.assets[0]
    glob=UnityPy.load(str(local/'NativeRuntimeABBinding/serialized/globalgamemanagers.assets')).assets[0]
    script_raw=glob.objects[2325].get_raw_data()
    script=copy.copy(glob.objects[2325]);script.assets_file=asset;script.path_id=max(asset.objects)+1;script.set_raw_data(script_raw)
    script.serialized_type=copy.copy(script.serialized_type);script.type_id=len(asset.types)
    asset.types.append(script.serialized_type);asset.objects[script.path_id]=script
    config=asset.objects[76721];tree=config.read_typetree();tree['m_Script']={'m_FileID':0,'m_PathID':script.path_id};config.save_typetree(tree)
    st=asset.script_types[config.serialized_type.script_type_index];st.local_serialized_file_index=0;st.local_identifier_in_file=script.path_id
    ab=asset.objects[1];tree=ab.read_typetree();pointer={'m_FileID':0,'m_PathID':script.path_id}
    tree['m_Container'].append(('debug/monoscript',{'preloadIndex':len(tree['m_PreloadTable']),'preloadSize':1,'asset':pointer.copy()}));tree['m_PreloadTable'].append(pointer)
    ab.save_typetree(tree);root=target/'resources.assets';root.write_bytes(asset.save())
    manifest=json.loads((source/'manifest.json').read_text(encoding='utf-8'))
    files=[(r['container'],root if r['container']=='resources.assets' else source/'serialized'/r['container'],4) for r in manifest['containers']]
    write_fs(target/'duckov-native-assets.bundle',files,engine='2022.3.62f3c1');print('Local script probe created',flush=True)

if __name__=='__main__':main()
