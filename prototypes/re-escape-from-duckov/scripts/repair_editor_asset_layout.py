"""Restore resource keys and FMOD's exported subasset lookup layout."""
import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--project', type=Path, required=True)
    args = parser.parse_args()
    local = args.project.resolve() / 'local'
    assets = local / 'RestoredUnityProject/Assets'
    settings = assets / 'Resources/FMODStudioSettings.asset'
    platforms = re.findall(r'guid: ([0-9a-f]{32})', settings.read_text(encoding='utf-8').split('  Platforms:')[1])
    lookup = {}
    for meta in assets.rglob('*.meta'):
        match = re.search(r'(?m)^guid: ([0-9a-f]{32})', meta.read_text(encoding='utf-8-sig'))
        if match:
            lookup[match[1]] = meta.with_suffix('')
    moves = []
    for guid in platforms:
        source = lookup[guid]
        destination = assets / 'Resources/FMODStudioSettings' / source.name
        if source == destination:
            continue
        destination.parent.mkdir(parents=True, exist_ok=True)
        if destination.exists():
            raise FileExistsError(destination)
        before = hashlib.sha256(source.read_bytes()).hexdigest()
        shutil.move(source, destination)
        shutil.move(Path(str(source) + '.meta'), Path(str(destination) + '.meta'))
        assert before == hashlib.sha256(destination.read_bytes()).hexdigest()
        moves.append({'from': str(source.relative_to(assets)), 'to': str(destination.relative_to(assets)), 'sha256': before})
    for source in (assets / 'Resources').rglob('*.generated.generated'):
        destination = source.with_suffix('.bytes')
        if destination.exists():
            raise FileExistsError(destination)
        before = hashlib.sha256(source.read_bytes()).hexdigest()
        shutil.move(source, destination)
        shutil.move(Path(str(source) + '.meta'), Path(str(destination) + '.meta'))
        assert before == hashlib.sha256(destination.read_bytes()).hexdigest()
        moves.append({'from': str(source.relative_to(assets)), 'to': str(destination.relative_to(assets)), 'sha256': before})
    # RuntimeUtils follows SourceBankPath/Desktop when Application.isEditor.
    banks = local / 'RestoredUnityProject/FMODBanks/Desktop'
    banks.mkdir(parents=True, exist_ok=True)
    for name in ('Master.bank', 'Master.strings.bank'):
        destination = banks / name
        source = assets / 'StreamingAssets' / name
        if destination.exists():
            if hashlib.sha256(destination.read_bytes()).digest() != hashlib.sha256(source.read_bytes()).digest():
                raise RuntimeError(f'Existing bank differs: {destination}')
        else:
            shutil.copy2(source, destination)
    (local / 'editor-asset-layout-repair.json').write_text(json.dumps({'moves': moves, 'platforms': len(platforms)}, indent=2), encoding='utf-8')
    print(json.dumps({'moves': len(moves), 'platforms': len(platforms)}))


if __name__ == '__main__':
    main()
