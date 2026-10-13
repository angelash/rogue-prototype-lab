"""Adapt player MonoScript assembly names to Unity's AssetBundle representation.

Keeps game assemblies and component payloads intact. Every changed MonoScript
object and both hashes are recorded; never rewrites the source bundle.
"""
import argparse,hashlib,json
from pathlib import Path
import UnityPy
from native_bundle import write_fs,digest

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,required=True);ap.add_argument('--rename-files',action='store_true');ap.add_argument('--preload-scripts',action='store_true');ap.add_argument('--debug-script',action='store_true');a=ap.parse_args()
    suffix=('Unique' if a.rename_files else '')+('Preload' if a.preload_scripts else '')+('Debug' if a.debug_script else '')
    local=a.project.resolve()/'local';source=local/'NativeRuntime';target=local/('NativeRuntimeABBinding'+suffix)
    target.mkdir(exist_ok=True);metadata=target/'serialized';metadata.mkdir(exist_ok=True)
    manifest=json.loads((source/'manifest.json').read_text(encoding='utf-8'));changes=[];files=[]
    for record in manifest['containers']:
        name=record['container'];p=source/'serialized'/name
        if name=='globalgamemanagers.assets' or a.rename_files or (name=='resources.assets' and (a.preload_scripts or a.debug_script)):
            env=UnityPy.load(str(p));asset=env.assets[0]
            for obj in asset.objects.values():
                if obj.type.name!='MonoScript':continue
                tree=obj.read_typetree();assembly=tree['m_AssemblyName']
                if not assembly or assembly.endswith('.dll'):continue
                old=hashlib.sha256(obj.get_raw_data()).hexdigest()
                tree['m_AssemblyName']=assembly+'.dll';updated_raw=obj.save_typetree(tree)
                changes.append({'object':name+':'+str(obj.path_id),'assemblyBefore':assembly,'assemblyAfter':tree['m_AssemblyName'],
                    'before':old,'after':hashlib.sha256(updated_raw).hexdigest()})
            if a.rename_files:
                for ext in asset.externals:
                    if ext.path.startswith('archive:/resources.assets/'):
                        ext.path='archive:/duckov-resources.assets/duckov-'+Path(ext.path).name
            if name=='resources.assets' and a.preload_scripts:
                glob=UnityPy.load(str(source/'serialized/globalgamemanagers.assets')).assets[0]
                scripts=[{'m_FileID':1,'m_PathID':o.path_id} for o in glob.objects.values() if o.type.name=='MonoScript']
                ab=asset.objects[1];tree=ab.read_typetree();tree['m_PreloadTable']=scripts+tree['m_PreloadTable']
                for _,entry in tree['m_Container']:
                    entry['preloadSize']=len(scripts)+entry['preloadIndex']+entry['preloadSize'];entry['preloadIndex']=0
                ab.save_typetree(tree)
            if name=='resources.assets' and a.debug_script:
                ab=asset.objects[1];tree=ab.read_typetree();pointer={'m_FileID':1,'m_PathID':2325}
                tree['m_Container'].append(('debug/monoscript',{'preloadIndex':len(tree['m_PreloadTable']),'preloadSize':1,'asset':pointer.copy()}))
                tree['m_PreloadTable'].append(pointer);ab.save_typetree(tree)
            p=metadata/name;p.write_bytes(asset.save())
        files.append(('duckov-'+name if a.rename_files else name,p,4))
    output=target/'duckov-native-assets.bundle';write_fs(output,files,engine=manifest['unity'])
    (target/'binding-changes.json').write_text(json.dumps({'reason':'Unity-generated AssetBundle seed uses .dll assembly names; original player MonoScript names omit this suffix',
        'changes':changes,'sourceBundle':manifest['bundle'],'bundle':{'path':str(output),'bytes':output.stat().st_size,'sha256':digest(output)}},ensure_ascii=False,indent=2),encoding='utf-8')
    print('Adapted MonoScript assembly names:',len(changes),flush=True)

if __name__=='__main__':main()
