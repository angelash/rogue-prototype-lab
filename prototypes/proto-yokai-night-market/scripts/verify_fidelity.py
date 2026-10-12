"""Verify local restoration and publish only counts, original replacement data and hashes.
No recovered code or source media is included in the output.
"""
from pathlib import Path
import hashlib,json,re,xml.etree.ElementTree as ET
from PIL import Image
from fontTools.ttLib import TTFont
ROOT=Path(__file__).resolve().parents[1]
REPO=ROOT.parents[1]
PROJECT=ROOT/'local/UnityProject'
SOURCE=REPO/'prototypes/re-crunch-time'
def read(p):return json.loads(p.read_text('utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def normalized(p):return p.read_text('utf-8-sig').replace('\r\n','\n')
def verify():
    checks=[]
    def check(name,ok,**facts):checks.append(dict(name=name,passed=bool(ok),**facts))
    theme=read(ROOT/'fidelity/data/theme.json');cat=read(SOURCE/'local/content-catalog.json')
    adaptations=[]
    for p in sorted((SOURCE/'local/decompiled').glob('*.cs')):
        expected=normalized(p)
        for a,b in [('base.collider2D','GetComponent<Collider2D>()'),('Screen.showCursor','Cursor.visible'),('.collider2D','.GetComponent<Collider2D>()'),('component.castShadows = false;','component.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;'),('Application.isLoadingLevel','LegacyEngine.IsLoadingLevel'),('Application.LoadLevel(Application.loadedLevel)','UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex)')]:expected=expected.replace(a,b)
        if p.name=='ProjectCard.cs':expected,n=re.subn(r'm_asCompanyName = new string\[8\] \{[^;]+;','m_asCompanyName = new string[8] { '+', '.join(json.dumps(x,ensure_ascii=False) for x in theme['companies'])+' };',expected,count=1);assert n==1
        if p.name=='Option.cs':expected=expected.replace('m_oMain.GetComponent<TextMesh>().text = hLanguage.sGetText(m_sTextID);','m_oMain.GetComponent<TextMesh>().text = m_sTextID == "Sound" ? (hLanguage.iLanguage == 0 ? "声音" : "Sound") : hLanguage.sGetText(m_sTextID);')
        actual=PROJECT/'Assets/Recovery/Scripts'/p.name
        ok=normalized(actual)==expected
        adaptations.append(dict(file=p.name,referenceSHA256=sha(p),runtimeSHA256=sha(actual),onlyDeclaredAdaptations=ok))
    check('All business scripts differ only by declared Unity API bridges, eight company names and an audio-option display alias',len(adaptations)==30 and all(x['onlyDeclaredAdaptations'] for x in adaptations),scripts=len(adaptations))
    fields={'id':'ID','type':'Type','turns':'Turns','target':'Target','function':'Function','amount':'Num','repair':'Repair','picture':'Picture'}
    check('All 95 card rule records preserve source numeric parameters and references',len(theme['cards'])==95 and all(all(c[a]==s[b] for a,b in fields.items()) for c,s in zip(theme['cards'],cat['cards'])))
    check('All 28 effective project goals preserve source Testing=2 behavior',len(theme['projects'])==28 and all(t['id']==s['ID'] and t['effectiveGoals']==s['effectiveTasks'] for t,s in zip(theme['projects'],cat['projects'])))
    check('All source text keys have two replacement language values',set(theme['strings'])=={t['id'] for t in cat['translations']} and len(theme['strings'])==420 and all(set(v)=={'zh','en'} for v in theme['strings'].values()),keys=len(theme['strings']))
    for name in ['CardsPlay','CardsProject']:
        xml=ET.parse(PROJECT/f'Assets/Recovery/Media/night-{name}.xml').getroot();items=cat['cards' if name=='CardsPlay' else 'projects'];valid=len(xml)==len(items)
        for row,item in zip(xml,items):
            actual={e.tag:e.text or '' for e in row};expected=dict(item['raw'])
            if 'Platform' in expected:expected['Platform']=theme['districts'][expected['Platform']]
            valid &= actual==expected
        check(name+' production XML preserves all source rule fields',valid)
    for name in ['Texts','TextsCards','TextsProjects']:
        xml=ET.parse(PROJECT/f'Assets/Recovery/Media/night-{name}.xml').getroot();valid=True
        for row in xml:
            k=row.findtext('ID') or '';valid &= (row.findtext('ENG') or '')==theme['strings'][k]['zh'] and (row.findtext('SPA') or '')==theme['strings'][k]['en']
        check(name+' production XML matches replacement strings',valid,rows=len(xml))
    check('All five logical Resources XML files match their verified production inputs',all((PROJECT/f'Assets/Resources/{name}.xml').exists() and sha(PROJECT/f'Assets/Resources/{name}.xml')==sha(PROJECT/f'Assets/Recovery/Media/night-{name}.xml') for name in ['Texts','TextsCards','TextsProjects','CardsPlay','CardsProject']))
    manifest=read(PROJECT/'Assets/Editor/RecoverySource/manifest.json');objects={o['id']:o for o in manifest['objects']}
    check('Initial TextMesh values preserve dictionary keys, with six declared static tutorial-title overrides',len(theme['staticTextOverrides'])==6 and all(o['tree']['m_Text']==theme['staticTextOverrides'].get(o['id'],read(SOURCE/'local/typetrees'/o['container']/(str(o['pathID'])+'_TextMesh.json'))['m_Text']) for o in objects.values() if o['type']=='TextMesh'))
    font=next(o for o in objects.values() if o['type']=='Font');fontfile=PROJECT/font['assetPath'];f=TTFont(fontfile);chars=set().union(*(set(t.cmap) for t in f['cmap'].tables));wanted={ord(c) for v in theme['strings'].values() for text in v.values() for c in text if not c.isspace()};wanted.update(ord(c) for t in theme['staticTextOverrides'].values() for c in t);wanted.update(ord(c) for v in theme['optionDisplayAlias'].values() for t in v.values() for c in t);missing=sorted(wanted-chars)
    check('Chinese font covers every replacement text character',not missing,missingCodePoints=missing,fontSHA256=sha(fontfile))
    check('All thirteen source font references bind to the same licensed CJK font bytes',sum(o['type']=='Font' for o in objects.values())==13 and all(sha(PROJECT/o['assetPath'])==sha(fontfile) for o in objects.values() if o['type']=='Font'))
    ledger=read(ROOT/'fidelity/art/generation-register.json');registrations={r['name']:r for r in ledger['images']};images=[]
    jobs=read(ROOT/'fidelity/art/jobs.json')['jobs']
    for j in jobs:
        raw=ROOT/j['raw'];final=ROOT/j['final'];obj=objects[j['id']];working=PROJECT/obj['assetPath'];record=registrations.get(j['name'],{});ok=raw.exists() and final.exists()
        item=dict(id=j['id'],name=j['name'],targetSize=[j['width'],j['height']],kind=j['kind'],passed=False)
        if ok:
            with Image.open(final) as im:validSize=list(im.size)==item['targetSize'];item['alphaRange']=list(im.getchannel('A').getextrema()) if 'A' in im.getbands() else [255,255]
            original=SOURCE/obj['exportFiles'][0]['path']
            ok=validSize and sha(final)==sha(working) and sha(raw)==record.get('rawSHA256') and sha(ROOT/j['prompt'])==record.get('promptSHA256') and sha(final)!=sha(original)
            item.update(rawSHA256=sha(raw),runtimeSHA256=sha(final),originalReferencePNG_SHA256=sha(original),passed=ok)
        images.append(item)
    check('All 82 independently generated images have source dimensions, provenance and runtime copies',len(images)==82 and all(i['passed'] for i in images),generated=len(registrations),exported=sum(i['passed'] for i in images))
    ownReferences=[p for r in ledger['images'] for p in r['references']]
    check('No original art was supplied to image generation',all(p.startswith('fidelity/art/revisions/') and (ROOT/p).exists() for p in ownReferences),ownEditReferences=ownReferences)
    structure=read(ROOT/'local/structure-audit.json');prefabs=read(ROOT/'local/prefab-report.json')
    check('Serialized structure audit passed',structure['passed'] and structure['transforms']==462 and structure['components']==1063 and structure['spriteVertices']==970,**{k:v for k,v in structure.items() if k not in ('passed','failures')})
    check('All eighteen scene roots exist as editable prefabs',prefabs['rootPrefabs']==18 and prefabs['nodesCovered']==462)
    settings=normalized(PROJECT/'ProjectSettings/ProjectSettings.asset')
    check('Runtime product and save identity are independent', 'companyName: RoguePrototypeLab' in settings and 'productGUID: 2a44e75ab078cae489d98d574a9221ca' in settings)
    quality=normalized(PROJECT/'ProjectSettings/QualitySettings.asset');tiers=next(o['tree'] for o in objects.values() if o['type']=='QualitySettings')['m_QualitySettings'];blocks=re.findall(r'  - serializedVersion: 3\n.*?(?=  - serializedVersion: 3\n|  m_TextureMipmapLimitGroupNames:)',quality,re.S);mapping={'blendWeights':'skinWeights','textureQuality':'globalTextureMipmapLimit'};valid=len(blocks)==6
    for block,tier in zip(blocks,tiers):
        for key,value in tier.items():
            key=mapping.get(key,key);m=re.search(r'(?m)^    '+re.escape(key)+r': (.*)$',block)
            if m is None:valid=False
            else:valid &= m.group(1)==value if isinstance(value,str) else abs(float(m.group(1))-float(value))<.00001
    check('All six source quality tiers and the Windows default are preserved',valid and 'm_CurrentQuality: 0' in quality)
    own=[]
    for folder,destination in [('unity','Assets/Editor'),('runtime','Assets/Recovery/Validation')]:
        for source in (ROOT/'scripts'/folder).glob('*.cs'):
            actual=PROJECT/destination/source.name;own.append(dict(file=source.relative_to(ROOT).as_posix(),sha256LF=hashlib.sha256(normalized(source).encode()).hexdigest(),sameRuntimeSource=actual.exists() and normalized(actual)==normalized(source)))
    check('Original restoration and QA tools match private Unity sources',all(i['sameRuntimeSource'] for i in own))
    out=dict(version=2,passed=all(c['passed'] for c in checks),scope='Static restoration, data, font and full image import coverage; not a claim of pixel or native experience equivalence',checks=checks,codeAdaptations=adaptations,originalTools=own,images=images)
    write(ROOT/'fidelity/evidence/fidelity-verification.json',out);print(json.dumps(dict(passed=out['passed'],checks=len(checks),failures=[c['name'] for c in checks if not c['passed']]),ensure_ascii=False))
    return out['passed']
if __name__=='__main__':raise SystemExit(0 if verify() else 2)
