"""Copy and hash every installation file; never replace mismatched existing data."""
from pathlib import Path
import argparse, configparser, datetime, hashlib, json, shutil, time

def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(4 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()

def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--source', required=True)
    ap.add_argument('--project', required=True)
    args = ap.parse_args()
    source, project = Path(args.source).resolve(), Path(args.project).resolve()
    dest = project / 'local/build-copy'
    if source == dest or source.is_relative_to(project):
        raise ValueError('Input must be separate from the project')
    manifest = []
    for i, p in enumerate(sorted(source.rglob('*'))):
        if not p.is_file(): continue
        rel = p.relative_to(source); q = dest / rel
        digest = sha(p)
        if not q.exists():
            q.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(p, q)
        copied = sha(q)
        if digest != copied: raise ValueError(f'Existing copy mismatch: {rel}')
        manifest.append(dict(path=rel.as_posix(), bytes=p.stat().st_size,
                             sha256=digest, copyMatches=True))
        if len(manifest) % 50 == 0: print(f'Copied and verified {len(manifest)} files', flush=True)
    write(project / 'evidence/build-files.json', manifest)
    info=configparser.ConfigParser();info.read(dest/'Info.ini',encoding='utf-8')
    write(project / 'evidence/build-summary.json', dict(files=len(manifest),
          bytes=sum(x['bytes'] for x in manifest), gameVersion=info.get('metadata','version'),
          source=str(source), copy=str(dest), backend='Mono', analysisDate=datetime.date.today().isoformat()))
    print(f'Complete: {len(manifest)} files, {sum(x["bytes"] for x in manifest)} bytes', flush=True)

if __name__ == '__main__': main()
