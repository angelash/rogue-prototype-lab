"""Package private Unity sources and a complete Windows player with byte manifests.
All archives and full manifests stay under local/. No source is uploaded here.
"""
from pathlib import Path
import argparse
import hashlib,json,zipfile
ROOT=Path(__file__).resolve().parents[1]
LOCAL=ROOT/'local'
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
    return h.hexdigest()
def write(path,value):path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def package(base,paths,name):
    archive=LOCAL/'Backups'/name;archive.parent.mkdir(parents=True,exist_ok=True)
    assert archive.resolve().is_relative_to(LOCAL.resolve())
    if archive.exists():raise FileExistsError('Preserve existing backup: '+str(archive))
    entries=[dict(path=p.relative_to(base).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(paths)]
    with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for item in entries:z.write(base/item['path'],arcname=item['path'])
    # Read every archived file back and compare bytes, not only the zip directory.
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        assert all(hashlib.sha256(z.read(i['path'])).hexdigest()==i['sha256'] for i in entries)
    manifest=archive.with_suffix('.files.json');write(manifest,entries)
    return dict(path=archive.relative_to(ROOT).as_posix(),bytes=archive.stat().st_size,sha256=sha(archive),files=len(entries),uncompressedBytes=sum(i['bytes'] for i in entries),readbackVerified=True,manifest=manifest.relative_to(ROOT).as_posix())
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--revision',default='v2');args=parser.parse_args();assert all(c.isalnum() or c in '-.' for c in args.revision)
    release=json.loads((LOCAL/'release-build-report.json').read_text());assert release['result']=='Succeeded' and release['errors']==0 and release['development']==False
    project=LOCAL/'UnityProject';player=LOCAL/'Builds/Release'
    sources=[p for folder in ['Assets','Packages','ProjectSettings'] for p in (project/folder).rglob('*') if p.is_file()]
    runtime=[p for p in player.rglob('*') if p.is_file() and p.suffix.lower() not in {'.log','.pdb','.mdb'} and not any(part.endswith('BackUpThisFolder_ButDontShipItWithYourGame') for part in p.parts)]
    out=dict(version=2,revision=args.revision,scope='Private local restoration backups; not redistribution packages',source=package(project,sources,'YokaiNightMarket-UnitySource-'+args.revision+'.zip'),player=package(player,runtime,'YokaiNightMarket-Windows-'+args.revision+'.zip'))
    write(LOCAL/'backup-report.json',out);print(json.dumps(out,ensure_ascii=False))
if __name__=='__main__':main()
