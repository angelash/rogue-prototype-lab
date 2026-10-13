"""Publish original-data delivery metadata after reading actual local evidence."""
from pathlib import Path
from datetime import datetime,timezone
import argparse,hashlib,json,re,shutil
ROOT=Path(__file__).resolve().parents[1]
LOCAL=ROOT/'local';PROJECT=LOCAL/'UnityProject';SOURCE=ROOT.parent/'re-crunch-time'
def read(p):return json.loads(p.read_text('utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--flow-report');parser.add_argument('--native-report');parser.add_argument('--visual-suffix',default='');parser.add_argument('--prompt-suffix',default='');parser.add_argument('--band-repair',action='store_true');args=parser.parse_args()
    verification=read(ROOT/'fidelity/evidence/fidelity-verification.json');assert verification['passed']
    images={r['id']:r for r in verification['images']};jobs=read(ROOT/'fidelity/art/jobs.json');spec=read(ROOT/'fidelity/image-specs.json');book=read(ROOT/'fidelity/art/generation-register.json')
    for j in jobs['jobs']:j['status']='imported_verified';j['runtimeSHA256']=images[j['id']]['runtimeSHA256']
    for s in spec['textures']:
        j=next(j for j in jobs['jobs'] if j['id']==s['id']);s.update(replacement=j['final'],raw=j['raw'],prompt=j['prompt'],kind=j['kind'],status='imported_verified',rawSHA256=images[s['id']]['rawSHA256'],runtimeSHA256=images[s['id']]['runtimeSHA256'])
    for r in book['images']:r['review']='Generated raw retained as rejected history; runtime uses a uniform procedural Continue plate with original alpha, verified in the prompt survey.' if r['name']=='SubTitle' and args.prompt_suffix else 'Raw image inspected; exact runtime dimensions and copy hashes verified; actual Player family samples checked separately in delivery evidence.'
    write(ROOT/'fidelity/art/jobs.json',jobs);write(ROOT/'fidelity/image-specs.json',spec);write(ROOT/'fidelity/art/generation-register.json',book)
    reports={}
    for key,relative in [('rules','QAResults/rules/rules.json'),('smoke16x9','QAResults/smoke-16x9/report.json'),('smoke4x3','QAResults/smoke-4x3/report.json'),('visual16x9','QAResults/visual-complete-16x9-v2.2/report.json'),('visual4x3','QAResults/visual-complete-4x3-v2.2/report.json'),('match','QAResults/match-v2.2/report.json')]:
        if key.startswith('visual') and args.visual_suffix:relative=relative.replace('-v2.2/',args.visual_suffix+'/')
        p=LOCAL/relative;r=read(p);assert r['passed'],key;reports[key]=dict(report=r,localPath='local/'+relative,sha256=sha(p));write(ROOT/'fidelity/evidence'/(key+'.json'),r)
    assert len(reports['rules']['report']['rules'])==40 and all(r['passed'] for r in reports['rules']['report']['rules'])
    for label,size in [('16x9',(1920,1080)),('4x3',(1440,1080))]:
        for family in ['smoke','visual']:
            report=reports[family+label]['report'];assert (report['width'],report['height'])==size and report['cards']==95 and report['projects']==28
        report=reports['visual'+label]['report'];assert report['tutorialPages']==2 and len(report['captures'])==128 and report['qaFrameRate']==60
        if args.visual_suffix:assert report['language']==0 and report['tutorialTextBoundsChecks']>0 and report['verifiedCardLanguages']==95 and report['verifiedProjectLanguages']==28 and not report['errors']
    match=reports['match']['report'];assert match['state']==50 and match['replayReady'] and len(match['winnerDone'])==5 and all(done>=goal for done,goal in zip(match['winnerDone'],match['winnerGoals']))
    if args.flow_report:
        p=ROOT/args.flow_report;r=read(p);assert r['passed'] and len(r['checks'])==15 and r['reloads']==1 and not r['errors'];reports['flow']=dict(report=r,localPath=args.flow_report,sha256=sha(p))
    metadata=read(LOCAL/'release-code-metadata.json');qaTypes=[t['name'] for t in metadata['types'] if any(k in t['name'] for k in ['NightSmokeProbe','NightRuleChecks','NightVisualProbe','NightFlowProbe','NightPromptProbe'])];assert not qaTypes
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
    out['native']=dict(passed=False,status='native acceptance requires an explicit actual report; no native coverage inferred from automated QA')
    if args.native_report:
        p=ROOT/args.native_report;r=read(p);assert r['cases'] and all(isinstance(c['passed'],bool) for c in r['cases']);out['native']=dict(passed=r['completeAcceptance'],partialChecks=sum(c['passed'] for c in r['cases']),status='partial native Release coverage completed; remaining cases listed in the actual report',localPath=args.native_report,sha256=sha(p),report=r)
    if args.flow_report:
        out['qaRevisionScope']['flowAddition']=['flow'];out['qaRevisionScope']['flowReason']='v2.3 adds a QA-only integration driver and version-controls full production/test inputs. All 30 production scripts, 95/28 numeric records, 420 text keys and 82 image bytes remain verified. Existing v2.2 visual/full-match reports retain their original runtime scope.'
    out['versionControl']=dict(fullSourceBranch='codex/yokai-night-market-source',assetCommit='e0b1c7ccfbe862d743191f8efc33d63a5e3acd1a',scope='Unity Assets/.meta, Packages, ProjectSettings, all art sources/exports and tests/reference fixtures; caches and outputs excluded',remote='full source history stays local; public origin retains original material only')
    if args.visual_suffix:
        out['qaRevisionScope']['copyPolish']=['visual16x9','visual4x3','visualEnglish16x9','visualEnglish4x3'];out['qaRevisionScope']['copyPolishReason']='v2.4 changes bilingual copy, two generated frames, eight declared TextMesh colors and eight label material colors. New visual reports verify each template language and text width in the current rendered packaging. Earlier rules, smoke, flow, native and full-match reports retain their original revision scope. Production business code and numeric rules remain unchanged.'
        out['qaRevisionScope']['finalHeaderArt']=['match'];out['qaRevisionScope']['reason']='The full-match report belongs to v2.2; retained as behavior history, it does not attest the v2.4 rendered copy.'
        for label in ('16x9','4x3'):
            p=LOCAL/f'QAResults/visual-complete-{label}{args.visual_suffix}-en/report.json';r=read(p)
            assert r['passed'] and not r['errors'] and len(r['captures'])==128 and r['tutorialTextBoundsChecks']>0 and r['language']==1 and r['verifiedCardLanguages']==95 and r['verifiedProjectLanguages']==28
            reports['visualEnglish'+label]=dict(report=r,localPath=p.relative_to(ROOT).as_posix(),sha256=sha(p))
            write(ROOT/'fidelity/evidence'/('visualEnglish'+label+'.json'),r)
    if args.prompt_suffix:
        for label,size in [('16x9',(1920,1080)),('4x3',(1440,1080))]:
            for lang,ending in [(0,''),(1,'-en')]:
                p=LOCAL/f'QAResults/prompts-{label}{args.prompt_suffix}{ending}/report.json';r=read(p)
                assert r['passed'] and not r['errors'] and len(r['captures'])==33 and r['boundsChecks']>=4 and r['projectHeaderChecks']==28 and r['language']==lang and (r['width'],r['height'])==size
                key=f'prompts{label}'+('English' if lang else 'Chinese');reports[key]=dict(report=r,localPath=p.relative_to(ROOT).as_posix(),sha256=sha(p))
                write(ROOT/'fidelity/evidence'/(key+'.json'),r)
        out['images'].update(runtimeGeneratedArt=81,proceduralPromptPlates=1,historicalGeneratedSources=82)
        out['qaRevisionScope']['promptRepair']={'reports':[k for k in reports if k.startswith('prompts')],'reason':'v2.5 corrects SubTitle from a baked logo to a text-free Continue background, clarifies three UI labels, shortens 28 English project subtitles and declares one project subtitle TextMesh characterSize=0.85 for the collapsed header gap. Node transforms, references, hit areas and business logic stay original. New fixtures cover the production table, four prompt states and all project header gaps in both aspect ratios and languages. The 512 card/tutorial captures are v2.4 history; the new prompt fixture is not native mediator evidence.'}
        nativePrompt=read(ROOT/'fidelity/evidence/prompt-native.json')
        assert all(c['passed'] for c in nativePrompt['cases'])
        for capture in nativePrompt['captures']:assert sha(ROOT/capture['path'])==capture['sha256']
        testedCurrentBuild=nativePrompt['releasePlayerSHA256']==sha(LOCAL/'Builds/Release/YokaiNightMarket.exe') and nativePrompt['releaseAssemblySHA256']==out['releaseAssemblySHA256']
        out['promptNative']=dict(report=nativePrompt,testedCurrentBuild=testedCurrentBuild,status='partial native prompt checks on the recorded build' if testedCurrentBuild else 'Recorded v2.5-r4 native prompt history; not a native recheck of the current release')
    if args.band_repair:
        assert args.visual_suffix and args.prompt_suffix
        current=['visual16x9','visual4x3','visualEnglish16x9','visualEnglish4x3']
        for key in current:
            r=reports[key]['report'];assert r['negativeCardsChecked']==26 and r['negativeBandInkChecks']==52
        out['qaRevisionScope']['copyPolishReason']='The v2.4 copy remains unchanged. Listed visual reports now come from the current v2.6 band repair; older copy-polish captures remain in local archives.'
        out['qaRevisionScope']['promptRepair']['reason']='The v2.5 prompt repair reports remain their recorded history; v2.6 changes only one negative-card band material and the QA-only pixel checks. No prompt texture, node, text or behavior changes in this revision.'
        out['qaRevisionScope']['reason']='Rules, smoke, flow, match and partial native reports retain their recorded earlier revision scope; only the four listed visual surveys attest the current v2.6 packaging.'
        out['qaRevisionScope']['bandRepair']=dict(reports=current,material='00_NEG_Detail',rgba=[.72,.24,.18,1],affectedCardTypes=[6,7],affectedCards=26,reason='Original pure-black classification bands are intentional source design; change their packaging tint to terracotta so the title and separators remain identifiable on the night-market frame. Each affected card renders both bands separately on black and must have at least 100 colored pixels. All 82 PNG bytes, text, geometry, references, business code and numeric rules remain unchanged.')
    write(ROOT/'fidelity/evidence/delivery.json',out);print(json.dumps(dict(status=out['status'],sourceFiles=len(inputs),images=82,qaReports=len(reports),release=release,backups=out['backups']),ensure_ascii=False))
if __name__=='__main__':main()
