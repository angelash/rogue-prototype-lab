"""Compile preserved decompiler output against the exact Mono player references.

No NuGet restore, source installation mutation, or implicit framework upgrade.
Generated response files, compiler logs and binaries stay in private local/.
"""
import argparse, hashlib, json, shutil, subprocess
from pathlib import Path

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--project', type=Path, required=True)
    ap.add_argument('--assembly', default='TeamSoda.Duckov.Core')
    ap.add_argument('--source', type=Path)
    ap.add_argument('--output', type=Path)
    args = ap.parse_args()
    local = args.project.resolve() / 'local'
    source = args.source.resolve() if args.source else local / 'HybridCode' / args.assembly
    if not source.exists():
        shutil.copytree(local / 'decompiled' / args.assembly, source)
    output = args.output.resolve() if args.output else local / 'HybridBuild' / args.assembly
    output.mkdir(parents=True, exist_ok=True)
    sdks = subprocess.check_output(['dotnet', '--list-sdks'], text=True).splitlines()
    version, root = sdks[-1].split(' [', 1)
    csc = Path(root.rstrip(']')) / version / 'Roslyn/bincore/csc.dll'
    refs = local / 'build-copy/Duckov_Data/Managed'
    files = sorted(p for p in source.rglob('*.cs') if not any(x in ('obj', 'bin') for x in p.parts))
    lines = ['/noconfig', '/utf8output', '/nostdlib+', '/target:library', '/langversion:12.0',
             '/unsafe+', '/optimize+', '/debug:portable', '/deterministic+',
             '/nowarn:1701,1702,0618,0649,0169,0414',
             f'/out:"{output / (args.assembly + ".dll")}"']
    lines += [f'/reference:"{p}"' for p in sorted(refs.glob('*.dll')) if p.stem != args.assembly]
    lines += [f'"{p}"' for p in files]
    response = output / 'compile.rsp'
    response.write_text('\n'.join(lines), encoding='utf-8')
    result = subprocess.run(['dotnet', str(csc), '@' + str(response)], capture_output=True, text=True, encoding='utf-8')
    (output / 'compiler.log').write_text(result.stdout + result.stderr, encoding='utf-8')
    errors = [x for x in (result.stdout + result.stderr).splitlines() if ': error ' in x]
    report = {'assembly': args.assembly, 'sourceFiles': len(files), 'exitCode': result.returncode,
              'errorCount': len(errors), 'compiler': str(csc), 'sourceRoot': str(source),
              'referencesRoot': str(refs), 'sourceDigest': hashlib.sha256('\n'.join(
                  str(p.relative_to(source)) + ':' + sha(p) for p in files).encode()).hexdigest()}
    dll = output / (args.assembly + '.dll')
    if result.returncode == 0:
        report['binarySHA256'] = sha(dll)
    (output / 'compile-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))
    print('\n'.join(errors[:30]))
    raise SystemExit(result.returncode)

if __name__ == '__main__':
    main()
