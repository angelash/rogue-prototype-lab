"""Derive public design deliverables from the sole authoring catalog; standard library only."""
import csv
import hashlib
import io
import itertools
import json
import re
from pathlib import Path

P = Path(__file__).resolve().parents[1]
C = json.loads((P/'data/catalog.json').read_text(encoding='utf-8'))
S = json.loads((P/'sources/crunch-structure.json').read_text(encoding='utf-8'))

def put(path, value):
    p = P/path
    p.parent.mkdir(parents=True, exist_ok=True)
    with p.open('w', encoding='utf-8', newline='\n') as f:
        f.write(value)

def js(path, obj):
    put(path, json.dumps(obj, ensure_ascii=False, indent=2)+'\n')

def canon(obj):
    return json.dumps(obj, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode('utf-8')

def norm(s):
    return re.sub('[^a-z0-9]', '', s.lower())

def csv_rows(path, rows):
    columns = list(dict.fromkeys(k for row in rows for k in row))
    out=io.StringIO(newline=''); w=csv.DictWriter(out,fieldnames=columns,lineterminator='\n');w.writeheader()
    for row in rows:
        w.writerow({k:json.dumps(v,ensure_ascii=False,separators=(',',':')) if isinstance(v,(dict,list)) else v for k,v in row.items()})
    put(path,out.getvalue())

for k in ['cards','contracts','facilities','guests','characters','chapters','tutorial','assets','audio','aiProfiles','screens','requirements','goldenCases','productionPlan','departments']:
    csv_rows('data/csv/'+k+'.csv',C[k])
csv_rows('data/csv/strings.csv',[{'key':k,'zh-CN':v} for k,v in C['strings'].items()])
js('replacement/state-flow.json',C['stateReplacement'])
js('replacement/system-decisions.json',C['systemReplacement'])

# Each image has an expanded prompt, not a placeholder template.
jobs=[]
for a in C['assets']:
    if a['method']!='image_gen':continue
    ref=a.get('reference')
    identity=a['id'] in ['NPC.guide','EVT.087','EVT.088']
    prompt='\n'.join([
        'Use case: stylized-concept' if a['kind'] not in ['contract','background','marketing'] else 'Use case: illustration-story',
        'Asset type: 《妖怪夜市开张》PC二维游戏的'+a['kind']+'，资产编号 '+a['id'],
        'Primary request: 制作一张可独立导入的原创手绘位图，仅包含下述规定内容。',
        ('Input images: 图1为已验收 '+ref+' 的身份编辑目标，保持脸、身体材质、服饰与工具；只改为下述姿态。' if identity else 'Input images: 图1为已验收 '+ref+' 的风格参考，只参考笔触、色板、光照与比例。') if ref else 'Input images: 无参考图；本图将作为项目风格母版。',
        'Scene/backdrop: 真正透明的alpha背景，不画地面、阴影底板或棋盘格。' if a['transparent'] else 'Scene/backdrop: 不透明的深靛东方旧巷夜景，背景服从下述主体构图，低细节区域干净。',
        'Subject: '+a['subject'],
        'Style/medium: 原创二维手绘，粗纸画感与清楚木刻轮廓，圆润成年妖怪比例，克制细节，生活手艺的温暖感；不是摄影或三维渲染。',
        'Composition/framing: '+a['composition'],
        'Lighting/mood: 左上蜜黄纸灯主光，右后弱冷青补光；温和、夜间、可读，形体内部不过暗。',
        'Color palette: 深靛#17243B、纸米白#F1E4C5、蜜黄#E9BC58、赤陶#D77854、灰绿#72AF88、灰紫#9A8BC2、冷青#70ADBF；优先遵守主体已指定衣色。',
        'Materials/textures: 纸有折纹、木有温和木纹、布有宽补丁、铁为哑光旧漆；只表现主体需要的材料，不增加无关装饰。',
        'Text: 无文字，无字母，无数字；标题、卡牌名、功能图标和按钮由游戏原生UI叠放。',
        'Constraints: 目标交付尺寸 '+str(a['sourceSize'][0])+'×'+str(a['sourceSize'][1])+' 像素；尺寸由产后检查确认；四边保持安全区，工具与手部完整正确，剪影在128px仍可辨；不烘焙状态光、UI边框或命中区域。',
        'Avoid: 现有IP或原游戏角色、写实恐怖、血腥、性感衣着、儿童化比例、现代办公物、霓虹赛博风、摄影皮肤、品牌、水印、额外角色、额外工具、重复肢体、畸形手、裁断工具、伪透明格、强烈光晕。',
    ])+'\n'
    edit='\n'.join([
        'Use case: identity-preserve' if a['kind'] in ['worker','npc','guest'] else 'Use case: precise-object-edit',
        'Asset type: 《妖怪夜市开张》'+a['id']+' 单件修订。',
        'Input images: 图1是本资产的已生成版本，作为编辑目标；图2如提供则是REF.style风格参考。',
        'Primary request: 修正本资产与下述冻结规格不符的部分；每次执行只修一个已观察到的缺陷。',
        'Frozen subject: '+a['subject'],
        'Composition/framing: '+a['composition'],
        'Constraints: 锁定同一身份、主光左上、原有配色、材质、比例、服饰和工具；不改变功能部件数量；保持'+('真实alpha透明背景' if a['transparent'] else '不透明夜景与标题安全区')+'；不添加任何文字、数字、边框、品牌或水印。',
        'Avoid: 重设计角色、替换工具、增加肢体、伪透明、画入UI状态。修订后重新检查原图与128px剪影。',
    ])+'\n'
    put(a['promptPath'],prompt);put('art/prompts/'+a['id']+'.edit.txt',edit)
    jobs.append({'assetID':a['id'],'referenceIDs':[ref] if ref else [],'mode':'edit-identity' if identity else 'generate-reference' if ref else 'generate','transparent_background':a['transparent'],'promptFile':a['promptPath'],'editPromptFile':'art/prompts/'+a['id']+'.edit.txt','promptSha256':hashlib.sha256(prompt.encode()).hexdigest(),'sourceSize':a['sourceSize'],'runtimeSize':a['runtimeSize'],'runtimePath':a['runtimePath'],'status':'specified-not-generated'})
js('art/generation-jobs.json',jobs)
put('art/prompt-index.md','# 完整生图提示词索引\n\n150件位图各有一条完整生成提示词和一条单件修订提示词。按referenceIDs拓扑顺序制作，先REF.style及代表样件；未接受参考不能作为后续输入。`generation-jobs.json`记录调用模式、透明度、路径、尺寸与提示词hash。\n\n'+'\n'.join(f'- [{j["assetID"]}](prompts/{j["assetID"]}.txt) · [修订](prompts/{j["assetID"]}.edit.txt) · {j["mode"]} · 参考 {", ".join(j["referenceIDs"]) or "无"}' for j in jobs)+'\n')

# Source-to-original-content mappings; source prose/media are never copied.
by_source={x['sourceID']:x for x in C['cards']}
js('replacement/cards.json',[{'sourceID':x['ID'],'sourceType':x['Type'],'sourceTurns':x['Turns'],'sourcePicture':x['Picture'],'targetCardID':by_source[x['ID']]['id'],'targetArtID':by_source[x['ID']]['art'],'targetRole':by_source[x['ID']]['department'],'policy':'original-redesign','ruleDecision':'保留2/4工作与1/2/3停工结构；效果按本版卡表；AP/实例/反应/洗牌/胜负使用新规则'} for x in S['cards']])
js('replacement/contracts.json',[{'sourceID':n['sourceID'],'sourceEffectiveGoals':n['sourceEffectiveGoals'],'sourceAnimation':n['sourceAnimation'],'targetContractID':n['id'],'newGoals':n['goals'],'newGuestIDs':n['guests'],'targetArtID':n['art'],'policy':'original-redesign','reason':n['numericDecision']} for n in C['contracts']])
old_ui={
'Pause':'ui.settings','Resume':'ui.resume','NextTurn':'ui.endTurn','AutoPause':'ui.autopause','FastMode':'ui.fast','Tutorial':'ui.tutorial','PlayAgain':'ui.playAgain','DisableAutoPause':'ui.autopause','Continue':'ui.next','ClickToUse':'ui.use','Recruiting':'ui.recruit','SelectingProjects':'ui.chooseContract','YourTurn':'ui.player','AITurn':'ui.aiThinking','YouWin':'ui.win','YouLose':'ui.lose','AI':'ui.ai','PLAYER':'ui.player','Player':'ui.player','YOURPROJECT':'ui.stallName','EDIT':'ui.edit','Design':'role.stock','Programming':'role.cook','Art':'role.dress','Testing':'role.ward','Sound':'role.call','SOUND DESIGNER':'role.call','SOUND DESIGNERGirl':'role.call','PROGRAMMER':'role.cook','PROGRAMMERGirl':'role.cook','DESIGNER':'role.stock','DESIGNERGirl':'role.stock','ARTIST':'role.dress','TESTER':'role.ward','TESTERGirl':'role.ward','OUCH':'ui.affected','INSTA-OUCH':'reason.4','COOL':'tuto.finish','SPECIAL':'ui.counter','DIRECTION':'ui.inspect','HEADQUARTERS':'ui.inspect','PROJECT':'ui.contract','Week':'ui.week','MoneySymbol':'ui.coin','NewRecord':'ui.record','SCORECARDTITLE':'ui.rank','TUTO_Welcome':'tuto.welcome','TUTO_Obj':'tuto.goal','TUTO_Para':'tuto.worker','TUTO_Turno':'tuto.end','TUTO_Cada':'tuto.work','TUTO_Termina':'tuto.goal','TUTO2_Cont':'tuto.work','TUTO2_Dev':'tuto.worker','TUTO2_Neg':'tuto.block','TUTO2_Pos':'tuto.finish','TUTO2_Esp':'tuto.repair','TUTO2_Juega':'tuto.lock','TUTO2_Buena':'tuto.finish','AFFECT':'ui.affected','TUTO_Dept_Anna':'tuto.guests','TUTO_Dept_Abel':'tuto.worker','TUTO_Dept_Rosa':'tuto.facility','TUTO_Dept_Mikis':'tuto.power','TUTO_Dept_English':'tuto.counter','TUTO_Dept_Marc':'tuto.repair','ExitIn':'ui.cleanExit'}
text_map=[]
for k in S['textKeys']:
    old=k['id'];target=None;policy='original-rewrite';reason='原界面语义改为夜市操作'
    if k['source']=='TextsCards':
        m=re.fullmatch('(Name|Quote|Description)(\d+)',old);target=f'card.C{int(m[2]):03d}.'+{'Name':'name','Quote':'quote','Description':'description'}[m[1]];reason='同ID效果，重新创作夜市名称/手艺短句/精确效果'
    elif k['source']=='TextsProjects':
        m=re.fullmatch('(Title|SubTitle)(\d+)',old);target=f'contract.N{int(m[2]):02d}.'+('name' if m[1]=='Title' else 'subtitle');reason='对应委托原创名称或开张说明'
    elif old in ['', 'REASON_-1']:
        policy='retired';reason='旧空字符串/合法动作不显示错误，无需可见文字'
    elif old.startswith('REASON_'):target='reason.'+old.split('_')[1]
    else:target=old_ui[old]
    text_map.append({'sourceTable':k['source'],'sourceKey':old,'policy':policy,'targetTextKey':target,'reason':reason})
js('replacement/text-keys.json',text_map)

def picture_assets(name):
    return list(dict.fromkeys(by_source[x['ID']]['art'] for x in S['cards'] if norm(x['Picture'])==norm(name)))

resource_map=[]
for name,obj in S['resources'].items():
    art=picture_assets(name);data=None;reason='原共享图片替换为对应的原创卡牌图片'
    if not art:
        if name in ['cardsplay','cardsproject','texts','textscards','textsprojects']:
            data='data/catalog.json';reason='五XML改为唯一JSON数据与中文文本权威'
        elif name.startswith(('00_','10_','card_','materials/')):art=['UI.card-frame'];reason='旧位图卡框/遮罩改原生九宫边框和工作点'
        else:
            mapping={'blocked':['UI.blocked'],'done':['UI.done'],'cancelled':['UI.cancel'],'attack':['UI.inspect'],'list':['UI.log'],'negotiator':['EVT.087','EVT.088'],'optiongreen':['UI.button'],'optionred':['UI.button'],'prj_no':['UI.warning'],'prj_ok':['UI.done'],'prj_wip':['UI.action'],'released':['UI.done'],'door':['BG.title']}
            art=mapping.get(name,[]);reason='旧状态/导航图标替换为确定原生UI或新场景' if art else '旧版专有装饰和表现资源退役，其用途由新卡牌插画/原生事件提示承担'
    resource_map.append(dict(sourceKey=name,sourceObjectID=obj,policy='original-redesign' if art else 'data-replaced' if data else 'retired',targetAssetIDs=art,targetData=data,reason=reason))
js('replacement/resource-keys.json',resource_map)

class_roles={
'ButtonProject':'Presentation.ContractSelectView','Rumble':'Presentation.EventPresenter','Stamp':'Presentation.ResultView','TaskIcon':'Presentation.RoleProgressView','PlayCard':'Core.CardDefinition + Presentation.CardView','ProjectCard':'Core.ContractDefinition + Presentation.ContractView','ScoreCard':'Presentation.ResultView','AI':'Application.AIController','Loader':'Editor.CatalogImporter + Application.Bootstrap','Tutorial':'Application.TutorialSession','hGameState':'Core.MatchPhase','hGameVars':'Core.MatchState','TextInfo':'Presentation.CardDetailView','ObjectToMove':'Presentation.EventPresenter','hLanguage':'Application.StringCatalog','Statistics':'Application.ResultCommitter','Action':'Core.Command + Core.DomainEvent','ActionManager':'Application.CommandBus + Presentation.EventPresenter','CardManager':'Core.CardInstances + Core.TurnReducer','MusicManager':'Presentation.AudioRouter','SoundManager':'Presentation.AudioRouter','TitleManager':'Presentation.ScreenRouter','Button':'Presentation.FocusNavigator','Continue':'Application.GameSession','Fader':'Presentation.ScreenTransition','OptionFlags':'Storage.SettingsDTO','Option':'Presentation.SettingsControl','Options':'Storage.SettingsStore + Presentation.SettingsView','Progressive':'Presentation.TextPageView','TextOutline':'TMP SDF Font Material'}
js('replacement/code-roles.json',[dict(sourceClass=k,targetRole=class_roles[k],policy='rewrite-original',targetSpec='design/08-technical.md',reason='以本版状态/命令/事件重写接口；不复制第三方实现；表现不能结算规则') for k in S['classes']])
js('replacement/metadata-types.json',[dict(sourceType=k,policy='business-role-mapped' if k in class_roles else 'compiler-metadata-retired',targetRole=class_roles.get(k),reason='业务类职责见code-roles' if k in class_roles else '原DLL模块/嵌套辅助/委托/编译器数据由新C#编译器或新DTO重建，不复制旧IL') for k in S['metadataTypes']])

obj_map=[]
for x in S['objects']:
    t=x['type'];assets=picture_assets(x['name']);data=None;policy='engine-reconfigured';spec='design/08-technical.md';reason='新Unity工程以本版设置重建，不沿用Unity4序列化对象'
    if t in ['Texture2D','Sprite','Material']:
        policy='original-redesign';spec='art/ART_DIRECTION.md';assets=assets or ['UI.card-frame','UI.panel'];reason='原媒体不复制；卡图按对应新资产，其他图层/遮罩改原生UI；引擎材质重新配置'
    elif t=='AudioClip':
        policy='original-replaced';spec='design/10-audio-release.md';reason='原音频全部不采用，以24项原创音乐/环境/事件总线重建'
    elif t=='Font':
        policy='audited-font-replaced';assets=['FONT.body'];spec='art/ART_DIRECTION.md';reason='原字体退役，使用已审核中文Noto与完整许可'
    elif t=='TextAsset':
        policy='data-replaced';data='data/catalog.json';spec='design/08-technical.md';reason='旧五XML替换为全量原创JSON及中文键'
    elif t in ['MonoBehaviour','MonoScript']:
        policy='rewrite-original';spec='design/08-technical.md';reason='30业务角色全重写，46旧脚本实例不复制；组件按新屏幕及规则初始化'
    elif t in ['Mesh','MeshFilter','MeshCollider','PhysicsManager','Physics2DSettings','BoxCollider2D','OcclusionCullingSettings','NavMeshSettings','NavMeshProjectSettings','NetworkManager','MasterServerInterface','HaloManager','LightmapSettings']:
        policy='retired';reason='本版离散二维UI不用旧内置网格、物理点击、寻路、联网或烘焙灯光'
    elif t=='Shader':
        policy='engine-recreated';spec='art/ART_DIRECTION.md';reason='用新Unity UI/Default及TMP字体材质，不复制原游戏Shader'
    elif t in ['GameObject','Transform','TextMesh','SpriteRenderer','MeshRenderer','Camera','ParticleSystem','ParticleSystemRenderer']:
        policy='original-redesign';spec='design/07-ui-input.md';reason='按新参考布局与slot锚点重建uGUI/插画层；旧层级位置/文字网格不复用'
    audio_prefix=x['name'].split('_')[0] if t=='AudioClip' else None
    target_audio=[a['id'] for a in C['audio'] if a['id'].split('.')[0]==audio_prefix] if audio_prefix else []
    obj_map.append(dict(sourceObjectID=x['id'],sourceType=t,sourceName=x['name'],sourceExportCount=x['exportCount'],policy=policy,targetAssetIDs=assets,targetAudioIDs=target_audio,targetData=data,targetSpec=spec,reason=reason))
js('replacement/objects.json',obj_map)
csv_rows('replacement/objects.csv',obj_map)

# Finite, independent feasibility witnesses for the service layer.
F={f['id']:f for f in C['facilities']};G={g['id']:g for g in C['guests']}
initial={x['facility']:(x['slot'],x['mode']) for x in C['rules']['initialFacilities']}
def evaluate(layout):
    tags=set();power=set()
    for f,slot,mode in layout:
        m=next(m for m in F[f]['modes'] if m['id']==mode)
        power.update(slot+off for off in m['powerOffsets'] if 0<=slot+off<5)
        if m['noise']:tags.add('noise')
    for f,slot,mode in layout:
        m=next(m for m in F[f]['modes'] if m['id']==mode)
        if not m['requiresPower'] or slot in power:tags.update(m['tags'])
    return tags

cache={};witnesses=[]
for n in C['contracts']:
    key=tuple(sorted(n['guests']))
    if key not in cache:
        needs=set().union(*(set(G[g]['requiredTags']) for g in key));avoid=set().union(*(set(G[g]['forbiddenTags']) for g in key))
        candidates=[]
        for count in range(1,6):
            for fs in itertools.combinations(F,count):
                for modes in itertools.product(*(F[f]['modes'] for f in fs)):
                    possible=set().union(*(set(m['tags']) for m in modes))
                    if not needs<=possible or ('noise' in avoid and any(m['noise'] for m in modes)):continue
                    if any(m['requiresPower'] for m in modes) and not any(m['powerOffsets'] for m in modes):continue
                    cost=sum(F[f]['buyCoins'] for f in fs if f not in initial)
                    retained=sum(f in initial for f in fs)
                    changed=sum(f in initial and initial[f][1]!=m['id'] for f,m in zip(fs,modes))
                    candidates.append((cost+count+changed-retained*2,fs,tuple(m['id'] for m in modes)))
        found=None
        for _,fs,modes in sorted(candidates):
            slots=sorted(itertools.permutations(range(5),len(fs)),key=lambda slots:sum(f in initial and initial[f][0]!=s for f,s in zip(fs,slots)))
            for slots_tuple in slots:
                layout=list(zip(fs,slots_tuple,modes));tags=evaluate(layout)
                if needs<=tags and not avoid&tags:found=layout;break
            if found:break
        if not found:raise ValueError('No legal guest layout '+n['id'])
        cache[key]=found
    target=cache[key];owned={f:{'slot':slot,'mode':mode} for f,(slot,mode) in initial.items()};retired=set();coins=C['rules']['initialCoins'];ap=0;round_no=0;commands=[]
    def act(type, **args):
        global coins,ap,round_no
        fee=F[args['facility']]['buyCoins'] if type=='BuyFacility' else 0
        while ap==0 or coins<fee:
            round_no+=1;coins=min(20,coins+2);ap=3;commands.append({'type':'BeginNextOwnTurn','round':round_no,'coins':coins,'ap':ap})
        ap-=1;coins-=fee
        f=args.get('facility')
        if type=='SellFacility':owned.pop(f);retired.add(f);coins=min(20,coins+F[f]['sellCoins'])
        elif type=='MoveFacility':owned[f]['slot']=args['to']
        elif type=='BuyFacility':owned[f]={'slot':args['slot'],'mode':F[f]['modes'][0]['id']}
        elif type=='SetMode':owned[f]['mode']=args['mode']
        commands.append({'type':type,**args,'afterCoins':coins,'afterAP':ap})
    needed={f for f,_,_ in target};wanted={f:(slot,mode) for f,slot,mode in target}
    for f in list(owned):
        if f not in needed:act('SellFacility',facility=f)
    # Retained pieces can be moved via one empty buffer, so no illegal swaps are implied.
    for _ in range(20):
        pending=[f for f in owned if owned[f]['slot']!=wanted[f][0]]
        if not pending:break
        occupied={v['slot'] for v in owned.values()};movable=[f for f in pending if wanted[f][0] not in occupied]
        f=movable[0] if movable else pending[0];dest=wanted[f][0] if movable else next(s for s in range(5) if s not in occupied)
        act('MoveFacility',facility=f,to=dest)
    else:raise ValueError('Move loop')
    for f,slot,mode in target:
        if f not in owned:act('BuyFacility',facility=f,slot=slot)
        if owned[f]['mode']!=mode:act('SetMode',facility=f,mode=mode)
    tags=sorted(evaluate(target))
    witnesses.append({'contractID':n['id'],'guestIDs':n['guests'],'layout':[dict(facility=f,slot=s,mode=m) for f,s,m in target],'providedTags':tags,'commands':commands,'ownTurnsRequired':round_no,'coinsRemaining':coins,'actionsUsed':sum(x['type']!='BeginNextOwnTurn' for x in commands),'proofScope':'设计层有限设施服务/经济/行动路径；未执行游戏规则或全对局随机可达测试','status':'design-witness'})
js('data/opening-layouts.json',witnesses)

# Human-readable content with all original Chinese names, descriptions and scripts.
out=['# 全部内容手册','', '由 data/catalog.json 派生。数值和文字修改回到JSON，不能直接维护本文件。', '', '## 95张卡牌', '']
for c in C['cards']:
    out += [f'### {c["id"]} {c["name"]}', '', c['description'], '', f'“{c["quote"]}”', '', f'类型 {c["kind"]}；岗位 {c["department"] or "全局/选目标"}；工作/停工 {c["turns"]}；量 {c["amount"]}；AP {c["ap"]}；图 {c["art"]}；原卡 {c["sourceID"]}。', '']
out += ['## 28个开张委托','']
for n in C['contracts']:
    out += [f'### {n["id"]} {n["name"]}', '', n['subtitle'], '', f'第{n["chapter"]}章，{n["district"]}；工作 '+', '.join(f'{d["name"]}{n["goals"][d["id"]]}' for d in C['departments'])+f'；来客 {", ".join(G[g]["name"] for g in n["guests"])}；期限20轮。', '', '入场：'+n['openingLine'], '', '胜利：'+n['successLine'], '', '失败：'+n['failureLine'], '', '源有效工作：'+json.dumps(n['sourceEffectiveGoals'],ensure_ascii=False)+f'；源Animation={n["sourceAnimation"]}。'+n['numericDecision'], '']
out += ['## 12件设施与24种用途','']
for f in C['facilities']:
    out += [f'### {f["id"]} {f["name"]}', '',f'买入 {f["buyCoins"]} 钱；变卖 {f["sellCoins"]} 钱；每方1件；图 {f["art"]}。', '']
    for m in f['modes']:out += [f'- {m["id"]} {m["name"]}：服务 {", ".join(m["tags"]) or "无"}；需电 {m["requiresPower"]}；供电邻格 {m["powerOffsets"]}；噪声 {m["noise"]}。']
    out.append('')
out += ['## 5类客人','']
for g in C['guests']:out += [f'- {g["id"]} {g["name"]}：需要 {", ".join(g["requiredTags"])}，禁忌 {", ".join(g["forbiddenTags"]) or "无"}。“{g["request"]}”']
out += ['','## 7章完整记事','']
for ch in C['chapters']:out += [f'### {ch["id"]} {ch["name"]}', '', '委托：'+', '.join(ch['contracts']), '', '开场：'+ch['intro'], '', '收章：'+ch['outro'], '', ch['reward']+'。失败：'+ch['fail']+'。', '']
out += ['## 46条角色展示档案（锅青双用途，共45个命名身份）','']
for ch in C['characters']:out += [f'- {ch["id"]} {ch["name"]}：{ch["identity"]}；{ch["costume"]}；{ch["prop"]}；动机：{ch["motivation"]}；语气：{ch["voice"]}；图 {ch["portrait"]}。']
out += ['','## 14步完整教学','']
for t in C['tutorial']:out += [f'- {t["id"]}：{C["strings"][t["textKey"]]}（{t["anchor"]}；{t["requiredAction"]}）']
put('design/04-content.md','\n'.join(out)+'\n')

summary={
    'sourceCards':len(S['cards']),'sourceProjects':len(S['projects']),
    'sourceTextKeys':len(text_map),'sourceResourceKeys':len(resource_map),
    'sourceObjects':len(obj_map),'sourceBusinessClasses':len(S['classes']),
    'sourceMetadataTypes':len(S['metadataTypes']),
    'sourceFlowStates':len(S['states']),
    'sourceNonemptyExportFiles':sum(o['sourceExportCount'] for o in obj_map),
    'newCards':len(C['cards']),'newContracts':len(C['contracts']),
    'newFacilities':len(C['facilities']),'newModes':sum(len(f['modes']) for f in C['facilities']),
    'newGuests':len(C['guests']),'newStrings':len(C['strings']),
    'rasterPrompts':len(jobs),'editPrompts':len(jobs),'allAssetSpecs':len(C['assets']),
    'audioSpecs':len(C['audio']),'goldenRuleCases':len(C['goldenCases']),
    'facilityWitnesses':len(witnesses),'maxFacilityWitnessTurns':max(w['ownTurnsRequired'] for w in witnesses),
    'catalogSha256':hashlib.sha256((P/'data/catalog.json').read_bytes()).hexdigest(),
}
js('evidence/coverage.json',summary)
put('replacement/README.md','# 包装替换总账\n\n全部记录从只含必要结构的来源快照与本版catalog派生，旧文本全文/源码/媒体不进入新项目。原作所有结构条目都有保留语义并原创替换、引擎重建、数据替换或明确退役的处置；完整覆盖不意味着复用原媒体。\n\n- [18旧流程状态](state-flow.json) / [系统改动](system-decisions.json)：输入、设置、胜负、保存、语言、得分与新增规则逐项锁定。\n- [95卡](cards.json)：源类型、时长、图片名到新卡/新图。\n- [28项目](contracts.json)：原有效五值、Animation到新五值及客人，逐项记录修正理由。\n- [420文本键](text-keys.json)：新原创中文键或明确退役；空键和REASON_-1共2项退役。\n- [74资源键](resource-keys.json)：原共享图片到多张原创新图，UI/数据替换与退役。\n- [1985对象](objects.json) / [CSV](objects.csv)：全部对象及345个非空导出所属对象逐项处置；空载荷/引擎默认资源也不漏账。\n- [30业务类](code-roles.json)：原创新架构职责与替换接口。\n- [40元类型](metadata-types.json)：30业务类职责和10个模块/嵌套/编译器类型的明确退役。\n\n旧Texture/Sprite名字不能作为复用授权；每个targetAssetID都必须在本版assets存在。场景/Transform等按新slot锚点与uGUI重建，不假称把462个旧GameObject原样搬过来。字体/音轨全部新来源；3D内置网格、碰撞点选、联网和旧设置不采用。原5个XML统一替为JSON，字段决定写在新规则中。\n')
print(json.dumps(summary,ensure_ascii=False))
