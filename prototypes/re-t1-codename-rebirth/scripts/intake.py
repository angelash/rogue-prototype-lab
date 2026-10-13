"""Non-destructive APK intake. Python standard library only."""
import argparse
import collections
import hashlib
import json
import shutil
import sys
import zipfile
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8', errors='backslashreplace')
def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for data in iter(lambda: f.read(4*1024*1024), b''):
            h.update(data)
    return h.hexdigest()
def main():
    p = argparse.ArgumentParser(); p.add_argument('--source', required=True)
    args = p.parse_args(); source = Path(args.source).resolve(strict=True)
    local = PROJECT/'local'; copy = local/'input'/source.name
    copy.parent.mkdir(parents=True, exist_ok=True)
    before = source.stat(); source_hash = digest(source)
    if copy.exists():
        if digest(copy) != source_hash:
            raise RuntimeError('Existing copy differs; preserve it and select a new project')
    else:
        shutil.copy2(source, copy)
    copy_hash = digest(copy); assert copy_hash == source_hash
    dest = local/'apk'; dest.mkdir(parents=True, exist_ok=True)
    entries=[]; failures=[]
    with zipfile.ZipFile(copy) as z:
        assert len(z.namelist()) == len(set(z.namelist())), 'Duplicate archive names'
        for i, info in enumerate(z.infolist()):
            path=(dest/info.filename).resolve()
            assert path.is_relative_to(dest.resolve()), 'Archive path escape'
            if info.is_dir():
                path.mkdir(parents=True, exist_ok=True); continue
            path.parent.mkdir(parents=True, exist_ok=True)
            h=hashlib.sha256(); head=b''; size=0
            try:
                # ZipExtFile validates CRC when EOF is reached.
                with z.open(info) as src, path.open('wb') as out:
                    for data in iter(lambda:src.read(1024*1024),b''):
                        if not head: head=data[:32]
                        h.update(data);out.write(data);size+=len(data)
                assert size==info.file_size
                status='ok'
            except Exception as ex:
                status='failed';failures.append({'path':info.filename,'error':str(ex)})
            entries.append({'path':info.filename,'bytes':info.file_size,'compressedBytes':info.compress_size,
                'crc32':f'{info.CRC:08x}','sha256':h.hexdigest(),'headerHex':head.hex(),'status':status})
            if i%250==0: print(f'intake {i}/{len(z.infolist())}',flush=True)
    assert source.stat().st_size==before.st_size and source.stat().st_mtime_ns==before.st_mtime_ns
    result={'date':'2026-10-13','source':str(source),'copy':str(copy),'bytes':before.st_size,
        'sourceSHA256':source_hash,'copySHA256':copy_hash,'files':len(entries),
        'uncompressedBytes':sum(x['bytes'] for x in entries),'failures':failures,
        'topGroups':dict(collections.Counter(x['path'].split('/')[0] for x in entries)),
        'copyMatches':source_hash==copy_hash,'sourceUnchanged':True}
    write(PROJECT/'evidence/apk-files.json',entries);write(PROJECT/'evidence/intake.json',result)
    print(json.dumps(result,ensure_ascii=False,indent=2))
    if failures: sys.exit(1)
if __name__=='__main__':main()
