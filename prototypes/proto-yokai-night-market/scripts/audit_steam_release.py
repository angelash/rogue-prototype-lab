"""Read-only Steam preparation audit. Evidence presence is not a legal opinion.

Never uploads, logs in, publishes, or changes the canonical research Player.
Full hashes/inventories and private approval records stay in ignored local/.
"""
from pathlib import Path
import argparse, hashlib, json, re

ROOT=Path(__file__).resolve().parents[1]
REPO=ROOT.parents[1]
LOCAL=ROOT/'local'
def read(p): return json.loads(p.read_text('utf-8'))
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for data in iter(lambda:f.read(1024*1024),b''):h.update(data)
    return h.hexdigest()
def fingerprint(entries):
    return hashlib.sha256(json.dumps(sorted(entries,key=lambda e:e['path']),sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
def private_proof(proof,private_root=LOCAL):
    if not isinstance(proof,dict) or not isinstance(proof.get('path'),str):return False
    p=(private_root/proof['path']).resolve()
    return p.is_relative_to(private_root.resolve()) and p.is_file() and proof.get('sha256')==sha(p)
def valid_app_id(app):return type(app) is int and app>0 and app not in (480,340550)
def valid_source_sha(value):return isinstance(value,str) and bool(re.fullmatch('[0-9a-f]{64}',value))
def windows_layout(build,executable):
    if not build or not isinstance(executable,str) or Path(executable).name!=executable or not executable.lower().endswith('.exe'):return False
    stem=Path(executable).stem
    required=[executable,'UnityPlayer.dll',stem+'_Data/globalgamemanagers',stem+'_Data/Managed/Assembly-CSharp.dll','MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll']
    return all((build/name).is_file() for name in required)
def files_match(build,entries):
    if not build.is_dir() or not entries:return False
    expected=set()
    for item in entries:
        p=(build/item['path']).resolve()
        if not p.is_relative_to(build.resolve()) or not p.is_file() or p.stat().st_size!=item['bytes'] or sha(p)!=item['sha256']:return False
        expected.add(item['path'])
    actual={p.relative_to(build).as_posix() for p in build.rglob('*') if p.is_file()}
    return expected==actual
def evaluate(product,rights,evidence,private_root=LOCAL):
    route=product.get('commercialRoute')
    approvals=evidence.get('rights',{})
    clearance=bool(rights['groups']) and all(g['status']=='cleared' and private_proof(approvals.get(g['id']),private_root) for g in rights['groups'])
    candidate=evidence.get('candidate',{})
    source=product.get('candidateSourceSHA256')
    build=Path(product['candidateBuild']).resolve() if product.get('candidateBuild') else None
    research=(ROOT/'local/Builds/Release').resolve()
    build_ok=bool(build and build.is_relative_to(private_root.resolve()) and build!=research and not build.is_relative_to(research) and files_match(build,candidate.get('files',[])))
    manifest_hash=fingerprint(candidate['files']) if candidate.get('files') else None
    receipts=evidence.get('quality',{})
    quality=[]
    for key in ('complete-native','audio-listening','performance-minimum','clean-install','steam-installed-build'):
        proof=receipts.get(key,{})
        ok=private_proof(proof,private_root)
        if ok:
            data=read((private_root/proof['path']).resolve())
            ok=data.get('passed') is True and valid_source_sha(source) and data.get('sourceSHA256')==source and data.get('buildManifestSHA256')==manifest_hash
        quality.append(dict(id=key,passed=ok))
    gates=[
        dict(id='commercial-route',passed=route in ('licensed','independent')),
        dict(id='rights-and-dependency-clearance',passed=clearance),
        dict(id='own-app-and-depot',passed=valid_app_id(product.get('appId')) and type(product.get('windowsDepotId')) is int and product['windowsDepotId']>0),
        dict(id='candidate-directory-and-all-bytes',passed=build_ok),
        dict(id='complete-windows-mono-layout',passed=windows_layout(build,product.get('executable'))),
        dict(id='candidate-source-identity',passed=valid_source_sha(source) and source==candidate.get('sourceSHA256')),
        dict(id='support-and-publisher',passed=bool(product.get('publisherDisplayName')) and '@' in (product.get('supportEmail') or '')),
    ]+quality
    return dict(readyForBuildReview=all(g['passed'] for g in gates),gates=gates,failedGates=[g['id'] for g in gates if not g['passed']],candidateManifestSHA256=manifest_hash,scope='Receipt integrity and declared gates only; platform state and legal sufficiency still require actual review.')
def inventory():
    project=LOCAL/'UnityProject';assets=project/'Assets'
    reference=REPO/'prototypes/re-crunch-time/local/exports'
    original_hashes={sha(p) for p in reference.rglob('*.wav')}
    audio=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p),matchesOriginalExport=sha(p) in original_hashes) for p in assets.rglob('*.wav')]
    entries=[dict(path=p.relative_to(project).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for folder in ('Assets','Packages','ProjectSettings') for p in (project/folder).rglob('*') if p.is_file()]
    return dict(canonicalSourceSHA256=fingerprint(entries),canonicalSourceFiles=len(entries),audio=audio,originalAudioHashMatches=sum(a['matchesOriginalExport'] for a in audio),recoveredBusinessScripts=len([p for p in (assets/'Recovery/Scripts').glob('*.cs') if p.stem not in ('LegacyEngine','LegacyObjectSlot')]),nightFonts=len(list((assets/'Recovery/NightFonts').glob('*.otf'))),historicalTTFInputs=len(list((assets/'Recovery/Media').glob('*.ttf'))),fontNoticePresent=(assets/'StreamingAssets/ThirdPartyNotices/NotoSansCJK-OFL-1.1.txt').is_file(),steamApiDllInCurrentRelease=bool(list((LOCAL/'Builds/Release').glob('**/steam_api*.dll'))))
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--enforce',action='store_true');args=parser.parse_args()
    product=read(ROOT/'release/product.json');rights=read(ROOT/'release/rights-register.json')
    private=LOCAL/'steam-release/private-evidence.json';evidence=read(private) if private.exists() else {}
    out=evaluate(product,rights,evidence);out.update(date='2026-10-14',inventory=inventory())
    target=LOCAL/'steam-release/readiness.json';target.parent.mkdir(parents=True,exist_ok=True);target.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print(json.dumps(dict(readyForBuildReview=out['readyForBuildReview'],failedGates=out['failedGates'],audioOriginalHashMatches=out['inventory']['originalAudioHashMatches'],canonicalSourceSHA256=out['inventory']['canonicalSourceSHA256'],report=target.relative_to(ROOT).as_posix()),ensure_ascii=False))
    return 2 if args.enforce and not out['readyForBuildReview'] else 0
if __name__=='__main__':raise SystemExit(main())
