"""Create a native streamed scene archive from original compiled scene bodies."""
import argparse, copy, gc, hashlib, json, sqlite3, sys, zlib
from pathlib import Path
import UnityPy
from build_native_runtime import fill_types
from native_bundle import write_fs, digest

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,required=True);a=ap.parse_args()
    project=a.project.resolve();local=project/'local';data=local/'build-copy/Duckov_Data';work=local/'NativeRuntime'
    sys.path.insert(0,str(local/'tools/python'))
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    gen=TypeTreeGenerator('2022.3.62f2');gen.load_local_game(str(local/'build-copy'))
    db=sqlite3.connect('file:'+str(local/'assets.sqlite')+'?mode=ro',uri=True)
    paths=json.loads(zlib.decompress(db.execute('SELECT tree FROM objects WHERE type="BuildSettings"').fetchone()[0]))['scenes']
    stems=[Path(p).stem for p in paths];assert len(stems)==len(set(stems))
    scene_names={f'level{i}':'BuildPlayer-'+stem for i,stem in enumerate(stems)}
    seed=UnityPy.load(str(local/'NativeProbe/seed-scenes'));wrapper=seed.assets[0]
    directory=next(o for o in wrapper.objects.values() if o.type.name=='AssetBundle')
    wrapper.objects={directory.path_id:directory}
    tree=directory.read_typetree();tree.update(m_Name='duckov-native-scenes',m_Dependencies=['duckov-native-assets'],m_PreloadTable=[],
        m_Container=[(p,{'preloadIndex':0,'preloadSize':0,'asset':{'m_FileID':0,'m_PathID':0}}) for p in paths])
    directory.save_typetree(tree)
    files=[('BuildPlayer-Duckov.sharedAssets',wrapper.save(),4)];records=[];diagnostics=[]
    for i,path in enumerate(paths):
        original='level'+str(i);env=UnityPy.load(str(data/original));env.typetree_generator=gen;asset=env.assets[0]
        before={pid:hashlib.sha256(o.get_raw_data()).hexdigest() for pid,o in asset.objects.items()}
        for ext in asset.externals:
            target=Path(ext.path).name
            if target.endswith('.assets'):ext.path='archive:/resources.assets/'+target
            elif target in scene_names:ext.path='archive:/'+scene_names[target]+'/'+scene_names[target]
        fill_types(asset,gen,diagnostics);asset.unity_version='2022.3.62f3c1';raw=asset.save()
        restored=UnityPy.load(raw).assets[0]
        assert before=={pid:hashlib.sha256(o.get_raw_data()).hexdigest() for pid,o in restored.objects.items()},original+' changed object data'
        target=work/'serialized'/original
        if target.exists():assert target.read_bytes()==raw,'Existing scene output differs; preserve previous artifact'
        else:target.write_bytes(raw)
        files.append((scene_names[original],target,4))
        records.append({'buildIndex':i,'path':path,'originalContainer':original,'nativeFile':scene_names[original],'objects':len(before),'unchangedOriginalPayloads':len(before),'sha256':digest(target)})
        print(f'Native scene {i+1}/{len(paths)} {original}: {len(before)} unchanged objects',flush=True)
        del env,asset,raw,restored;gc.collect()
    target=work/'duckov-native-scenes.bundle';write_fs(target,files,engine='2022.3.62f3c1')
    (work/'scenes.json').write_text(json.dumps({'scenes':records,'partialTypeTrees':diagnostics,'bundle':{'path':str(target),'bytes':target.stat().st_size,'sha256':digest(target)}},ensure_ascii=False,indent=2),encoding='utf-8')
    print('Native scenes archive complete',flush=True)

if __name__=='__main__':main()
