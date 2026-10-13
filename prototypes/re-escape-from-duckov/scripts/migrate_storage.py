"""Copy this project's external storage back to F, verify bytes, then switch links.

Original D directories are retained. Only verified junction entries are removed.
Per-file manifests remain private under local/storage-migration.
"""
import argparse, hashlib, json, os, shutil, stat, time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.writing')
    temporary.write_text(json.dumps(value, indent=2), encoding='utf-8')
    os.replace(temporary, path)


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for data in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            h.update(data)
    return h.hexdigest()


def is_reparse(path):
    return bool(path.lstat().st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT)


def inventory(root):
    result = []
    for folder, directories, files in os.walk(root, followlinks=False):
        for name in directories + files:
            if is_reparse(Path(folder) / name):
                raise ValueError('Nested reparse point requires separate inspection: ' + str(Path(folder) / name))
        for name in sorted(files):
            path = Path(folder) / name
            info = path.stat()
            result.append((path.relative_to(root).as_posix(), info.st_size, info.st_mtime_ns))
    return sorted(result)


def copy_verified_file(source, stage, row):
    relative, size, mtime = row
    original, copied = source / relative, stage / relative
    copied.parent.mkdir(parents=True, exist_ok=True)
    source_hash = hashlib.sha256()
    with original.open('rb') as reader, copied.open('xb') as writer:
        for data in iter(lambda: reader.read(4 * 1024 * 1024), b''):
            source_hash.update(data)
            writer.write(data)
    if (original.stat().st_size, original.stat().st_mtime_ns) != (size, mtime):
        raise RuntimeError('Source changed while copying: ' + relative)
    sha = source_hash.hexdigest()
    if digest(copied) != sha:
        raise RuntimeError('Destination hash mismatch: ' + relative)
    shutil.copystat(original, copied)
    return dict(path=relative, bytes=size, sha256=sha)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--run-name', default='2026-10-13-return-to-f')
    parser.add_argument('--workers', type=int, default=16)
    args = parser.parse_args()
    project = args.project.resolve()
    if project.name != 're-escape-from-duckov' or project.drive.upper() != 'F:':
        raise ValueError('This operation is limited to the inspected Duckov project on F:')
    local = project / 'local'
    run_name = args.run_name
    if Path(run_name).name != run_name:
        raise ValueError('run-name must be a single filename component')
    run = local / 'storage-migration' / run_name
    if run.exists():
        raise FileExistsError('Preserve previous migration evidence: ' + str(run))
    mappings = [
        ('.git', 'D:/CodexBackups/DuckovLocalGitDirectory-2026-10-13'),
        ('UnityProject/Library', 'D:/CodexCaches/duckov-restoration-2022.3.62f3c1'),
        ('NativeProbe', 'D:/CodexCaches/duckov-restoration-native-probes/NativeProbe'),
        ('NativeMerged', 'D:/CodexCaches/duckov-restoration-merged-assets'),
    ]
    for name in ('NativeRuntimeABBinding', 'NativeRuntimeABBindingUniqueDebug',
                 'NativeRuntimeABBindingUniquePreload', 'NativeRuntimeLocalScriptProbe'):
        mappings.append((name, 'D:/CodexCaches/duckov-restoration-native-probes/AssetBindingExperiments/' + name))
    mappings.append(('Player', 'D:/CodexCaches/duckov-restoration-full-game-v3'))
    archive_names = ('exact-script-probe', 'full-game', 'full-game-v2',
                     'merged-assets-v2', 'merged-lazy', 'merged-player-binding',
                     'native-player', 'native-preloaded', 'native-schemas',
                     'player-script-ids')
    (local / 'StorageExperiments').mkdir(exist_ok=True)
    for suffix in archive_names:
        name = 'duckov-restoration-' + suffix
        mappings.append(('StorageExperiments/' + name, 'D:/CodexCaches/' + name))
    plans = []
    for relative, source_text in mappings:
        destination = local / relative
        source = Path(source_text).resolve(strict=True)
        # Validate the physical parent before any directory rename.
        destination.parent.resolve(strict=True).relative_to(local)
        if relative == 'Player' or relative.startswith('StorageExperiments/'):
            if destination.exists():
                raise FileExistsError(destination)
        elif not is_reparse(destination) or destination.resolve(strict=True) != source:
            raise ValueError('Unexpected link or target: ' + str(destination))
        stage = destination.with_name(destination.name + '.storage-stage-' + run_name)
        retired = destination.with_name(destination.name + '.storage-retired-link-' + run_name)
        if stage.exists() or retired.exists():
            raise FileExistsError('Preserve prior storage stages')
        files = inventory(source)
        plans.append(dict(relative=relative, source=str(source), destination=str(destination),
                          stage=str(stage), retired=str(retired), files=files,
                          bytes=sum(row[1] for row in files), status='pending'))
    free = shutil.disk_usage(local).free
    required = sum(plan['bytes'] for plan in plans)
    if free < required + 5 * 1024 ** 3:
        raise OSError('Insufficient free space for all copies plus 5 GiB working reserve')
    record = dict(status='copying', sourceRetained=True, freeBytesBefore=free,
                  bytesToCopy=required, filesToCopy=sum(len(p['files']) for p in plans),
                  directories=[{k: v for k, v in p.items() if k != 'files'} for p in plans])
    write_json(run / 'progress.json', record)
    completed_bytes = completed_files = 0
    last_message = time.monotonic()
    for number, plan in enumerate(plans):
        source, destination, stage = map(Path, (plan['source'], plan['destination'], plan['stage']))
        stage.mkdir()
        manifest = []
        print('Copying and verifying ' + plan['relative'], flush=True)
        with ThreadPoolExecutor(max_workers=max(1, min(32, args.workers))) as executor:
            for result in executor.map(lambda row: copy_verified_file(source, stage, row), plan['files']):
                manifest.append(result)
                completed_bytes += result['bytes']
                completed_files += 1
                if time.monotonic() - last_message >= 8:
                    print(json.dumps(dict(files=completed_files, totalFiles=record['filesToCopy'],
                                          GiB=round(completed_bytes / 1024 ** 3, 3),
                                          totalGiB=round(required / 1024 ** 3, 3))), flush=True)
                    last_message = time.monotonic()
        if inventory(source) != plan['files']:
            raise RuntimeError('Source inventory changed; retain original active link: ' + str(source))
        write_json(run / ('manifest-' + str(number) + '.json'), manifest)
        # Both destination and stage parents are explicitly inside this project.
        stage.parent.resolve(strict=True).relative_to(local)
        destination.parent.resolve(strict=True).relative_to(local)
        if plan['relative'] != 'Player' and not plan['relative'].startswith('StorageExperiments/'):
            if not is_reparse(destination) or destination.resolve(strict=True) != source:
                raise RuntimeError('Active link changed before switch')
            retired = Path(plan['retired'])
            os.rename(destination, retired)  # Same-volume rename of the link itself.
            try:
                os.rename(stage, destination)
            except BaseException:
                os.rename(retired, destination)
                raise
            if not is_reparse(retired) or retired.resolve(strict=True) != source:
                raise RuntimeError('Retired link does not match inspected source')
            os.rmdir(retired)  # Non-recursive removal of the junction entry only.
            if not source.is_dir():
                raise RuntimeError('Original D copy must remain available')
        else:
            os.rename(stage, destination)
        plan['status'] = 'verified-and-active-on-f'
        record['directories'][number]['status'] = plan['status']
        record['completedFiles'], record['completedBytes'] = completed_files, completed_bytes
        write_json(run / 'progress.json', record)
        print('Active on F: ' + plan['relative'], flush=True)
    record.update(status='complete', allFileSHA256Matched=True,
                  freeBytesAfter=shutil.disk_usage(local).free)
    write_json(run / 'progress.json', record)
    write_json(project / 'evidence/storage-migration.json', record)
    print(json.dumps(dict(status='complete', files=completed_files, bytes=completed_bytes,
                          freeBytesAfter=record['freeBytesAfter'])), flush=True)


if __name__ == '__main__':
    main()
