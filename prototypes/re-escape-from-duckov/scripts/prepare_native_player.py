"""Restore native player Resources/shared layout using the locally-built Unity host.

Retains the host's build/license settings and independent product/save identity.
Does not alter game assemblies, Steam checks, source installation or real saves.
Default mode uses the host's empty camera scene. --game-scenes restores the
original 63 scene paths and startup flow, with an independent save identity.
"""
import argparse,copy,hashlib,json,shutil
from pathlib import Path
import UnityPy
from UnityPy.streams import EndianBinaryReader,EndianBinaryWriter
from native_bundle import digest

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);ap.add_argument('--host',type=Path);ap.add_argument('--game-scenes',action='store_true');a=ap.parse_args()
    local=a.project.resolve()/'local';host=a.host.resolve() if a.host else local/'NativeProbe/Player';source=local/'build-copy/Duckov_Data';target=a.output.resolve()
    if target.exists():raise FileExistsError('Preserve previous player candidate: '+str(target))
    target.mkdir();data=target/'DuckovRestored_Data';data.mkdir()
    shutil.copy2(host/'DuckovNativeProbe.exe',target/'DuckovRestored.exe')
    for p in host.iterdir():
        if p.name in ['DuckovNativeProbe.exe','DuckovNativeProbe_Data']:continue
        if p.is_dir():shutil.copytree(p,target/p.name)
        else:shutil.copy2(p,target/p.name)
    hd=host/'DuckovNativeProbe_Data';records=[]
    for p in hd.iterdir():
        if p.is_dir():shutil.copytree(p,data/p.name)
        elif not(p.name.endswith('.assets') or p.name.endswith('.resS') or p.name=='globalgamemanagers'):
            shutil.copy2(p,data/p.name)
    # Keep original file identities and payloads. The installed engine is used.
    for p in source.iterdir():
        if not p.is_file() or not(p.name.endswith('.assets') or p.name.endswith('.resS') or (a.game_scenes and p.name.startswith('level'))):continue
        dest=data/p.name
        if p.name.endswith('.assets') or (p.name.startswith('level') and not p.name.endswith('.resS')):
            asset=UnityPy.load(str(p)).assets[0];before={pid:hashlib.sha256(o.get_raw_data()).digest() for pid,o in asset.objects.items()}
            asset.unity_version='2022.3.62f3c1';dest.write_bytes(asset.save());after=UnityPy.load(str(dest)).assets[0]
            assert before=={pid:hashlib.sha256(o.get_raw_data()).digest() for pid,o in after.objects.items()}
        else:shutil.copy2(p,dest)
        records.append({'file':p.name,'source':digest(p),'output':digest(dest),'objectBodiesUnchanged':True})
    original=UnityPy.load(str(source/'globalgamemanagers')).assets[0];built=UnityPy.load(str(hd/'globalgamemanagers')).assets[0]
    by_type={o.type.name:o for o in built.objects.values()}
    changes=[]
    for o in original.objects.values():
        if o.type.name=='BuildSettings':
            settings_raw=by_type['BuildSettings'].get_raw_data()
            if a.game_scenes:
                # The native f3c1 host has a BuildSettings tail not decoded by the
                # generic TPK schema. Replace its validated first string array;
                # preserve every byte of the host's flags/license/API tail.
                reader=EndianBinaryReader(settings_raw,endian='<');count=reader.read_int()
                assert count==1 and reader.read_aligned_string().endswith('/Probe.unity')
                tail=settings_raw[reader.Position:]
                writer=EndianBinaryWriter(endian='<');paths=o.read_typetree()['scenes'];writer.write_int(len(paths))
                for path in paths:writer.write_aligned_string(path)
                settings_raw=writer.bytes+tail
            o.serialized_type=copy.copy(by_type['BuildSettings'].serialized_type)
            original.types[o.type_id]=o.serialized_type;changes.append('Build/license settings retained from locally built host; '+('original 63 scene paths restored' if a.game_scenes else 'empty probe scene retained'))
            o.set_raw_data(settings_raw)
        elif o.type.name=='PlayerSettings':
            tree=o.read_typetree();settings=by_type['PlayerSettings'].read_typetree()
            for k in ['companyName','productName','productGUID','AID','cloudProjectId','organizationId','projectName']:
                if k in tree and k in settings:tree[k]=settings[k]
            o.save_typetree(tree);changes.append('Independent player/save and cloud identity from locally built host')
    original.unity_version='2022.3.62f3c1';(data/'globalgamemanagers').write_bytes(original.save())
    # Original startup callbacks + restoration probes; no native resource override flag.
    source_init=json.loads((source/'RuntimeInitializeOnLoads.json').read_text());host_init=json.loads((hd/'RuntimeInitializeOnLoads.json').read_text())
    source_init['root'].extend(x for x in host_init['root'] if x['assemblyName']=='DuckovRecovery.Runtime')
    (data/'RuntimeInitializeOnLoads.json').write_text(json.dumps(source_init),encoding='utf-8')
    for folder in ['StreamingAssets','Plugins']:
        if (source/folder).exists():shutil.copytree(source/folder,data/folder,dirs_exist_ok=True)
    (target/'app.info').write_text('RoguePrototypeLab\nDuckovRestorationLab\n',encoding='utf-8')
    if a.game_scenes:
        # Steam SDK's normal development app selection. SteamAPI checks remain intact.
        (target/'steam_appid.txt').write_text('3167020\n',encoding='ascii')
    proof={'unity':'2022.3.62f3c1','originalUnity':'2022.3.62f2','probeSceneOnly':not a.game_scenes,'changes':changes,'files':records,
        'unityPlayerSHA256':digest(target/'UnityPlayer.dll'),'hostUnityPlayerSHA256':digest(host/'UnityPlayer.dll')}
    (target/'native-player-inputs.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
    print('Native player Resources candidate prepared:',target,flush=True)

if __name__=='__main__':main()
