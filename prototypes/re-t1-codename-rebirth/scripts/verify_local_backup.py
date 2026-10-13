"""Verify the private Git snapshot without publishing its recovered contents."""
import hashlib
import json
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOCAL = ROOT / 'local'


def git(*args):
    return subprocess.check_output(['git', '-C', str(LOCAL), *args])


def main():
    start = time.monotonic()
    head = git('rev-parse', 'HEAD').decode().strip()
    rows = git('ls-tree', '-r', '-l', '-z', head).split(b'\0')
    files = {}
    for row in rows:
        if not row:
            continue
        meta, path = row.split(b'\t', 1)
        mode, kind, oid, size = meta.split()
        assert kind == b'blob', (kind, path)
        files[path.decode('utf8')] = int(size)
    intake = json.loads((ROOT / 'evidence/intake.json').read_text('utf8'))
    apk_path = Path(intake['copy']).relative_to(LOCAL).as_posix()
    proc = subprocess.Popen(['git', '-C', str(LOCAL), 'cat-file', 'blob',
                             f'{head}:{apk_path}'], stdout=subprocess.PIPE)
    digest = hashlib.sha256()
    for block in iter(lambda: proc.stdout.read(2 ** 20), b''):
        digest.update(block)
    assert proc.wait() == 0
    assert digest.hexdigest() == intake['sourceSHA256']
    alternates = LOCAL / '.git/objects/info/alternates'
    mirror = Path(alternates.read_text('utf8').strip()).parent
    mirror_head = subprocess.check_output(['git', '--git-dir', str(mirror),
                                           'rev-parse', 'HEAD']).decode().strip()
    mirror_remotes = subprocess.check_output(['git', '--git-dir', str(mirror),
                                              'remote', '-v']).decode().strip()
    assert mirror_head == head and not mirror_remotes
    assert not (mirror / 'objects/info/alternates').exists()
    fsck = subprocess.run(['git', '--git-dir', str(mirror), 'fsck', '--full'],
                          capture_output=True, text=True, encoding='utf8', errors='replace')
    assert fsck.returncode == 0, fsck.stderr[:4000]
    remotes = git('remote', '-v').decode().strip()
    clean = not git('status', '--porcelain').strip()
    assert not remotes and clean
    counts = {
        'extractedAPK': sum(p.startswith('apk/') for p in files),
        'managedPE': sum(p.startswith('managed/') and p.endswith('.dll') for p in files),
        'canonicalCSharp': sum(p.startswith('source/all/') and p.endswith('.cs') for p in files),
        'decodedBundles': sum(p.startswith('decoded/bundles/') for p in files),
        'configTables': sum(p.startswith('config/tables/') and p.endswith('.json') for p in files),
        'exportedMediaFiles': sum(p.startswith('assets/export/') for p in files),
        'androidNormalJava': sum(p.startswith('android-java/') and p.endswith('.java') for p in files),
        'androidFallbackJava': sum(p.startswith('android-fallback/') and p.endswith('.java') for p in files),
    }
    assert counts['extractedAPK'] == 2051
    assert counts['managedPE'] == 38
    assert counts['canonicalCSharp'] == 17872
    assert counts['decodedBundles'] == 981
    assert counts['configTables'] == 208
    assert counts['androidNormalJava'] == 6719
    assert counts['androidFallbackJava'] == 11502
    assert counts['exportedMediaFiles'] >= 11558 + 333
    evidence = {
        'date': '2026-10-13', 'localRepository': str(LOCAL),
        'gitDirectory': git('rev-parse', '--absolute-git-dir').decode().strip(),
        'standaloneLocalMirror': str(mirror), 'mirrorCommit': mirror_head,
        'mirrorRemotes': mirror_remotes, 'mirrorHasAlternates': False,
        'gitStorageReason': 'F drive was low on space. C holds a complete independent private Git snapshot; local/.git shares its old objects. Work files remain in the project.',
        'migrationAudit': 'An empty hidden .git directory remained after the move. Automatic review rejected its deletion; retained it and used Git shared-clone initialization, then fetched the complete new snapshot into C. No safeguard bypass.',
        'localCommit': head, 'remotes': remotes, 'localClean': clean,
        'trackedFiles': len(files), 'trackedBytes': sum(files.values()),
        'trackedManifestSHA256': hashlib.sha256(git('ls-tree', '-r', '-l', '-z', head)).hexdigest(),
        'originalAPKBlobSHA256': digest.hexdigest(), 'originalAPKBlobVerified': True,
        'trackedGroups': counts, 'gitFsckSource': 'standalone C mirror without alternates',
        'gitFsckExitCode': fsck.returncode,
        'gitFsckDiagnosticLines': len((fsck.stdout + fsck.stderr).splitlines()),
        'gitFsckDiagnosticsSHA256': hashlib.sha256((fsck.stdout + fsck.stderr).encode()).hexdigest(),
        'seconds': round(time.monotonic() - start, 2),
        'scope': 'Complete private input and recovery snapshot; no third-party data pushed to a remote.',
    }
    (ROOT / 'evidence/deep-backup.json').write_text(
        json.dumps(evidence, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print(json.dumps(evidence, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
