"""Snapshot complete local research, keep it offline, and make a Git bundle."""
import argparse,datetime,json,subprocess
from pathlib import Path
from prepare import write,sha

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);ap.add_argument('--bundle',required=True);a=ap.parse_args()
    project=Path(a.project).resolve();local=project/'local';bundle=Path(a.bundle).resolve()
    if bundle.is_relative_to(local):raise ValueError('Bundle must be outside its input repository')
    def git(*args):return subprocess.check_output(['git',*args],cwd=local,text=True,encoding='utf-8').strip()
    if not (local/'.git').exists():
        git('init','-b','local/duckov-2.3.30');git('config','core.autocrlf','false');git('config','core.longpaths','true')
        (local/'.gitattributes').write_text('* -text\n',encoding='utf-8')
        (local/'.gitignore').write_text('/tools/\n/assets.sqlite-*\n',encoding='utf-8')
    if git('remote'):raise ValueError('Local proprietary input repo must have no remote')
    git('add','--all')
    if git('status','--porcelain'):git('commit','-m','Snapshot Duckov local research inputs and analysis outputs','--quiet')
    commit=git('rev-parse','HEAD');tree=git('rev-parse','HEAD^{tree}')
    tracked=subprocess.check_output(['git','ls-files','-z'],cwd=local).decode('utf-8').split('\0')[:-1]
    tracked_set=set(tracked)
    manifest=json.loads((project/'evidence/build-files.json').read_text('utf-8'))
    for r in manifest:
        rel='build-copy/'+r['path'];assert rel in tracked_set
        assert git('hash-object','--no-filters',rel)==git('rev-parse','HEAD:'+rel),rel
    expected={f.relative_to(local).as_posix() for f in local.rglob('*') if f.is_file() and f.relative_to(local).parts[0] not in ('.git','tools') and not f.name.startswith('assets.sqlite-')}
    assert expected==tracked_set,dict(missing=sorted(expected-tracked_set),extra=sorted(tracked_set-expected))
    print(f'All {len(tracked)} local research files tracked; {len(manifest)} input blobs checked',flush=True)
    subprocess.run(['git','fsck','--full','--no-progress'],cwd=local,check=True)
    print('Local object database integrity passed',flush=True)
    if bundle.exists():raise FileExistsError('Choose a fresh bundle filename; existing backup is never overwritten')
    bundle.parent.mkdir(parents=True,exist_ok=True)
    subprocess.run(['git','-c','pack.window=0','-c','core.compression=1','bundle','create',str(bundle),'--all'],cwd=local,check=True)
    subprocess.run(['git','bundle','verify',str(bundle)],cwd=local,check=True)
    rec=dict(date=datetime.datetime.now().isoformat(),localRepository=str(local),localCommit=commit,localTree=tree,
        localTrackedFiles=len(tracked),localTrackedBytes=sum((local/r).stat().st_size for r in tracked),allLocalResearchTracked=True,
        installationBlobsVerified=len(manifest),localGitFsck=True,localRemote=None,
        bundle=dict(path=str(bundle),bytes=bundle.stat().st_size,sha256=sha(bundle),verified=True),
        reportBackup=dict(status='pending',branch='codex/duckov-analysis',includesThirdPartyBodiesOrMedia=False))
    write(project/'evidence/backup.json',rec);print(json.dumps(rec),flush=True)

if __name__=='__main__':main()
