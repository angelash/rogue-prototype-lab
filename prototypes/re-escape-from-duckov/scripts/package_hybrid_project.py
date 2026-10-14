"""Make a verified, portable handover ZIP after distribution is authorized."""
import argparse
import datetime
import hashlib
import json
import zipfile
from pathlib import Path

ASSEMBLIES = ['TeamSoda.Duckov.Core', 'TeamSoda.Duckov.Utilities',
              'TeamSoda.MiniLocalizor', 'ItemStatsSystem', 'SodaLocalization',
              'Plugins', 'Assembly-CSharp', 'KAnimationCore.Runtime',
              'FlexReader', 'PackageTools']
TOOLS = ['assemble_hybrid_player.py', 'audit_hybrid_abi.ps1',
         'build-hybrid-project.ps1', 'compile_hybrid_code.py',
         'start-hybrid-project.ps1', 'verify_hybrid_player.py',
         'package_hybrid_project.py']
PREFIX = 'HybridUnityProject-share/'
EXCLUDED_DIRS = {'Library', 'Temp', 'Logs', 'obj', 'UserSettings', '.git',
                 '.vs', '.idea', '.vscode', '__pycache__'}


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def portable_tool(name, text):
    if name == 'build-hybrid-project.ps1':
        old = "-Project $project -BuildRoot $compiled"
        assert old in text
        text = text.replace(old, "-Project $project -Editor (Split-Path -Parent $Unity) -BuildRoot $compiled")
        old = "& $Python (Join-Path $PSScriptRoot 'assemble_hybrid_player.py') --project $project --output $candidate --compiled-root $compiled"
        assert old in text
        text = text.replace(old,
            "$base = Join-Path $local 'Player'\n"
            "if (!(Test-Path -LiteralPath $base)) { $base = Join-Path $local 'HybridPlayer' }\n"
            "& $Python (Join-Path $PSScriptRoot 'assemble_hybrid_player.py') --project $project --base $base --output $candidate --compiled-root $compiled")
    if name == 'build-hybrid-project.ps1':
        old = "$ErrorActionPreference = 'Stop'"
        assert old in text
        text = text.replace(old, old + "\n"
            "if (!(Test-Path -LiteralPath $Unity)) { throw 'Specify the installed editor with -Unity; required version: 2022.3.62f3c1' }")
    if name == 'start-hybrid-project.ps1':
        old = 'if ($Editor) {'
        assert old in text
        text = text.replace(old, old + "\n    if (!(Test-Path -LiteralPath $Unity)) { throw 'Specify the installed editor with -Unity; required version: 2022.3.62f3c1' }")
    if name == 'build-hybrid-project.ps1':
        old = "if (!(Test-Path -LiteralPath $Unity))"
        text = text.replace(old,
            "if (!(Test-Path -LiteralPath $Python)) {\n"
            "    $command = Get-Command python -ErrorAction SilentlyContinue\n"
            "    if (!$command) { throw 'Specify Python with -Python and install the documented dependencies' }\n"
            "    $Python = $command.Source\n"
            "}\n" + old, 1)
    return text


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--project', type=Path, required=True)
    ap.add_argument('--output', type=Path, required=True)
    ap.add_argument('--distribution-authorized', action='store_true',
                    help='Use only after permission to distribute this content is confirmed')
    args = ap.parse_args()
    if not args.distribution_authorized:
        raise ValueError('Distribution authorization is required for the proprietary contents')
    project = args.project.resolve(); local = project / 'local'
    output = args.output.resolve()
    if output.exists():
        raise FileExistsError('Previous archives are preserved: ' + str(output))
    if output.suffix.lower() != '.zip':
        raise ValueError('Output must be a .zip file')
    files = {}
    def add_file(source, target):
        if source.is_symlink() or not source.is_file():
            raise ValueError('Missing regular input file: ' + str(source))
        if not source.resolve().is_relative_to(project):
            raise ValueError('Input outside project')
        if target in files or target.startswith('/') or '..' in Path(target).parts:
            raise ValueError('Duplicate or unsafe archive path: ' + target)
        files[target] = source
    def add_tree(source, target):
        for path in sorted(source.rglob('*')):
            rel = path.relative_to(source)
            if any(part in EXCLUDED_DIRS for part in rel.parts):
                continue
            if path.is_symlink():
                raise ValueError('Symlink input: ' + str(path))
            if path.is_file():
                if path.suffix.lower() in {'.log', '.sav', '.es3', '.pem', '.pfx', '.key'} or path.name.startswith('.env'):
                    raise ValueError('Unexpected log/save/credential input: ' + str(rel))
                add_file(path, target + '/' + rel.as_posix())
    for name in ['Assets', 'Packages', 'ProjectSettings']:
        add_tree(local / 'HybridUnityProject' / name, 'local/HybridUnityProject/' + name)
    add_file(local / 'HybridUnityProject/README.md', 'local/HybridUnityProject/README.md')
    add_tree(local / 'HybridPlayer', 'local/HybridPlayer')
    add_tree(local / 'build-copy/Duckov_Data/Managed', 'local/build-copy/Duckov_Data/Managed')
    for name in ASSEMBLIES:
        add_file(local / 'HybridBuild' / name / 'compile-report.json',
                 'local/HybridBuild/' + name + '/compile-report.json')
    add_file(project / 'PACKAGING.md', 'README.md')
    add_file(project / 'reports/hybrid-restoration.md', 'reports/hybrid-restoration.md')
    generated = {}
    for name in TOOLS:
        generated['scripts/' + name] = portable_tool(name,
            (project / 'scripts' / name).read_text(encoding='utf-8-sig')).encode('utf-8')
    # The packaged build script chooses the existing HybridPlayer as its frozen
    # baseline, avoiding a second 3 GB Player. Preserve the original reference DLLs.
    manifest = json.loads((local / 'HybridPlayer/hybrid-player-inputs.json').read_text())
    source_matches = []
    for row in manifest['assemblies']:
        root = local / 'HybridUnityProject/Assets/Reconstructed' / row['assembly']
        source = sorted(root.rglob('*.cs'))
        h = hashlib.sha256('\n'.join(str(p.relative_to(root)) + ':' + digest(p)
                                     for p in source).encode()).hexdigest()
        source_matches.append({'assembly': row['assembly'],
                               'sourceMatchesTestedPlayer': h == row['unitySourceDigest']})
    records = []
    output.parent.mkdir(parents=True, exist_ok=True)
    total = sum(p.stat().st_size for p in files.values()) + sum(map(len, generated.values()))
    print(json.dumps({'inputFiles': len(files) + len(generated), 'inputBytes': total,
                      'sourceMatchesTestedPlayer': all(x['sourceMatchesTestedPlayer'] for x in source_matches)}), flush=True)
    with output.open('xb') as destination:
        with zipfile.ZipFile(destination, 'w', compression=zipfile.ZIP_DEFLATED,
                             compresslevel=1, allowZip64=True) as archive:
            for i, (name, path) in enumerate(sorted(files.items()), 1):
                before = path.stat(); h = hashlib.sha256(); size = 0
                with path.open('rb') as src, archive.open(PREFIX + name, 'w', force_zip64=True) as dst:
                    for block in iter(lambda: src.read(1024 * 1024), b''):
                        h.update(block); size += len(block); dst.write(block)
                after = path.stat()
                if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
                    raise ValueError('Input changed during compression: ' + name)
                records.append({'path': name, 'bytes': size, 'sha256': h.hexdigest()})
                if i % 500 == 0:
                    print('Packed ' + str(i) + ' files', flush=True)
            for name, data in sorted(generated.items()):
                archive.writestr(PREFIX + name, data)
                records.append({'path': name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(),
                                'packagedScriptAdaptedForPortablePaths': True})
            package = {'format': 'complete-hybrid-project', 'distributionAuthorizationConfirmed': True,
                'unity': '2022.3.62f3c1', 'platform': 'Windows x64 Mono',
                'files': records, 'sourcePlayerConsistency': source_matches,
                'containsPersonalSavesLogsGitOrUnityCaches': False,
                'fullGameAcceptancePassed': False,
                'portableChanges': ['Cecil editor path follows -Unity',
                                    'Frozen baseline defaults to packaged HybridPlayer',
                                    'Python can be specified or resolved on PATH'],
                'notIncluded': ['Library', 'Temp', 'Logs', 'UserSettings', '.git',
                                'Old candidates', 'Research database and exports', 'Personal saves']}
            archive.writestr(PREFIX + 'manifest.json', json.dumps(package, ensure_ascii=False, indent=2))
    # Independent archive readback checks every decompressed byte and ZIP CRC.
    with zipfile.ZipFile(output) as archive:
        assert set(archive.namelist()) == {PREFIX + r['path'] for r in records} | {PREFIX + 'manifest.json'}
        assert json.loads(archive.read(PREFIX + 'manifest.json')) == package
        for i, row in enumerate(records, 1):
            h = hashlib.sha256(); size = 0
            with archive.open(PREFIX + row['path']) as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b''):
                    h.update(block); size += len(block)
            assert size == row['bytes'] and h.hexdigest() == row['sha256'], row['path']
            if i % 1000 == 0:
                print('Verified ' + str(i) + ' decompressed files', flush=True)
    result = {'date': datetime.datetime.now().isoformat(), 'archive': str(output),
              'files': len(records) + 1, 'inputBytes': total, 'archiveBytes': output.stat().st_size,
              'sha256': digest(output), 'allEntriesReadbackVerified': True,
              'allSourceMatchesTestedPlayer': all(x['sourceMatchesTestedPlayer'] for x in source_matches),
              'fullGameAcceptancePassed': False}
    (project / 'evidence/share-package.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result), flush=True)


if __name__ == '__main__':
    main()
