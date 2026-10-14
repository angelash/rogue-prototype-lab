"""Version all private inputs, recovered code, assets, tooling and project snapshot."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from common import PROJECT,LOCAL,write


def git(*args):
    r=subprocess.run(['git','-C',str(LOCAL),*args],capture_output=True,check=False)
    if r.returncode:
        log=LOCAL/'logs/git-backup-error.txt';log.parent.mkdir(exist_ok=True)
        log.write_bytes(r.stdout+r.stderr)
        raise RuntimeError(f"git {args[0]} exited {r.returncode}; diagnostic: {log}\n"+r.stderr.decode('utf8',errors='replace')[-2000:])
    return r.stdout.decode('utf8',errors='replace').strip()


def main():
    assert (LOCAL/'.git').is_dir() and not git('remote')
    # Database and transient logs are repeatable analysis output; all source,
    # canonical data/media and native instruction derivatives stay versioned.
    (LOCAL/'.gitignore').write_text('/logs/\n/catalog.sqlite3*\n**/__pycache__/\n*.py[cod]\n',encoding='utf8',newline='\n')
    (LOCAL/'.gitattributes').write_text('*.apk binary\n*.dll binary\n*.so binary\n*.png binary\n*.jpg binary\n*.wav binary\n*.native binary\n*.bin binary\n*.bytes binary\n*.gz binary\n*.jar binary\n',encoding='utf8',newline='\n')
    snapshot=LOCAL/'project-snapshot'
    snapshot.mkdir(exist_ok=True)
    for folder in ['scripts','reports','sources','evidence']:
        destination=snapshot/folder
        destination.mkdir(exist_ok=True)
        for source in (PROJECT/folder).rglob('*'):
            if not source.is_file() or '__pycache__' in source.parts or source.suffix=='.pyc':continue
            target=destination/source.relative_to(PROJECT/folder);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
    for name in ['README.md','DEVELOPMENT.md']:
        shutil.copyfile(PROJECT/name,snapshot/name)
    # Evidence records describe completed prior steps. The new backup record is
    # written after the snapshot commit, so any snapshot copy is historical.
    git('config','core.autocrlf','false');git('config','core.longpaths','true');git('config','core.compression','1');git('config','pack.window','0');git('config','pack.threads','2')
    git('add','--all')
    if git('diff','--cached','--name-only'):git('commit','-m','Preserve complete authorized Xiaoxiaoshenbing static recovery')
    commit=git('rev-parse','HEAD');git('fsck','--full')
    original=next((LOCAL/'input').glob('*.apk'));relative=original.relative_to(LOCAL).as_posix()
    proc=subprocess.Popen(['git','-C',str(LOCAL),'cat-file','blob',f'{commit}:{relative}'],stdout=subprocess.PIPE)
    digest=hashlib.sha256()
    for block in iter(lambda:proc.stdout.read(1024*1024),b''):digest.update(block)
    proc.stdout.close();assert proc.wait()==0
    expected=json.loads((PROJECT/'evidence/preparation.json').read_text('utf8'))['apk_sha256']
    files=git('ls-files','-z').split('\0');files=[f for f in files if f]
    clean=not git('status','--porcelain')
    record={'date':'2026-10-14','repository':'local/.git','remotes':[],'commit':commit,'tracked_files':len(files),
        'input_blob_sha256':digest.hexdigest(),'input_matches':digest.hexdigest()==expected,'git_fsck_pass':True,'working_tree_clean':clean,
        'scope':'All inputs, reconstructed code/IL indexes, Unity trees/config/media, native/DEX derivatives, tools and project snapshot; repeatable SQLite/log/cache output excluded.'}
    write(PROJECT/'evidence/deep-backup.json',record);print(json.dumps(record,ensure_ascii=True))
    assert record['input_matches'] and clean


if __name__=='__main__':main()
