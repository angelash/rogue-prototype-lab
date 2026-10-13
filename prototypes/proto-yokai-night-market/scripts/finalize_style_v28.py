"""Close the v2.8 art revision with current evidence, retaining earlier scopes honestly."""
from pathlib import Path
import json,hashlib,shutil
from datetime import datetime,timezone
ROOT=Path(__file__).resolve().parents[1];LOCAL=ROOT/'local';PROJECT=LOCAL/'UnityProject'
def read(p):return json.loads(p.read_text('utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def main():
    verification=read(ROOT/'fidelity/evidence/fidelity-verification.json');contract=read(LOCAL/'style-contract-v2.8.json');promotion=read(LOCAL/'style-promotion-v2.8.json')
    assert verification['passed'] and contract['passed'] and contract['project']=='UnityProject' and promotion['passed']
    backup=read(LOCAL/'backup-report.json');assert backup['revision']=='v2.8-style-r3'
    inputs=[dict(path=p.relative_to(PROJECT).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for folder in ('Assets','Packages','ProjectSettings') for p in (PROJECT/folder).rglob('*') if p.is_file()];inputs.sort(key=lambda v:v['path'])
    assert sorted(read(ROOT/backup['source']['manifest']),key=lambda v:v['path'])==inputs
    for entry in backup['source'],backup['player']:assert entry['readbackVerified'] and sha(ROOT/entry['path'])==entry['sha256']
    for item in read(ROOT/backup['player']['manifest']):assert sha(LOCAL/'Builds/Release'/item['path'])==item['sha256']
    write(LOCAL/'source-inputs.json',inputs);fingerprint=hashlib.sha256(json.dumps(inputs,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
    reports={};total=0
    for family,folder,count in [('visual','visual-complete',128),('surface','surfaces',26),('prompt','prompts',33)]:
        for aspect,width in [('16x9',1920),('4x3',1440)]:
            for lang,suffix in [(0,''),(1,'-en')]:
                revision='-v2.8-style-r3'
                relative=f'local/QAResults/{folder}-{aspect}{revision}{suffix}/report.json';p=ROOT/relative;r=read(p)
                assert r['passed'] and not r['errors'] and r['width']==width and r['height']==1080 and r['language']==lang and len(r['captures'])==count
                if family=='visual':assert r['verifiedCardLanguages']==95 and r['verifiedProjectLanguages']==28 and r['negativeBandInkChecks']==52 and r['tutorialTextBoundsChecks']>0
                if family=='surface':assert r['phaseChecks']==7 and r['optionChecks']==10
                if family=='prompt':assert r['projectHeaderChecks']==28 and r['boundsChecks']>=4
                key=f'{family}-{aspect}-{suffix or "zh"}';reports[key]=dict(localPath=relative,sha256=sha(p),report=r);total+=count
    for name,filename,count in [('rules','rules.json',40),('flow','report.json',15)]:
        p=LOCAL/f'QAResults/{name}-v2.8-style-r3/{filename}';r=read(p);assert r['passed'] and not r.get('errors') and len(r['rules' if name=='rules' else 'checks'])==count
        reports[name]=dict(localPath=p.relative_to(ROOT).as_posix(),sha256=sha(p),report=r)
    metadata=read(LOCAL/'release-code-metadata.json');qaTypes=[t['name'] for t in metadata['types'] if any(k in t['name'] for k in ('NightSmokeProbe','NightRuleChecks','NightVisualProbe','NightFlowProbe','NightPromptProbe','NightSurfaceProbe'))];assert not qaTypes
    release=read(LOCAL/'release-build-report.json');assert release['result']=='Succeeded' and release['errors']==0 and not release['development']
    jobs=read(ROOT/'fidelity/art/jobs.json');images={r['id']:r for r in verification['images']};spec=read(ROOT/'fidelity/image-specs.json')
    for j in jobs['jobs']:j.update(status='imported_verified',runtimeSHA256=images[j['id']]['runtimeSHA256'])
    for s in spec['textures']:
        j=next(j for j in jobs['jobs'] if j['id']==s['id']);s.update(replacement=j['final'],raw=j['raw'],prompt=j['prompt'],kind=j['kind'],status='imported_verified',rawSHA256=images[j['id']]['rawSHA256'],runtimeSHA256=images[j['id']]['runtimeSHA256'])
        if 'materialSource' in j:s.update(materialSource=j['materialSource'],surfaceSpec=j['surfaceSpec'])
    write(ROOT/'fidelity/art/jobs.json',jobs);write(ROOT/'fidelity/image-specs.json',spec)
    ledger=read(ROOT/'fidelity/art-direction-v2.8/generation-register.json')
    for r in ledger['images']:
        if r['name'] in ('indigo-paper-v1','neutral-paper-v1','card-lower-master-v2'):r['status']='production_source_integrated_verified'
        if r['name']=='card-lower-master-v1':r['status']='rejected_frame_overlap_history'
    write(ROOT/'fidelity/art-direction-v2.8/generation-register.json',ledger)
    plan=read(ROOT/'fidelity/art-direction-v2.8/replacements.json');plan.update(status='integrated_verified_in_current_player',version='2.8-style-r3')
    for e in plan['entries']:e['productionStatus']='retained_bytes_verified_and_family_reviewed' if e['action']=='retain' else 'imported_dimension_alpha_verified_and_player_reviewed'
    write(ROOT/'fidelity/art-direction-v2.8/replacements.json',plan)
    palette=read(ROOT/'fidelity/art-direction-v2.8/palette.json');palette.update(status='applied_verified',version='2.8-style-r3');write(ROOT/'fidelity/art-direction-v2.8/palette.json',palette)
    install=Path('E:/SteamLibrary/steamapps/common/Crunch Time');original=read(ROOT.parent/'re-crunch-time/evidence/build-files.json');assert all(sha(install/e['path'])==e['sha256'] for e in original)
    native=read(LOCAL/'NativeQA-v2.8-style-r3/report.json');assert all(c['passed'] for c in native['cases'])
    for c in native['captures']:assert sha(ROOT/c['path'])==c['sha256']
    native.update(sourceInputSHA256=fingerprint,releasePlayerSHA256=sha(LOCAL/'Builds/Release/YokaiNightMarket.exe'),releaseAssemblySHA256=sha(LOCAL/'Builds/Release/YokaiNightMarket_Data/Managed/Assembly-CSharp.dll'))
    write(LOCAL/'NativeQA-v2.8-style-r3/report.json',native)
    destination=ROOT/'fidelity/evidence/delivery.json';history=LOCAL/'history/delivery-v2.7-before-style.json';history.parent.mkdir(parents=True,exist_ok=True)
    if not history.exists():shutil.copy2(destination,history)
    previous=read(history)
    out=dict(version='2.8',revision='v2.8-style-r3',date=datetime.now(timezone.utc).isoformat(),status='Night-market visual replacement integrated and checked; complete original-game experience equivalence remains unproven',unity='2022.3.62f3c1',platform='Windows x64 Mono',project='local/UnityProject',scene='Assets/Recovery/Scenes/NightMarket.unity',player='local/Builds/Release/YokaiNightMarket.exe',release=release,releaseQAClasses=qaTypes,sourceInputFiles=len(inputs),sourceInputSHA256=fingerprint,sourceInputManifest='local/source-inputs.json',releaseAssemblySHA256=native['releaseAssemblySHA256'],originalInstallUnchanged=True,originalInstallFilesVerified=len(original),images=dict(imported=82,replacedUITextures=38,retainedTextures=44,historicalGeneratedSources=82,newGeneratedMasters=4,currentGeneratedMastersUsed=3,referenceArtNeverSuppliedToImagegen=True),styleContract=contract,promotion=promotion,qa=reports,currentPlayerCaptures=total,native=dict(completeAcceptance=False,testedCurrentBuild=True,checks=len(native['cases']),localPath='local/NativeQA-v2.8-style-r3/report.json',sha256=sha(LOCAL/'NativeQA-v2.8-style-r3/report.json'),report=native),historicalEvidence=dict(localPath=history.relative_to(ROOT).as_posix(),revision=previous['revision'],scope='Earlier match, native and audio gaps retain their recorded scope; not reclassified as current native acceptance.'),backups=backup,limitations=previous['limitations'],versionControl=dict(fullSourceBranch='codex/yokai-night-market-source',fullSourceRemotePushed=False,scope='Canonical Unity assets/meta, all imagegen masters/prompts, exports, authoring data and tests under Git; outputs/reports excluded; protected recovery inputs never published'))
    assert total==748
    write(destination,out);write(ROOT/'fidelity/evidence/style-contract-v2.8.json',contract)
    print(json.dumps(dict(revision=out['revision'],sourceFiles=len(inputs),currentCaptures=total,nativeChecks=len(native['cases']),releaseErrors=release['errors']),ensure_ascii=False))
if __name__=='__main__':main()
