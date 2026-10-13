"""Use cached Editor-capable packages and remap recovered plugin script refs."""
import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--remap-only', action='store_true', help='Repair script references without copying packages again')
    args = parser.parse_args()
    project = args.project.resolve()
    local = project / 'local'
    cache = Path.home() / 'AppData/Local/Unity/cache/packages/packages.unity.cn'
    ui = Path('D:/Program files/2022.3.62f3c1/Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.ugui')
    packages = {'com.unity.inputsystem': cache / 'com.unity.inputsystem@1.14.0',
                'com.unity.burst': cache / 'com.unity.burst@1.8.21',
                'com.unity.ugui': ui}
    for package in packages.values():
        if not (package / 'package.json').is_file():
            raise SystemExit(f'Required existing package not found: {package}')
    old_scripts = json.loads((local / 'editor-plugin-script-map.json').read_text(encoding='utf-8-sig'))['scripts']
    package_scripts = {}
    for package in packages.values():
        for cs in package.rglob('*.cs'):
            if 'Editor' in cs.relative_to(package).parts or not cs.with_suffix('.cs.meta').is_file():
                continue
            code = cs.read_text(encoding='utf-8-sig')
            ns = re.search(r'\bnamespace\s+([\w.]+)', code)
            guid = re.search(r'(?m)^guid: ([0-9a-f]{32})', cs.with_suffix('.cs.meta').read_text())
            if ns and guid:
                package_scripts[ns[1] + '.' + cs.stem] = guid[1]
    mapping = {}
    for script in old_scripts:
        if script['type'] in package_scripts:
            mapping[(str(script['fileId']), script['guid'])] = package_scripts[script['type']]
    reference = re.compile(r'\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: 3\}')
    reports = []
    for project_name in ('UnityProject', 'RestoredUnityProject'):
        target = local / project_name
        if not (target / 'Packages/manifest.json').is_file():
            raise SystemExit(f'Project preparation has not finished: {target}')
        if args.remap_only:
            changes, unresolved = remap_assets(target, mapping, old_scripts, reference)
            reports.append({'project': project_name, 'changedReferenceFiles': changes,
                            'unresolvedPluginRefs': sorted(unresolved)})
            continue
        backup = local / 'editor-compatibility-originals' / project_name
        backup.mkdir(parents=True, exist_ok=True)
        removed = []
        for name in ('Unity.Burst.dll', 'Unity.Burst.Unsafe.dll', 'Unity.InputSystem.dll', 'UnityEngine.UI.dll'):
            for path in (target / 'Assets/Plugins' / name, target / 'Assets/Plugins' / (name + '.meta')):
                if path.exists():
                    destination = backup / path.name
                    if destination.exists():
                        raise RuntimeError(f'Refusing to overwrite previous plugin backup {destination}')
                    shutil.move(path, destination)
                    removed.append(path.name)
        for name, source in packages.items():
            output = target / 'Packages' / name
            shutil.copytree(source, output)
            # Embedded package tests are imported automatically, unlike registry
            # packages. Preserve them outside Unity import without adding a new
            # NUnit/test-framework dependency to the recovered game.
            if (output / 'Tests').is_dir():
                (output / 'Tests').rename(output / 'Tests~')
            if (output / 'DocCodeSamples.Tests').is_dir():
                (output / 'DocCodeSamples.Tests').rename(output / 'DocCodeSamples.Tests~')
            if name == 'com.unity.burst':
                package_json = output / 'package.json'
                config = json.loads(package_json.read_text())
                # Mathematics is already present as a recovered precompiled
                # dependency; avoid introducing a duplicate assembly.
                config.get('dependencies', {}).pop('com.unity.mathematics', None)
                package_json.write_text(json.dumps(config, indent=2), encoding='utf-8')
            if name == 'com.unity.inputsystem':
                # Input 1.14 uses Unity 6's UGUI asmdef names. Unity 2022's
                # built-in UGUI has the same APIs under its legacy names.
                for asmdef in output.rglob('*.asmdef'):
                    text = asmdef.read_text(encoding='utf-8-sig')
                    text = text.replace('"Unity.ugui.Editor"', '"UnityEditor.UI"')
                    text = text.replace('"Unity.ugui"', '"UnityEngine.UI"')
                    asmdef.write_text(text, encoding='utf-8', newline='\n')
        manifest_path = target / 'Packages/manifest.json'
        manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
        for name in packages:
            manifest['dependencies'][name] = 'file:' + name
        manifest_path.write_text(json.dumps(manifest, indent=2), encoding='utf-8')
        changes, unresolved = remap_assets(target, mapping, old_scripts, reference)
        editor = target / 'Assets/Editor/Recovery' if project_name == 'RestoredUnityProject' else target / 'Assets/Editor'
        for name in ('DuckovEditorCompatibility.cs', 'DuckovEditorQA.cs'):
            shutil.copy2(project / 'scripts/unity' / name, editor / name)
        asmdef = target / ('Assets/Editor/Recovery/DuckovEditorRecovery.Editor.asmdef'
                           if project_name == 'RestoredUnityProject' else 'Assets/Editor/DuckovRecovery.Editor.asmdef')
        config = json.loads(asmdef.read_text(encoding='utf-8-sig'))
        for name in ('Unity.Burst', 'Unity.InputSystem'):
            if name not in config.setdefault('references', []):
                config['references'].append(name)
        asmdef.write_text(json.dumps(config, indent=2), encoding='utf-8')
        reports.append({'project': project_name, 'removedPlugins': removed,
                        'changedReferenceFiles': changes, 'unresolvedPluginRefs': sorted(unresolved),
                        'packages': {name: str(path) for name, path in packages.items()}})
    output_name = 'editor-package-remapping.json' if args.remap_only else 'editor-package-recovery.json'
    (local / output_name).write_text(json.dumps(reports, indent=2), encoding='utf-8')
    print(json.dumps([{'project': x['project'], 'referenceFiles': len(x['changedReferenceFiles']),
                       'unresolvedRefs': len(x['unresolvedPluginRefs'])} for x in reports]))


def remap_assets(target, mapping, old_scripts, reference):
    changes, unresolved = [], set()
    old_guids = {s['guid'] for s in old_scripts}
    for path in (target / 'Assets').rglob('*'):
        if path.suffix not in ('.asset', '.prefab', '.unity', '.mat', '.controller', '.anim'):
            continue
        with path.open('rb') as stream:
            if stream.read(5) != b'%YAML':
                continue
        before = path.read_text(encoding='utf-8-sig')
        def remap(match):
            key = (match[1], match[2])
            if key in mapping:
                return '{fileID: 11500000, guid: ' + mapping[key] + ', type: 3}'
            if match[2] in old_guids:
                unresolved.add(key)
            return match[0]
        after = reference.sub(remap, before)
        if after != before:
            path.write_text(after, encoding='utf-8', newline='\n')
            changes.append(str(path.relative_to(target)))
    return changes, unresolved


if __name__ == '__main__':
    main()
