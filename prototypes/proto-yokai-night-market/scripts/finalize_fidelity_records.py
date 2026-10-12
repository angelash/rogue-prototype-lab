"""Publish original-data delivery metadata after reading actual local evidence."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,shutil
ROOT=Path(__file__).resolve().parents[1]
LOCAL=ROOT/'local';PROJECT=LOCAL/'UnityProject';SOURCE=ROOT.parent/'re-crunch-time'
def read(p):return json.loads(p.read_text('utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def main():
    verification=read(ROOT/'fidelity/evidence/fidelity-verification.json');assert verification['passed']
    images={r['id']:r for r in verification['images']};jobs=read(ROOT/'fidelity/art/jobs.json');spec=read(ROOT/'fidelity/image-specs.json');book=read(ROOT/'fidelity/art/generation-register.json')
    for j in jobs['jobs']:j['status']='imported_verified';j['runtimeSHA256']=images[j['id']]['runtimeSHA256']
    for s in spec['textures']:
        j=next(j for j in jobs['jobs'] if j['id']==s['id']);s.update(replacement=j['final'],raw=j['raw'],prompt=j['prompt'],kind=j['kind'],status='imported_verified',rawSHA256=images[s['id']]['rawSHA256'],runtimeSHA256=images[s['id']]['runtimeSHA256'])
    for r in book['images']:r['review']='Raw image inspected; exact runtime dimensions and copy hashes verified; actual Player family samples checked separately in delivery evidence.'
    write(ROOT/'fidelity/art/jobs.json',jobs);write(ROOT/'fidelity/image-specs.json',spec);write(ROOT/'fidelity/art/generation-register.json',book)
    reports={}
    for key,relative in [('rules','QAResults/rules/rules.json'),('smoke16x9','QAResults/smoke-16x9/report.json'),('smoke4x3','QAResults/smoke-4x3/report.json'),('visual16x9','QAResults/visual-complete-16x9-v2.2/report.json'),('visual4x3','QAResults/visual-complete-4x3-v2.2/report.json'),('match','QAResults/match-v2.2/report.json')]:
        p=LOCAL/relative;r=read(p);assert r['passed'],key;reports[key]=dict(report=r,localPath='local/'+relative,sha256=sha(p));write(ROOT/'fidelity/evidence'/(key+'.json'),r)
    assert len(reports['rules']['report']['rules'])==40 and all(r['passed'] for r in reports['rules']['report']['rules'])
    for label,size in [('16x9',(1920,1080)),('4x3',(1440,1080))]:
        for family in ['smoke','visual']:
            report=reports[family+label]['report'];assert (report['width'],report['height'])==size and report['cards']==95 and report['projects']==28
        report=reports['visual'+label]['report'];assert report['tutorialPages']==2 and len(report['captures'])==128 and report['qaFrameRate']==60
    match=reports['match']['report'];assert match['state']==50 and match['replayReady'] and len(match['winnerDone'])==5 and all(done>=goal for done,goal in zip(match['winnerDone'],match['winnerGoals']))
    metadata=read(LOCAL/'release-code-metadata.json');qaTypes=[t['name'] for t in metadata['types'] if any(k in t['name'] for k in ['NightSmokeProbe','NightRuleChecks','NightVisualProbe'])];assert not qaTypes
    original=read(SOURCE/'evidence/build-files.json');installation=Path('E:/SteamLibrary/steamapps/common/Crunch Time');unchanged=all(sha(installation/f['path'])==f['sha256'] for f in original);assert unchanged
    inputs=[]
    for folder in ['Assets','Packages','ProjectSettings']:
        for p in sorted((PROJECT/folder).rglob('*')):
            if p.is_file():inputs.append(dict(path=p.relative_to(PROJECT).as_posix(),bytes=p.stat().st_size,sha256=sha(p)))
    write(LOCAL/'source-inputs.json',inputs)
    backup=read(LOCAL/'backup-report.json');assert read(ROOT/backup['source']['manifest'])==inputs
    for bundle in ['source','player']:
        record=backup[bundle];assert record['readbackVerified'] and sha(ROOT/record['path'])==record['sha256']
    for item in read(ROOT/backup['player']['manifest']):assert sha(LOCAL/'Builds/Release'/item['path'])==item['sha256']
    fingerprint=hashlib.sha256(json.dumps(inputs,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
    release=read(LOCAL/'release-build-report.json');assert release['result']=='Succeeded' and release['errors']==0
    out=dict(version=2,revision=backup['revision'],date=datetime.now(timezone.utc).isoformat(),status='local playable restoration candidate; native and pixel-equivalence acceptance incomplete',unity='2022.3.62f3c1',platform='Windows x64 Mono',scene='Assets/Recovery/Scenes/NightMarket.unity',project='local/UnityProject',player='local/Builds/Release/YokaiNightMarket.exe',release=release,sourceInputFiles=len(inputs),sourceInputSHA256=fingerprint,sourceInputManifest='local/source-inputs.json',releaseAssemblySHA256=sha(LOCAL/'Builds/Release/YokaiNightMarket_Data/Managed/Assembly-CSharp.dll'),releaseQAClasses=qaTypes,originalInstallFilesVerified=len(original),originalInstallUnchanged=unchanged,structure=read(LOCAL/'structure-audit.json'),prefabs=read(LOCAL/'prefab-report.json'),images=dict(generated=82,imported=82,referenceArtNeverSuppliedToImagegen=True),qa=reports,backups=backup,native=dict(passed=False,status='pending user cancellation of existing Windows firewall security prompt; no native input or listening claimed'),limitations=['No Unity 4 vs Unity 2022 same-seed RNG or rasterizer equivalence proof','No complete native keyboard/mouse, original-speed experience, audio listening or performance acceptance','Uncapped batch-mode tutorial fade stalls were observed; final QA uses a QA-only 60 FPS cap, while release keeps source settings; cross-version cause not established','Original Unity 4 startup resolution/quality picker not rebuilt','Scene roots reconstructed as prefabs; original Editor-only prefab connections absent','Recovered code, original audio and stencil derivatives remain local only'])
    out['qaRevisionScope']={'beforeFinalHeaderArt':['rules','smoke16x9','smoke4x3'],'finalHeaderArt':['visual16x9','visual4x3','match'],'reason':'Business sources, numeric parameters, Resources XML and input structure unchanged after the earlier rule/smoke pass; final change replaces one title-frame image and tightens QA-only ending acceptance to require the original replay callback.'}
    write(ROOT/'fidelity/evidence/delivery.json',out);print(json.dumps(dict(status=out['status'],sourceFiles=len(inputs),images=82,qaReports=len(reports),release=release,backups=out['backups']),ensure_ascii=False))
if __name__=='__main__':main()
