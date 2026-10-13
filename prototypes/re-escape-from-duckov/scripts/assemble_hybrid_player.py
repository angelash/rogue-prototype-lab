"""Fresh hybrid candidate: native Resources layout plus recompiled business code."""
import argparse, hashlib, json, shutil, uuid
from pathlib import Path
import UnityPy

ASSEMBLIES = ['TeamSoda.Duckov.Core', 'TeamSoda.Duckov.Utilities', 'TeamSoda.MiniLocalizor',
              'ItemStatsSystem', 'SodaLocalization', 'Plugins', 'Assembly-CSharp',
              'KAnimationCore.Runtime', 'FlexReader', 'PackageTools']

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--project', type=Path, required=True)
    ap.add_argument('--base', type=Path)
    ap.add_argument('--compiled-root', type=Path, help='Unity Player Managed directory; otherwise use Roslyn trial outputs')
    ap.add_argument('--output', type=Path, required=True)
    args = ap.parse_args()
    local = args.project.resolve() / 'local'
    base = args.base.resolve() if args.base else local / 'Player'
    output = args.output.resolve()
    if output.exists():
        raise FileExistsError('Previous candidates are preserved: ' + str(output))
    proof = json.loads((base / 'native-player-inputs.json').read_text())
    if proof['probeSceneOnly'] or sha(base / 'UnityPlayer.dll') != proof['hostUnityPlayerSHA256']:
        raise ValueError('A verified locally built native engine with the game scene layout is required')
    records = []
    for name in ASSEMBLIES:
        build = local / 'HybridBuild' / name
        report = json.loads((build / 'compile-report.json').read_text())
        dll = (args.compiled_root.resolve() / (name + '.dll')) if args.compiled_root else build / (name + '.dll')
        if report['exitCode'] != 0 or not dll.exists():
            raise ValueError('Unverified source build: ' + name)
        if not args.compiled_root and sha(dll) != report['binarySHA256']:
            raise ValueError('Source output changed after compilation: ' + name)
        if args.compiled_root:
            report['unityCompiledBinarySHA256'] = sha(dll)
            report['unityCompiledRoot'] = str(args.compiled_root.resolve())
            source_root = local / 'HybridUnityProject/Assets/Reconstructed' / name
            source_files = sorted(source_root.rglob('*.cs'))
            report['unitySourceFiles'] = len(source_files)
            report['unitySourceDigest'] = hashlib.sha256('\n'.join(
                str(p.relative_to(source_root)) + ':' + sha(p) for p in source_files).encode()).hexdigest()
        records.append(report)
    shutil.copytree(base, output)
    data = output / 'DuckovRestored_Data'
    for name in ASSEMBLIES:
        for suffix in ['.dll', '.pdb']:
            source = (args.compiled_root.resolve() / (name + suffix)) if args.compiled_root else local / 'HybridBuild' / name / (name + suffix)
            if source.exists():
                shutil.copy2(source, data / 'Managed' / source.name)
    # Keep the frozen game dependency ABI. The opt-in recovery observer itself
    # is compiled against the same original runtime reference set.
    observer = local / 'HybridBuild/DuckovRecovery.Runtime/DuckovRecovery.Runtime.dll'
    if observer.exists():
        shutil.copy2(observer, data / 'Managed' / observer.name)
        callback_file = data / 'RuntimeInitializeOnLoads.json'
        callbacks = json.loads(callback_file.read_text())
        callback = dict(assemblyName='DuckovRecovery.Runtime', nameSpace='', className='DuckovHybridQA',
                        methodName='Begin', loadTypes=1, isUnityClass=False)
        if callback not in callbacks['root']:
            callbacks['root'].append(callback)
        callback_file.write_text(json.dumps(callbacks), encoding='utf-8')
    settings_file = data / 'globalgamemanagers'
    assets = UnityPy.load(str(settings_file)).assets[0]
    for obj in assets.objects.values():
        if obj.type.name == 'PlayerSettings':
            tree = obj.read_typetree()
            tree['companyName'] = 'RoguePrototypeLab'
            tree['productName'] = 'DuckovHybridRestorationLab'
            # Keep the locally built engine's product GUID; save identity is separate.
            obj.save_typetree(tree)
    settings_file.write_bytes(assets.save())
    (output / 'app.info').write_text('RoguePrototypeLab\nDuckovHybridRestorationLab\n', encoding='utf-8')
    manifest = {'route': 'recompiled-business-code/native-resources', 'assemblies': records,
                'base': str(base), 'engineSHA256': sha(output / 'UnityPlayer.dll'),
                'sceneCount': 63, 'saveIdentity': 'RoguePrototypeLab/DuckovHybridRestorationLab'}
    (output / 'hybrid-player-inputs.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(json.dumps({'output': str(output), 'sourceAssemblies': len(records),
                      'sourceFiles': sum(x.get('unitySourceFiles', x['sourceFiles']) for x in records)}))

if __name__ == '__main__':
    main()
