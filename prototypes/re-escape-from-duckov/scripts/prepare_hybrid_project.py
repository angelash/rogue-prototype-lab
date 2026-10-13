"""Create the Unity source project without exporting native assets to lossy YAML."""
import argparse, json, re, shutil, xml.etree.ElementTree as ET
from pathlib import Path
from assemble_hybrid_player import ASSEMBLIES

def compatible_source(text):
    # Unity 2022 accepts C# 9. A namespace block has the same type identity.
    text, changed = re.subn(r'^namespace ([\w.]+);[ \t]*$', r'namespace \1\n{', text, count=1, flags=re.M)
    if changed:
        text += '\n}\n'
    return text

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--project', type=Path, required=True)
    args = ap.parse_args()
    local = args.project.resolve() / 'local'
    host = local / 'UnityProject'
    target = local / 'HybridUnityProject'
    if target.exists():
        raise FileExistsError('Preserve existing project: ' + str(target))
    target.mkdir()
    for directory in ['Assets', 'Packages', 'ProjectSettings']:
        shutil.copytree(host / directory, target / directory)
    for filename in ['DuckovEditorCompatibility.cs', 'DuckovEditorCompatibility.cs.meta']:
        (target / 'Assets/Editor' / filename).unlink(missing_ok=True)
    shutil.copy2(args.project.resolve() / 'scripts/unity/DuckovHybridEditor.cs', target / 'Assets/Editor/DuckovHybridEditor.cs')
    plugins = target / 'Assets/Plugins'
    for name in ASSEMBLIES:
        for suffix in ['.dll', '.dll.meta', '.pdb']:
            p = plugins / (name + suffix)
            if p.exists():
                p.unlink()
    # Third-party libraries remain matched dependencies; game business code is source.
    generated = target / 'Assets/Reconstructed'
    count = 0
    for name in ASSEMBLIES:
        source = local / 'HybridCode' / name
        destination = generated / name
        destination.mkdir(parents=True)
        for p in source.rglob('*.cs'):
            if p.name == 'UnitySourceGeneratedAssemblyMonoScriptTypes_v1.cs':
                # The local Unity compiler regenerates this integration metadata.
                continue
            rel = p.relative_to(source)
            dest = destination / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_text(compatible_source(p.read_text(encoding='utf-8-sig')), encoding='utf-8')
            count += 1
        if name != 'Assembly-CSharp':
            tree = ET.parse(source / (name + '.csproj'))
            refs = [x.attrib['Include'] for x in tree.findall('.//Reference')]
            refs = [x for x in refs if x in ASSEMBLIES and x != name and x != 'Assembly-CSharp']
            if name == 'TeamSoda.Duckov.Core':
                refs.append('PackageTools')
            refs += ['Unity.InputSystem', 'UnityEngine.UI', 'Unity.Burst']
            asmdef = {'name': name, 'references': sorted(set(refs)), 'allowUnsafeCode': True,
                      'autoReferenced': True, 'overrideReferences': False}
            (destination / (name + '.asmdef')).write_text(json.dumps(asmdef, indent=2), encoding='utf-8')
    shutil.copy2(local / 'build-copy/Duckov_Data/Managed/ICSharpCode.SharpZipLib.dll', plugins / 'ICSharpCode.SharpZipLib.dll')
    for relative in ['Assets/Runtime/DuckovRecovery.Runtime.asmdef', 'Assets/Editor/DuckovRecovery.Editor.asmdef']:
        p = target / relative
        definition = json.loads(p.read_text(encoding='utf-8-sig'))
        definition['references'] = sorted(set(definition.get('references', []) + [x for x in ASSEMBLIES if x != 'Assembly-CSharp']))
        p.write_text(json.dumps(definition, indent=2), encoding='utf-8')
    settings = target / 'ProjectSettings/ProjectSettings.asset'
    text = settings.read_text(encoding='utf-8-sig').replace('productName: DuckovRestorationLab', 'productName: DuckovHybridRestorationLab')
    settings.write_text(text, encoding='utf-8')
    (target / 'README.md').write_text(
        '# Duckov hybrid recovery\n\n'
        'Unity 2022.3.62f3c1; Windows x64 Mono.\n\n'
        'Assets/Reconstructed contains recompiled decompiler-derived business code. '
        'Native game scenes and assets stay in ../build-copy and the verified native '
        'Resources player layout; they are assembled after the Unity host build. '
        'The empty Probe scene is only the engine build seed.\n', encoding='utf-8')
    print(json.dumps({'project': str(target), 'sourceAssemblies': len(ASSEMBLIES), 'sourceFiles': count}))

if __name__ == '__main__':
    main()
