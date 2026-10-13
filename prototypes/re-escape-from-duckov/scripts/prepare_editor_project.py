"""Prepare an editable recovery candidate, preserving the export and raw input."""
import argparse
import hashlib
import json
import re
import shutil
import uuid
from datetime import datetime, timezone
from pathlib import Path


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--project', type=Path, required=True)
    args = parser.parse_args()
    project = args.project.resolve()
    local = project / 'local'
    source = local / 'AssetRipperExport/ExportedProject'
    output = local / 'RestoredUnityProject'
    if output.exists():
        raise SystemExit(f'Refusing to overwrite an existing candidate: {output}')
    if not (source / 'Assets/Scenes/Startup/Startup.unity').is_file():
        raise SystemExit('Expected recovered Startup scene is missing')
    if (local / 'UnityProject').is_symlink():
        raise SystemExit('Host must be the verified physical F-drive directory')

    report = {'startedAt': datetime.now(timezone.utc).isoformat(),
              'source': str(source), 'output': str(output),
              'status': 'prepared-not-imported', 'copiedFiles': 0,
              'pluginChanges': [], 'settingsChanges': []}
    for directory in ('Assets', 'Packages', 'ProjectSettings'):
        shutil.copytree(source / directory, output / directory)
    report['copiedFiles'] = sum(p.is_file() for p in output.rglob('*'))

    # Preserve exported GUIDs/fileIDs; use the already API-updated original
    # runtime assemblies, instead of AssetRipper's rewritten implementations.
    for dll in sorted((output / 'Assets/Plugins').glob('*.dll')):
        host_dll = local / 'UnityProject/Assets/Plugins' / dll.name
        if not host_dll.is_file():
            host_dll = local / 'editor-compatibility-originals/UnityProject' / dll.name
        if not host_dll.is_file():
            raise RuntimeError(f'Validated host assembly is missing: {dll.name}')
        before = digest(dll)
        shutil.copy2(host_dll, dll)
        meta = dll.with_suffix('.dll.meta')
        text = meta.read_text(encoding='utf-8-sig')
        text, count = re.subn(r'(Editor: Editor\s+second:\s+enabled: )0', r'\g<1>1', text)
        meta.write_text(text, encoding='utf-8', newline='\n')
        report['pluginChanges'].append({'name': dll.name, 'exportSha256': before,
                                       'hostSha256': digest(dll), 'enabledEditor': bool(count)})

    # Native platform binaries are required for Editor FMOD/platform startup.
    native = local / 'build-copy/Duckov_Data/Plugins/x86_64'
    native_out = output / 'Assets/Plugins/x86_64'
    native_out.mkdir(parents=True, exist_ok=True)
    for dll in native.glob('*.dll'):
        if dll.name == 'lib_burst_generated.dll':
            continue  # Player AOT binary; Editor uses the documented managed fallback.
        shutil.copy2(dll, native_out / dll.name)
        (native_out / (dll.name + '.meta')).write_text(
            'fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\nPluginImporter:\n'
            '  serializedVersion: 2\n  isPreloaded: 0\n  platformData:\n'
            '  - first:\n      Any: \n    second:\n      enabled: 0\n      settings: {}\n'
            '  - first:\n      Editor: Editor\n    second:\n      enabled: 1\n'
            '      settings:\n        CPU: x86_64\n        OS: Windows\n'
            '  - first:\n      Standalone: Win64\n    second:\n      enabled: 1\n'
            '      settings:\n        CPU: x86_64\n', encoding='utf-8')

    settings_path = output / 'ProjectSettings/ProjectSettings.asset'
    settings = settings_path.read_text(encoding='utf-8-sig')
    replacements = {'companyName': 'RoguePrototypeLab', 'productName': 'DuckovEditorRecoveryLab',
                    'productGUID': uuid.uuid4().hex, 'cloudProjectId': '',
                    'cloudProjectName': '', 'organizationId': '',
                    'defaultScreenWidth': '1280', 'defaultScreenHeight': '720',
                    'defaultIsNativeResolution': '0', 'fullscreenMode': '3',
                    'runInBackground': '1'}
    for key, value in replacements.items():
        settings, count = re.subn(rf'(?m)^(  {re.escape(key)}:) .*$', rf'\g<1> {value}', settings)
        if count:
            report['settingsChanges'].append(key)
    settings = re.sub(r'(?m)^(    Standalone:) .*$', r'\1 com.rogueprototypelab.duckov.editorrecovery', settings)
    settings_path.write_text(settings, encoding='utf-8', newline='\n')
    (output / 'ProjectSettings/ProjectVersion.txt').write_text(
        'm_EditorVersion: 2022.3.62f3c1\n', encoding='utf-8')

    editor = output / 'Assets/Editor/Recovery'
    editor.mkdir(parents=True)
    (editor / 'DuckovEditorRecovery.Editor.asmdef').write_text(json.dumps({
        'name': 'DuckovEditorRecovery.Editor', 'references': ['Unity.Burst', 'Unity.InputSystem'], 'includePlatforms': ['Editor']
    }), encoding='utf-8')
    for name in ('DuckovEditorCompatibility.cs', 'DuckovEditorQA.cs'):
        shutil.copy2(project / 'scripts/unity' / name, editor / name)
    (output / 'README.md').write_text(
        '# Unity 编辑恢复候选\n\n本目录包含 63 个恢复场景与导出资产，使用本地 Unity 2022.3.62f3c1。\n'
        '它是恢复候选，导入、引用与玩法必须另行验证；不是原作者工程。\n'
        'Assets/Plugins 暂时使用原运行程序集以保留脚本绑定；反编译 C# 在 ../decompiled，尚未全部源码化。\n'
        '独立产品：RoguePrototypeLab / DuckovEditorRecoveryLab。\n'
        '原始导出 ../AssetRipperExport/ExportedProject 和旧探针宿主 ../UnityProject 保留。\n', encoding='utf-8')
    report['completedAt'] = datetime.now(timezone.utc).isoformat()
    (local / 'editor-project-preparation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'status': report['status'], 'output': str(output),
                      'copiedFiles': report['copiedFiles'], 'plugins': len(report['pluginChanges'])}, ensure_ascii=False))


if __name__ == '__main__':
    main()
