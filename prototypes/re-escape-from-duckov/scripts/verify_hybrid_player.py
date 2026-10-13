"""Independent candidate checks and private paired-image difference metrics."""
import argparse, hashlib, json
from pathlib import Path

def sha(p):
    h = hashlib.sha256()
    with p.open('rb') as f:
        for block in iter(lambda: f.read(4 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--project', type=Path, required=True)
    ap.add_argument('--reference-image', type=Path)
    ap.add_argument('--candidate-image', type=Path)
    args = ap.parse_args()
    project = args.project.resolve(); local = project / 'local'; player = local / 'HybridPlayer'
    native = json.loads((player / 'native-player-inputs.json').read_text())
    manifest = json.loads((player / 'hybrid-player-inputs.json').read_text())
    failures = []
    for record in native['files']:
        path = player / 'DuckovRestored_Data' / record['file']
        if sha(path) != record['output']:
            failures.append('native-resource:' + record['file'])
    for record in manifest['assemblies']:
        path = player / 'DuckovRestored_Data/Managed' / (record['assembly'] + '.dll')
        expected = record.get('unityCompiledBinarySHA256', record['binarySHA256'])
        if sha(path) != expected:
            failures.append('business-assembly:' + record['assembly'])
    report = {'nativeResourceFilesChecked': len(native['files']), 'sourceAssembliesChecked': len(manifest['assemblies']),
              'engineMatchesFrozenLocalHost': sha(player / 'UnityPlayer.dll') == native['hostUnityPlayerSHA256'],
              'failures': failures}
    if args.reference_image and args.candidate_image:
        from PIL import Image
        import numpy as np
        a = np.asarray(Image.open(args.reference_image).convert('RGB'))
        b = np.asarray(Image.open(args.candidate_image).convert('RGB'))
        if a.shape != b.shape:
            raise ValueError('Paired image dimensions differ')
        if float(a.std()) < 2 or float(b.std()) < 2:
            raise ValueError('Blank images cannot prove visual equivalence')
        delta = np.abs(a.astype(np.int16) - b.astype(np.int16))
        equal = np.all(delta == 0, axis=2)
        report['pixelSample'] = {'scope': 'frozen title; original business DLL versus Unity-recompiled business DLL; same f3c1 engine',
            'width': int(a.shape[1]), 'height': int(a.shape[0]), 'pixels': int(equal.size),
            'identicalPixels': int(equal.sum()), 'identicalFraction': float(equal.mean()),
            'meanAbsoluteChannelDelta': float(delta.mean()), 'maxChannelDelta': int(delta.max()),
            'allPixelsIdentical': bool(equal.all()), 'referenceSHA256': sha(args.reference_image),
            'candidateSHA256': sha(args.candidate_image), 'provesFullGameEquivalence': False}
        Image.fromarray(np.minimum(delta * 8, 255).astype(np.uint8)).save(local / 'hybrid-pixel-difference.png')
    (local / 'hybrid-verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))
    if failures or not report['engineMatchesFrozenLocalHost']:
        raise SystemExit(1)

if __name__ == '__main__':
    main()
