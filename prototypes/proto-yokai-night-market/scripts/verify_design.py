"""Validate design coverage and service-layout witnesses, not game implementation."""
import csv
import hashlib
import json
import re
import sys
from pathlib import Path

P=Path(__file__).resolve().parents[1]
def read(path):return json.loads((P/path).read_text(encoding='utf-8'))
C=read('data/catalog.json');S=read('sources/crunch-structure.json')
checks=[]
def check(name,condition,detail):
    checks.append({'name':name,'status':'passed' if condition else 'failed','detail':detail})

def validate_shape(value,sch,path='$'):
    errors=[]
    typ='null' if value is None else 'boolean' if isinstance(value,bool) else 'integer' if isinstance(value,int) else 'number' if isinstance(value,float) else 'string' if isinstance(value,str) else 'array' if isinstance(value,list) else 'object'
    allowed=sch.get('type',[]);allowed=[allowed] if isinstance(allowed,str) else allowed
    if allowed and typ not in allowed and not (typ=='integer' and 'number' in allowed):return [path+':type']
    if 'const' in sch and value!=sch['const']:errors.append(path+':const')
    if isinstance(value,dict):
        props=sch.get('properties',{})
        errors += [path+':missing '+k for k in sch.get('required',[]) if k not in value]
        if sch.get('additionalProperties') is False:errors += [path+':unknown '+k for k in value if k not in props]
        for k,v in value.items():
            if k in props:errors+=validate_shape(v,props[k],path+'.'+k)
    elif isinstance(value,list):
        if len(value)<sch.get('minItems',0) or len(value)>sch.get('maxItems',len(value)):errors.append(path+':count')
        for i,x in enumerate(value):errors+=validate_shape(x,sch.get('items',{}),path+f'[{i}]')
    elif isinstance(value,(int,float)) and not isinstance(value,bool):
        if value<sch.get('minimum',value) or value>sch.get('maximum',value):errors.append(path+':range')
    elif isinstance(value,str):
        if len(value)<sch.get('minLength',0):errors.append(path+':length')
        if 'pattern' in sch and not re.search(sch['pattern'],value):errors.append(path+':pattern')
    return errors

shape=validate_shape(C,read('schemas/catalog.schema.json'))
check('catalog_structure_schema',not shape,shape[:20])
expected={'cards':95,'contracts':28,'facilities':12,'guests':5,'characters':46,'chapters':7,'tutorial':14,'assets':196,'audio':24,'aiProfiles':3,'screens':12,'requirements':18,'goldenCases':52}
for key,count in expected.items():
    rows=C[key];ids=[x['id'] for x in rows]
    check('count_and_unique_'+key,len(rows)==count and len(ids)==len(set(ids)),{'expected':count,'actual':len(rows),'unique':len(set(ids))})
assets={x['id']:x for x in C['assets']};cards={x['id']:x for x in C['cards']};roles={x['id'] for x in C['departments']};fac={x['id']:x for x in C['facilities']};guests={x['id']:x for x in C['guests']};chars={x['id']:x for x in C['characters']};nights={x['id']:x for x in C['contracts']};gold={x['id'] for x in C['goldenCases']}
effects={'work','block','steal_hand','return_workers','discard_hand','advance_all','finish_all','unlock_department','reduce_goal','repair','cancel_inspection','add_goal','reopen_task'}
bad=[]
for c in C['cards']:
    if c['art'] not in assets or c['effect'] not in effects or c['department'] not in roles|{None}:bad.append(c['id']+':ref')
    if c['kind']=='worker' and (c['turns'] not in [2,4] or c['character'] not in chars or c['target']!='self'):bad.append(c['id']+':worker')
    if c['kind']=='negative' and c['turns'] not in [1,2,3]:bad.append(c['id']+':duration')
    if c['ap']!=(0 if c['kind']=='counter' else 1) or c['coins']!=0:bad.append(c['id']+':cost')
    if c['kind']=='counter' and c['target']!='self':bad.append(c['id']+':reaction')
    if any(r not in cards or cards[r]['kind']!='negative' for r in c['repairIDs']):bad.append(c['id']+':repair')
    for field in ['name','description','quote']:
        if C['strings'].get(f'card.{c["id"]}.{field}')!=c[field]:bad.append(c['id']+':text')
    if c['kind']=='worker' and str(c['turns']) not in c['description']:bad.append(c['id']+':duration-text')
check('card_semantics_references_and_text',not bad,bad)
check('all_card_effects_covered',effects=={c['effect'] for c in C['cards']},sorted(effects))
check('worker_original_duration_distribution',sum(x['kind']=='worker' and x['turns']==2 for x in C['cards'])==21 and sum(x['kind']=='worker' and x['turns']==4 for x in C['cards'])==21,{'twoTurn':21,'fourTurn':21})
check('repair_groups_exact',all(cards[f'C{i:03d}']['repairIDs']==['C044','C045','C049','C050'] for i in [82,83,84]) and all(cards[f'C{i:03d}']['repairIDs']==['C047','C048'] for i in [85,86]),'雨棚4种/灯线2种，5张修补牌')

bad=[]
for n in C['contracts']:
    if set(n['goals'])!=roles or sum(n['goals'].values())!=12 or min(n['goals'].values())<1:bad.append(n['id']+':goals')
    if not 1<=len(n['guests'])<=2 or len(n['guests'])!=len(set(n['guests'])) or any(g not in guests for g in n['guests']):bad.append(n['id']+':guests')
    if n['art'] not in assets or n['roundLimit']!=C['rules']['maxRounds']:bad.append(n['id']+':config')
    old=next(x for x in S['projects'] if x['ID']==n['sourceID'])
    if n['goals']['ward']!=old['ignoredAnimation'] or any(n['goals'][d['id']]!=old['effectiveTasks'][d['source']] for d in C['departments'] if d['id']!='ward'):bad.append(n['id']+':numeric-decision')
    for field in ['name','subtitle','openingLine','successLine','failureLine']:
        if C['strings'].get(f'contract.{n["id"]}.{field}')!=n[field]:bad.append(n['id']+':text')
check('contract_goals_original_fix_and_references',not bad,bad)
check('chapter_content_partition',sorted(n for ch in C['chapters'] for n in ch['contracts'])==sorted(nights) and all(len(ch['contracts'])==4 for ch in C['chapters']),{'chapters':7,'contractsPerChapter':4})
tags={'food','light','warmth','music','water','ward','quiet','shade'}
bad=[]
for f in C['facilities']:
    if len(f['modes'])!=2 or f['sellCoins']!=f['buyCoins']//2 or f['stockPerPlayer']!=1 or f['art'] not in assets:bad.append(f['id'])
    for m in f['modes']:
        if not set(m['tags'])<=tags or not set(m['powerOffsets'])<={-1,1}:bad.append(f['id']+':mode')
for g in C['guests']:
    if not g['requiredTags'] or not set(g['requiredTags'])<=tags or not set(g['forbiddenTags'])<={'noise'} or g['art'] not in assets:bad.append(g['id'])
check('facilities_modes_prices_and_guest_tags',not bad,bad)
check('input_and_ui_minimum',C['uiLayout']['minimum']==[1280,720] and C['uiLayout']['minimumRenderedTextPixels']>=16 and 'Escape' in C['inputBindings']['Cancel'],'全键鼠与最小文字定义')

# Independent replayer for the bounded facility-only preparation witnesses.
def service(layout):
    powered=set();provided=set()
    for slot,(id,mode) in layout.items():
        m=next(m for m in fac[id]['modes'] if m['id']==mode)
        if m['noise']:provided.add('noise')
        powered |= {slot+off for off in m['powerOffsets'] if 0<=slot+off<5}
    for slot,(id,mode) in layout.items():
        m=next(m for m in fac[id]['modes'] if m['id']==mode)
        if not m['requiresPower'] or slot in powered:provided |= set(m['tags'])
    return provided

witnesses=read('data/opening-layouts.json');bad=[]
for w in witnesses:
    layout={x['slot']:(x['facility'],x['mode']) for x in C['rules']['initialFacilities']};stock=set(fac)-{x[0] for x in layout.values()};retired=set();coins=8;ap=0;turn=0
    try:
        for cmd in w['commands']:
            t=cmd['type']
            if t=='BeginNextOwnTurn':
                turn+=1;coins=min(20,coins+2);ap=3
                assert cmd['round']==turn and cmd['coins']==coins and cmd['ap']==ap
                continue
            assert ap>0;ap-=1;f=cmd['facility']
            current=next((s for s,x in layout.items() if x[0]==f),None)
            if t=='SellFacility':
                assert current is not None or f in stock
                if current is not None:layout.pop(current)
                else:stock.remove(f)
                retired.add(f);coins=min(20,coins+fac[f]['sellCoins'])
            elif t=='BuyFacility':
                assert f in stock and f not in retired and cmd['slot'] not in layout and coins>=fac[f]['buyCoins']
                coins-=fac[f]['buyCoins'];stock.remove(f);layout[cmd['slot']]=(f,fac[f]['modes'][0]['id'])
            elif t=='MoveFacility':
                assert current is not None and cmd['to'] not in layout and 0<=cmd['to']<5
                layout[cmd['to']]=layout.pop(current)
            elif t=='SetMode':
                assert current is not None and cmd['mode'] in [m['id'] for m in fac[f]['modes']] and cmd['mode']!=layout[current][1]
                layout[current]=(f,cmd['mode'])
            else:raise AssertionError('unknown command')
            assert cmd['afterCoins']==coins and cmd['afterAP']==ap
            assert len(stock)+len(retired)+len(layout)==12 and len({x[0] for x in layout.values()})==len(layout)
        desired={x['slot']:(x['facility'],x['mode']) for x in w['layout']}
        assert layout==desired and turn==w['ownTurnsRequired'] and coins==w['coinsRemaining'] and turn<=20
        effective=service(layout);assert effective==set(w['providedTags'])
        for gid in nights[w['contractID']]['guests']:
            assert set(guests[gid]['requiredTags'])<=effective and not set(guests[gid]['forbiddenTags'])&effective
    except (AssertionError,KeyError,StopIteration) as e:bad.append(w['contractID']+':'+str(e))
check('all_28_facility_paths_replayed_independently',not bad and {w['contractID'] for w in witnesses}==set(nights),{'witnesses':len(witnesses),'failures':bad,'scope':'设施设计路径，不是Unity/整局测试'})

mapping_specs=[('cards.json','sourceID',{x['ID'] for x in S['cards']}),('contracts.json','sourceID',{x['ID'] for x in S['projects']}),('resource-keys.json','sourceKey',set(S['resources'])),('objects.json','sourceObjectID',{x['id'] for x in S['objects']}),('code-roles.json','sourceClass',set(S['classes'])),('metadata-types.json','sourceType',set(S['metadataTypes']))]
for file,key,wanted in mapping_specs:
    rows=read('replacement/'+file);actual=[x[key] for x in rows]
    check('replacement_'+file,len(actual)==len(set(actual)) and set(actual)==wanted and all(x.get('policy') and x.get('reason',x.get('ruleDecision')) for x in rows),{'sourceCount':len(wanted),'mapped':len(rows)})
    bad=[]
    for row in rows:
        for a in row.get('targetAssetIDs',[]):
            if a not in assets:bad.append(a)
        if row.get('targetArtID') and row['targetArtID'] not in assets:bad.append(row['targetArtID'])
        for a in row.get('targetAudioIDs',[]):
            if a not in {x['id'] for x in C['audio']}:bad.append(a)
        for field in ['targetData','targetSpec']:
            if row.get(field) and not (P/row[field]).is_file():bad.append(row[field])
    check('replacement_targets_'+file,not bad,bad)
textmap=read('replacement/text-keys.json');actual=[(x['sourceTable'],x['sourceKey']) for x in textmap];wanted={(x['source'],x['id']) for x in S['textKeys']}
check('replacement_all420_text_keys',len(actual)==len(set(actual))==len(wanted) and set(actual)==wanted and all(x['targetTextKey'] in C['strings'] if x['policy']!='retired' else x['targetTextKey'] is None and x['reason'] for x in textmap),{'mapped':len(actual),'retired':sum(x['policy']=='retired' for x in textmap)})
check('source_nonempty_export_coverage',sum(x['exportCount'] for x in S['objects'])==345 and sum(x['sourceExportCount'] for x in read('replacement/objects.json'))==345,'全部345非空导出所属对象有处置')
state_map=read('replacement/state-flow.json')
check('all_18_source_flow_states_mapped',len(state_map)==18 and {x['sourceState'] for x in state_map}=={x['id'] for x in S['states']} and state_map==C['stateReplacement'] and all((P/x['targetSpec']).is_file() for x in state_map),'原18状态逐项改为新流程；自动修补与动画胜负明确替换')
systems=read('replacement/system-decisions.json')
check('all_system_replacement_decisions_defined',len(systems)==14 and systems==C['systemReplacement'] and all((P/x['targetSpec']).is_file() for x in systems),{'systems':len(systems)})

jobs=read('art/generation-jobs.json');bad=[]
for j in jobs:
    a=assets[j['assetID']];prompt=(P/j['promptFile']).read_text(encoding='utf-8');edit=(P/j['editPromptFile']).read_text(encoding='utf-8')
    if hashlib.sha256(prompt.encode()).hexdigest()!=j['promptSha256']:bad.append(j['assetID']+':hash')
    if any(x in prompt+edit for x in ['<角色>','{角色}','TODO','TBD','待填']):bad.append(j['assetID']+':placeholder')
    if len(prompt)<650 or a['subject'] not in prompt or 'Text:' not in prompt or 'Avoid:' not in prompt:bad.append(j['assetID']+':incomplete')
    if j['transparent_background']!=a['transparent'] or j['sourceSize']!=a['sourceSize'] or j['runtimePath']!=a['runtimePath']:bad.append(j['assetID']+':spec')
    if any(ref not in assets for ref in j['referenceIDs']):bad.append(j['assetID']+':reference')
check('all_150_expanded_generation_and_edit_prompts',not bad and len(jobs)==150 and {j['assetID'] for j in jobs}=={a['id'] for a in C['assets'] if a['method']=='image_gen'},bad)
visiting=set();done=set()
def visit(id):
    if id in visiting:raise ValueError(id)
    if id in done:return
    visiting.add(id);ref=assets[id].get('reference')
    if ref:visit(ref)
    visiting.remove(id);done.add(id)
try:
    for id in assets:visit(id)
    cycle=None
except (ValueError,KeyError) as e:cycle=str(e)
check('asset_reference_graph_no_cycle',cycle is None,cycle or {'assets':len(done)})
check('asset_paths_unique_and_production_status_truthful',all(len([a[k] for a in C['assets']])==len({a[k] for a in C['assets']}) for k in ['sourcePath','runtimePath']) and all(a['status']=='specified' for a in C['assets']),{'specs':len(assets),'generated':0,'imported':0})
vectors=[a for a in C['assets'] if a['method']=='native-svg']
check('all_41_native_vector_construction_specs',len(vectors)==41 and all(a.get('svgPaths') and a.get('svgViewBox')==[0,0,64,64] and a.get('svgStrokeWidth')==3 for a in vectors),{'vectorSpecs':len(vectors),'sourceFilesProduced':0})
salts={'P0.draw':0xA341316C,'P1.draw':0xC8013EA4,'P0.effects':0xAD90777D,'P1.effects':0x7E95761E,'cosmetic':0x9E3779B9}
check('rng_numeric_contract',C['rngConfig']['salts']==salts and C['rngConfig']['zeroFallback']==0x6D2B79F5 and C['rngConfig']['uniformDomainSize']==2**32-1,'四玩法流与一个独立装饰流；非零输出域')
audio={a['id'] for a in C['audio']}
check('audio_event_mapping_and_specs',all(id in audio for id in C['audioEventMap'].values()) and all(a['sampleRate']==48000 and a['peakDbfs']==-1 and a['status']=='specified' for a in C['audio']),{'events':len(C['audioEventMap']),'audio':len(audio),'production':'not-executed'})
check('public_information_ai',all(not p['canSeeHidden'] and p['actionBudget']==3 and p['boundedNodes']<=512 for p in C['aiProfiles']),[p['id'] for p in C['aiProfiles']])
check('save_identity_and_schema',C['meta']['productName']=='YokaiNightMarket' and C['meta']['companyName']=='RoguePrototypeLab' and re.fullmatch('[0-9a-f]{32}',C['meta']['productGuid']) is not None and read('schemas/save-envelope.schema.json')['properties']['schemaVersion']['const']==1,{'rules':C['meta']['rulesVersion'],'format':1})

bad=[]
for key,value in C['strings'].items():
    if not value.strip():bad.append(key+':empty')
    vars=sorted(re.findall(r'\{([A-Za-z]+)\}',value))
    if vars!=sorted(C['stringVariables'].get(key,[])):bad.append(key+':variables')
for t in C['tutorial']:
    if t['textKey'] not in C['strings']:bad.append(t['id'])
for s in C['screens']:
    if s['background'] not in assets or s['defaultFocus'] not in s['actionKeys'] or not set(s['actionKeys'])<=set(s['textKeys']) or any(k not in C['strings'] for k in s['textKeys']):bad.append(s['id'])
    if s['focusFallback'] and s['focusFallback'] not in s['actionKeys']:bad.append(s['id']+':focus-fallback')
check('strings_variables_screens_tutorial_closed',not bad,{'strings':len(C['strings']),'failures':bad})
check('all_golden_cases_not_claimed_executed',all(g['setup'] and g['commands'] and g['expected'] and g['status']=='specified-not-executed' for g in C['goldenCases']),{'specified':52,'gameTestsExecuted':0})
bad=[]
for r in C['requirements']:
    if any(not (P/path).is_file() for path in r['specPaths']) or any(id not in gold for id in r['acceptanceCaseIDs']) or r['implementationEvidence'] is not None:bad.append(r['id'])
check('all_requirements_to_specs_and_acceptance',not bad,bad)

csv_bad=[]
for k in list(expected)+['departments','strings','productionPlan']:
    path=P/'data/csv'/f'{k}.csv'
    with path.open(encoding='utf-8',newline='') as f:rows=list(csv.DictReader(f))
    if len(rows)!=len(C[k]):csv_bad.append(k)
    if k!='strings':
        for src,row in zip(C[k],rows):
            for field,v in src.items():
                target=json.dumps(v,ensure_ascii=False,separators=(',',':')) if isinstance(v,(dict,list)) else '' if v is None else str(v)
                if row.get(field)!=target:csv_bad.append(k+':'+field)
    else:
        if dict((r['key'],r['zh-CN']) for r in rows)!=C['strings']:csv_bad.append('strings:values')
check('all_csv_authority_values_match',not csv_bad,csv_bad[:20])

links_bad=[]
for p in P.rglob('*.md'):
    text=p.read_text(encoding='utf-8')
    for link in re.findall(r'\]\(([^)]+)\)',text):
        target=link.strip('<>').split('#')[0]
        if not target or re.match(r'^(https?://|app://)',target):continue
        if re.match(r'^[A-Za-z]:/',target):path=Path(target)
        else:path=p.parent/target
        if not path.exists() and path.resolve()!=(P/'evidence/design-verification.json').resolve():
            links_bad.append({'file':p.relative_to(P).as_posix(),'target':target})
check('all_local_document_links_exist',not links_bad,links_bad)

try:
    from fontTools.ttLib import TTFont
    prep=read('evidence/preparation.json');font_path=P.parents[1]/prep['fontPath']
    font=TTFont(str(font_path));cmap=font.getBestCmap();used={ord(ch) for s in C['strings'].values() for ch in s if not ch.isspace()};missing=sorted(used-set(cmap))
    font_hash_matches=hashlib.sha256(font_path.read_bytes()).hexdigest()==prep['fontSha256']
    check('font_all_current_text_glyphs',not missing and font_hash_matches,{'uniqueCodepoints':len(used),'missing':[f'U+{v:04X}' for v in missing],'fontHashMatches':font_hash_matches})
except (ImportError,FileNotFoundError) as e:
    checks.append({'name':'font_all_current_text_glyphs','status':'not-executed','detail':str(e)})

coverage=read('evidence/coverage.json');hash_actual=hashlib.sha256((P/'data/catalog.json').read_bytes()).hexdigest()
check('derived_outputs_current_catalog_hash',coverage['catalogSha256']==hash_actual,hash_actual)
files=[]
for p in sorted(P.rglob('*')):
    if not p.is_file() or '__pycache__' in p.parts or p.name in ['design-verification.json','design-files.json']:continue
    files.append({'path':p.relative_to(P).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
failed=sum(x['status']=='failed' for x in checks);pending=sum(x['status']=='not-executed' for x in checks)
report={'date':'2026-10-12','scope':'design-preparation-only','contentVersion':C['meta']['contentVersion'],'rulesVersion':C['meta']['rulesVersion'],'catalogSha256':hash_actual,'result':'passed' if not failed else 'failed','checksPassed':sum(x['status']=='passed' for x in checks),'checksFailed':failed,'checksNotExecuted':pending,'coverage':coverage,'checks':checks,'excludedClaims':['Unity规则实现/编译/测试','素材生成/导入/Player目检','音频生产/听感','实际键鼠/试玩/性能','构建/Steam发行'],'designCoveragePercent':100 if not failed and not pending else None}
for name,obj in [('design-verification.json',report),('design-files.json',files)]:
    with (P/'evidence'/name).open('w',encoding='utf-8',newline='\n') as f:f.write(json.dumps(obj,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'result':report['result'],'passed':report['checksPassed'],'failed':failed,'notExecuted':pending,'coveragePercent':report['designCoveragePercent'],'failures':[x for x in checks if x['status']=='failed']},ensure_ascii=False))
sys.exit(1 if failed else 0)
