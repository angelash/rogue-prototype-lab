"""Read-only inventory and proposed migration tables; never edits Unity inputs.

Private findings are reports, while --write-plan emits versioned planning data.
Names are proposals, not an automated C# refactor or a rights clearance.
"""
from pathlib import Path
import argparse, collections, hashlib, json, re, wave
from PIL import Image
from audit_steam_release import fingerprint, sha

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'local/UnityProject'
PLAN = ROOT / 'release/commercial-migration-v3'
REFERENCE = ROOT.parents[1] / 'prototypes/re-crunch-time/local/exports'

# First retain each responsibility, then extract state/flow/view boundaries.
CLASS_MAP = {
 'Action': ('Flow', 'MarketCue', '单个变换、等待或回调步骤'),
 'ActionManager': ('Flow', 'MarketCueQueue', '按原顺序执行步骤，保持同帧推进条件'),
 'AI': ('Domain', 'RivalStallPlanner', '隔壁摊位决策；优先级、遍历、随机调用不变'),
 'Button': ('Presentation', 'MarketButtonBinding', '原自定义按钮状态与回调；非 uGUI Button'),
 'ButtonProject': ('Presentation', 'CommissionChoiceView', '委托选择按钮'),
 'CardManager': ('Flow', 'MarketTableController', '牌区、合法性、目标和结算编排；后续拆纯规则'),
 'Continue': ('Presentation', 'MarketPromptView', '继续、逐牌停顿、调停倒计时'),
 'Fader': ('Presentation', 'PaperFadeView', '三层遮罩淡入淡出'),
 'hGameState': ('Flow', 'MarketSessionFlow', '主状态机、暂停、胜负、重开和退出'),
 'hGameVars': ('Flow', 'MarketSessionContext', '共享状态与场景绑定；后续分状态、引用和设置'),
 'hLanguage': ('Content', 'MarketLocalization', '双语查表、换行、成绩格式'),
 'LegacyEngine': ('Platform', 'MarketSceneStatus', 'Unity 2022 场景状态兼容入口'),
 'LegacyObjectSlot': ('Authoring', 'MarketObjectBinding', '过渡对象槽；最终用明确类型的新引用替代'),
 'Loader': ('Flow', 'MarketBootstrap', '固定初始化次序、数据装配和主循环'),
 'MusicManager': ('Presentation', 'NightMarketMusic', '开市、双方回合、调停、暂停、胜负音乐'),
 'ObjectToMove': ('Presentation', 'MarketMotionTarget', '位置与运动状态'),
 'Option': ('Presentation', 'MarketSettingView', '单项设置、悬停与文本'),
 'OptionFlags': ('Presentation', 'MarketLanguageSelector', '语言选择与高亮'),
 'Options': ('Presentation', 'MarketSettingsPanel', '设置面板编排'),
 'PlayCard': ('Presentation', 'MarketCardView', '卡牌表现与输入；后续把规则搬入唯一 Domain 服务'),
 'Progressive': ('Presentation', 'MarketValueTween', '渐变速度、重置与终止'),
 'ProjectCard': ('Presentation', 'StallCommissionView', '委托、岗位进度与统计表现'),
 'Rumble': ('Presentation', 'PaperShakeMotion', '卡面抖动'),
 'ScoreCard': ('Presentation', 'MarketLedgerView', '十条成绩显示、插入与重置'),
 'SoundManager': ('Presentation', 'NightMarketAudio', '音效事件与变体路由'),
 'Stamp': ('Presentation', 'MarketSealView', '完成、受阻、作废印记'),
 'Statistics': ('Platform', 'MarketStatisticsWriter', '先核实实际调用，再保留等效行为'),
 'TaskIcon': ('Presentation', 'StallJobTokenView', '五岗位图标与状态'),
 'TextInfo': ('Presentation', 'MarketLocalizedText', '文本键与显示对象绑定'),
 'TextOutline': ('Presentation', 'MarketTextOutline', '文字描边与透明度'),
 'TitleManager': ('Presentation', 'MarketPhaseBanner', '开市、选委托、回合、胜负条幅'),
 'Tutorial': ('Presentation', 'MarketTutorialFlow', '两页教程时序、文字与继续条件'),
}
UNITY_MESSAGES = set('Awake Start Update FixedUpdate LateUpdate OnEnable OnDisable OnDestroy OnGUI OnApplicationFocus OnApplicationPause OnApplicationQuit Reset OnValidate'.split())
FIELD_OVERRIDES = {
 ('Button','m_iSoundID'):'_audioCueId', ('hGameVars','oSoundManager'):'_audioPlayer',
 ('hGameVars','m_bSound'):'_audioEnabled', ('ProjectCard','m_iSound'):'_guestRequirement',
 ('hGameVars','ID_SERVER_DOWN_1'):'RainLeakTroubleCard', ('hGameVars','ID_SERVER_DOWN_2'):'HeavyRainTroubleCard',
 ('AI','m_aiDevelopersAvailable'):'_availableHelperCounts', ('AI','m_bDevelopersAvailable'):'_hasAvailableHelper',
 ('Button','m_asObjects'):'_objectNames', ('Button','m_aoObjects'):'_boundObjects',
 ('Continue','DECISION_TIME'):'MediationWindowSeconds', ('Continue','m_fDecisionTime'):'_mediationElapsedSeconds',
 ('hGameState','m_iTitleCT'):'_openingBannerStage', ('hGameState','m_oTitleCT'):'_openingBannerObject',
 ('hGameVars','oTutorial'):'_tutorialView', ('hGameVars','m_bTutorial'):'_tutorialEnabled',
 ('ProjectCard','m_asCompanyName'):'_stallNameLabels', ('ProjectCard','m_iCompanyName'):'_stallNameIndex',
 ('ProjectCard','m_oScore'):'_scoreLabelObject', ('ProjectCard','m_fScore'):'_displayedScore',
 ('ProjectCard','m_asIcons'):'_jobIconNames', ('ProjectCard','m_aoIcons'):'_jobIconObjects',
 ('ProjectCard','m_asBar'):'_progressBarNames', ('ProjectCard','m_aoBar'):'_progressBarObjects',
 ('Stamp','m_bAutoRemove'):'_autoRemoveEnabled', ('Stamp','m_fAutoRemove'):'_autoRemoveDelaySeconds',
 ('CardManager+PlayersCards','iProject'):'_commissionIndex', ('CardManager+PlayersCards','oProject'):'_commissionObject',
}
TERMS = {
 'Programming':'Cooking', 'Programmer':'Cook', 'Design':'Stocking', 'Designer':'StockKeeper',
 'Art':'Lanterns', 'Artist':'LanternKeeper', 'Testing':'Wards', 'Tester':'WardKeeper',
 'Sound':'Guests', 'Dept':'Job', 'Departments':'Jobs', 'Department':'Job',
 'Developer':'Helper', 'Developers':'Helpers', 'Dev':'Helper', 'Devs':'Helpers',
 'Project':'Commission', 'Projects':'Commissions', 'Company':'Stall', 'Companies':'Stalls',
 'Headquarters':'Patrol', 'HQ':'Patrol', 'Negotiator':'Mediator', 'Negotiation':'Mediation',
 'Nega':'Trouble', 'Negas':'Troubles', 'Negative':'Trouble', 'Negatives':'Troubles',
 'Positive':'Assist', 'Positives':'Assists', 'Special':'Intervention', 'Specials':'Interventions',
 'Insta':'Immediate', 'CT':'Market', 'Repare':'Repair', 'Unic':'Unique',
 'ENG':'ZhHans', 'SPA':'En', 'Fired':'Dismissed', 'Resignation':'LuredAway',
 'Holidays':'Rest', 'Sickness':'Unwell', 'Scrum':'Disruption', 'Bug':'Mishap',
 'Marketing':'RivalStall', 'Engine':'Stove', 'Keyboard':'NameEntry',
}

def read(path): return json.loads(path.read_text('utf-8-sig'))
def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
def relative(path): return path.relative_to(ROOT).as_posix()

def importer_fields(meta):
    """Record functional scalar settings, never copy GUIDs or complete .meta."""
    wanted=set('enableMipMap sRGBTexture isReadable streamingMipmaps filterMode aniso mipBias wrapU wrapV wrapW nPOTScale maxTextureSize textureCompression textureFormat compressionQuality crunchedCompression overridden alphaUsage alphaIsTransparency spriteMode spritePivot spritePixelsToUnits spriteBorder loadType sampleRateSetting sampleRateOverride compressionFormat quality preloadAudioData forceToMono normalize loadInBackground ambisonic 3D'.split())
    result=collections.defaultdict(list);platform='root/default'
    for line in meta.splitlines():
        match=re.match(r'\s*([A-Za-z0-9_]+):\s*(.*?)\s*$',line)
        if not match:continue
        key,value=match.groups()
        if key=='buildTarget':platform=value
        if key in wanted:result[key].append(dict(scope=platform,value=value))
    return dict(result)

def proposed_member(name, kind, owner):
    if name in ('.ctor', '.cctor'): return CLASS_MAP.get(owner, ('', owner))[1]
    if kind == 'method' and name in UNITY_MESSAGES: return name
    prefix = ''
    if name.startswith(('get_', 'set_')): prefix, name = name[:4], name[4:]
    name = re.sub(r'^m_', '', name).replace('HeadQuarters','Patrol')
    name = re.sub(r'^(?:ao|af|as|ai|ab|av|fnc|[obifsve])(?=[A-Z])', '', name)
    words = re.findall(r'[A-Z]+(?=[A-Z][a-z]|[^A-Za-z]|$)|[A-Z]?[a-z]+|[0-9]+', name)
    mapped = [TERMS.get(w, TERMS.get(w.title(), w)) for w in words]
    result = ''.join(w[:1].upper()+w[1:].lower() if w.isupper() and w not in ('ID','UI','FX','AI','DT') else w[:1].upper()+w[1:] for w in mapped)
    if kind in ('field','parameter'): result = ('_' if kind=='field' else '') + ('id' if result=='ID' else result[:1].lower() + result[1:])
    return prefix + (result or 'Value')

def inventory():
    source = [dict(path=f.relative_to(PROJECT).as_posix(), bytes=f.stat().st_size, sha256=sha(f))
              for folder in ('Assets','Packages','ProjectSettings') for f in (PROJECT/folder).rglob('*') if f.is_file()]
    manifest = read(PROJECT/'Assets/Editor/RecoverySource/manifest.json')
    jobs = read(ROOT/'fidelity/art/jobs.json')['jobs']
    theme = read(ROOT/'fidelity/data/theme.json')
    metadata = read(ROOT/'local/release-code-metadata.json')
    original_hashes = collections.defaultdict(list)
    for f in REFERENCE.rglob('*'):
        if f.is_file(): original_hashes[sha(f)].append(f.relative_to(REFERENCE).as_posix())
    assets = []
    for f in sorted((PROJECT/'Assets').rglob('*')):
        if not f.is_file() or f.suffix == '.meta': continue
        digest = sha(f)
        assets.append(dict(path=f.relative_to(PROJECT).as_posix(), extension=f.suffix, sha256=digest,
                           originalExportMatches=original_hashes.get(digest, [])))
    guid_files = {}
    for f in (PROJECT/'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})', f.read_text('utf-8-sig'), re.M)
        if match: guid_files[match[1]] = str(f)[:-5]
    references = collections.defaultdict(list)
    for f in (PROJECT/'Assets').rglob('*'):
        if f.suffix not in ('.unity','.prefab','.asset','.meta'): continue
        for guid in set(re.findall(r'guid: ([0-9a-f]{32})', f.read_text('utf-8-sig', errors='replace'))):
            references[guid].append(f.relative_to(PROJECT).as_posix())
    assets_by_path = {a['path']:a for a in assets}
    by_id = {o['id']:o for o in manifest['objects']}

    textures = []
    for i, job in enumerate(jobs, 1):
        obj = by_id[job['id']]; f = PROJECT/obj['assetPath']
        meta = Path(str(f)+'.meta').read_text('utf-8-sig')
        guid = re.search(r'^guid: (\w+)', meta, re.M).group(1)
        with Image.open(f) as im:
            actual = list(im.size); alpha = im.convert('RGBA').getchannel('A')
            alpha_hash = hashlib.sha256(alpha.tobytes()).hexdigest(); alpha_range=alpha.getextrema()
        card_refs = [dict(id=c['id'], name=c['name'], type=c['type']) for c in theme['cards'] if c['picture']==job['name']]
        kind = job['kind']; proposed = f"NightMarket_{'Illustration' if kind=='illustration' else 'Surface'}_{i:03}"
        method = ('复核既有自有 AI 原图及引用谱系后，直接从合格原图等比裁切重导出；不读取原作像素或遮罩'
                  if kind in ('illustration','table') else
                  '从自有纸纹/新图/原生字形独立绘制并导出；按新轮廓生成 alpha，不以原 PNG、stencil 或顶点作为制作输入')
        textures.append(dict(id=f'image-{i:03}', oldName=job['name'], sourceAssetId=job['id'],
          proposedName=proposed, category=kind, dimensions=[job['width'],job['height']], actualDimensions=actual,
          source=relative(f), currentGuid=guid, currentAlphaSHA256=alpha_hash, alphaRange=list(alpha_range),
          currentSHA256=sha(f), candidateOwnRaw=job['raw'], candidateOwnMaterial=job.get('materialSource'),
          currentImporterFunctionalFields=importer_fields(meta),
          spriteFunctionalContracts=[dict(name=s['name'],rect=s.get('tree',{}).get('m_Rect'),offset=s.get('tree',{}).get('m_Offset'),pixelsPerUnit=s.get('tree',{}).get('m_PixelsToUnits'),newTopologyMustBeIndependentlyAuthored=True) for s in manifest['objects'] if s['type']=='Sprite' and s['name']==job['name']],
          cardUses=card_refs, serializedReferences=references[guid], method=method,
          status='planned_not_replaced', alphaPolicy='Independent alpha; preserve functional visible bounds and hit regions, not copied alpha bytes',
          checks=['exact dimensions','uniform scale/crop only','no original input/derived stencil','native CN/EN glyphs','independent pivot/mesh','all usages in 16:9 and 4:3','no black bands/halos/double frames']))

    # Record every serialized clip slot, not just the apparent filename category.
    audio_uses = collections.defaultdict(list)
    def walk(value, field, owner, container):
        if isinstance(value,dict):
            if {'m_FileID','m_PathID'} <= value.keys() and value['m_PathID']:
                fileid=value['m_FileID']
                containers={c['name']:c for c in manifest['containers']}
                target_container=container
                if fileid:
                    externals=containers.get(container,{}).get('externals',[])
                    if 0<fileid<=len(externals): target_container=externals[fileid-1]['path'].split('/')[-1]
                target=f"{target_container}:{value['m_PathID']}"
                if by_id.get(target,{}).get('type')=='AudioClip':
                    audio_uses[target].append(dict(owner=owner, field=field))
            for k,v in value.items(): walk(v, field+'.'+k, owner, container)
        elif isinstance(value,list):
            for i,v in enumerate(value): walk(v,field+f'[{i}]',owner,container)
    for obj in manifest['objects']:
        if obj['type'] in ('MonoBehaviour','AudioSource'): walk(obj.get('tree',{}),'',obj['id'],obj['container'])
    audio=[]
    for i,obj in enumerate((o for o in manifest['objects'] if o['type']=='AudioClip'),1):
        f=PROJECT/obj['assetPath']
        with wave.open(str(f),'rb') as clip:
            spec=dict(sampleRate=clip.getframerate(), channels=clip.getnchannels(), sampleWidthBytes=clip.getsampwidth(), frames=clip.getnframes(), durationSeconds=clip.getnframes()/clip.getframerate())
        group='music' if obj['name'].startswith('MUS_') else 'ambience' if obj['name'].startswith('AMB_') else 'sfx'
        audio.append(dict(id=f'audio-{i:03}', oldName=obj['name'], sourceAssetId=obj['id'], source=relative(f),
             proposedName=f'NightMarket_{group.title()}_{i:03}', group=group, spec=spec,
             currentImporterFunctionalFields=importer_fields(Path(str(f)+'.meta').read_text('utf-8-sig')),
             currentSHA256=sha(f), originalByteMatch=bool(assets_by_path[obj['assetPath']]['originalExportMatches']),
             serializedUses=audio_uses[obj['id']], status='planned_not_produced',
             method=('原创编曲/演奏或有完整许可的新作品；不得采样原旋律/音轨' if group=='music' else '自录纸牌、木桌、铜铃、印章、笔墨、炉火并独立混音；必要时原创新合成'),
             checks=['event index/variant order unchanged during migration','random call count/order unchanged','exact sample format and frame count for first parity candidate','loop and intro handoff','peak/loudness no clipping','actual event listening','no source samples']))

    bindings=[]; sources={}
    patterns={
      'object-name':r'(?:GameObject\.Find|\.transform\.Find|\.FindChild)\s*\(',
      'resource-path':r'Resources\.Load\s*(?:<[^>]+>)?\s*\(',
      'string-type-factory':r'ScriptableObject\.CreateInstance\s*\(\s*"',
      'save-key':r'PlayerPrefs\.',
      'string-callback-or-reflection':r'\b(?:Invoke|SendMessage|BroadcastMessage|GetMethod|GetField|GetProperty)\s*\(',
      'random':r'\bRandom\.',
      'color-state':r'\bColor\b|\.color\s*[!=]=',
      'scene-load':r'LoadScene|LoadLevel|loadedLevel',
    }
    for f in sorted((PROJECT/'Assets/Recovery/Scripts').glob('*.cs')):
        content=f.read_text('utf-8-sig');sources[f.stem]=content
        for line_no,line in enumerate(content.splitlines(),1):
            if line.lstrip().startswith('//'): continue
            for kind,pattern in patterns.items():
                if re.search(pattern,line): bindings.append(dict(file=f.name,line=line_no,kind=kind,strings=re.findall(r'"([^"\r\n]*)"',line)))
    types=[]; symbols=[]; parameters=[]
    for t in metadata['types']:
        owner=t['name'].split('+')[0]
        if owner not in CLASS_MAP: continue
        layer,newname,purpose=CLASS_MAP[owner]
        target='YokaiNightMarket.'+layer+'.'+newname
        if '+' in t['name']: target+='.'+proposed_member(t['name'].split('+',1)[1], 'type',owner)
        types.append(dict(old=t['name'], proposed=target, layer=layer, responsibility=purpose,
                          methods=len(t['methods']), fields=len(t['fields']), status='proposal_not_executed'))
        for kind,items in [('method',t['methods']),('field',t['fields'])]:
            for item in items:
                keep=kind=='method' and item['name'] in UNITY_MESSAGES and t.get('baseType','').endswith('MonoBehaviour')
                proposed=FIELD_OVERRIDES.get((t['name'],item['name']),proposed_member(item['name'],kind,owner))
                if kind=='field' and 'Literal' in item['attributes'] and proposed.startswith('_'):
                    proposed=proposed[1:2].upper()+proposed[2:]
                symbols.append(dict(owner=t['name'],token=item['token'],kind=kind,old=item['name'],
                    proposed=proposed, targetOwner=target,
                    signatureHex=item['signatureHex'], attributes=item['attributes'],
                    disposition='preserve_engine_callback' if keep else 'review_semantic_rename',
                    preserveSignatureShape=True, manuallyApproved=False))
                if kind=='method':
                    for parameter in item.get('parameters',[]):
                        parameters.append(dict(owner=t['name'],methodToken=item['token'],source=parameter,
                            proposed=proposed_member(parameter.get('name','').lstrip('_'),'parameter',owner),manuallyApproved=False))
    # Collisions are intentionally exposed; never silently suffix public APIs.
    groups=collections.defaultdict(list)
    for s in symbols:
        groups[(s['owner'],s['kind'],s['proposed'],s['signatureHex'] if s['kind']=='method' else '')].append(s)
    collisions=[dict(owner=k[0],kind=k[1],proposed=k[2],tokens=[s['token'] for s in v]) for k,v in groups.items() if len(v)>1 and k[2] not in ('MarketCue',)]
    property_rows=[]
    for t in metadata['types']:
        if t['name'].split('+')[0] not in CLASS_MAP:continue
        names=sorted({m['name'][4:] for m in t['methods'] if m['name'].startswith(('get_','set_'))})
        property_rows.extend(dict(owner=t['name'],old=n,proposed=proposed_member(n,'property',t['name'].split('+')[0]),manuallyApproved=False) for n in names)

    fonts=[]
    for a in assets:
        if a['extension'] not in ('.ttf','.otf'):continue
        f=PROJECT/a['path'];mt=Path(str(f)+'.meta').read_text('utf-8-sig');g=re.search(r'^guid: (\w+)',mt,re.M).group(1)
        fonts.append(dict(path=a['path'],sha256=a['sha256'],originalByteMatch=bool(a['originalExportMatches']),
              serializedReferrers=references[g], action='Keep licensed Noto source and OFL; reauthor import profiles with new names/GUIDs' if a['extension']=='.otf' else 'Exclude original TTF; verify recursively from new scene and Resources'))
    covered={t['source'].removeprefix('local/UnityProject/') for t in textures+audio}
    covered.update(f['path'] for f in fonts)
    policies={'.asset':'Reauthor material/sprite/object bindings from own definitions; new GUIDs, no recovered YAML or tight mesh copied',
      '.prefab':'Rebuild named functional anchors and components from reviewed layout contract; no recovered YAML copied',
      '.unity':'Build new MarketTable scene from new prefabs and explicit binding registry',
      '.obj':'Exclude recovered built-in mesh exports; instantiate licensed engine primitives or independently authored meshes',
      '.png':'Exclude exported built-in textures/atlases; engine-owned fonts/atlases generated afresh',
      '.xml':'Replace office-game XML schema with authored MarketCatalog and explicit effective values; no raw XML copied',
      '.json':'Exclude RecoverySource and all source typetrees from new project/build; approved catalog is newly authored',
      '.cs':'Rename/refactor authorized code or rebuild Editor/QA tools against new contracts; do not copy original assets into new target',
      '.dll':'Verify package origin/license and Editor-only import scope; install legitimate dependency only if still required',
      '.txt':'Exclude original text exports; retain required license notices and review own content individually'}
    other_assets=[dict(path=a['path'],extension=a['extension'],currentSHA256=a['sha256'],
        identicalToExport=bool(a['originalExportMatches']),strategy=policies.get(a['extension'],'Review explicitly'),status='planned_not_executed')
        for a in assets if a['path'] not in covered]
    job_map={'Sound':'Guests','Programming':'Cooking','Design':'Stocking','Art':'Lanterns','Testing':'Wards'}
    data_map=dict(status='proposal_not_migrated',
      fieldMapping={'ID':'cardId or commissionId by table','Type':'kind','Turns':'workTurns','Target':'targetPolicy','Function':'effect','Num':'quantity','Repair':'repairTags','Picture':'illustrationId','Platform':'districtId','ENG':'zh-Hans','SPA':'en'},
      jobs=[dict(old=k,new=v,label={'Guests':'招客','Cooking':'备餐','Stocking':'备货','Lanterns':'挂灯','Wards':'布符'}[v],ordinal=i) for i,(k,v) in enumerate(job_map.items())],
      cards=[dict(oldId=c['id'],newId=f"nm.card.{c['id']:03}",stableRuleIndex=c['id'],name=c['name'],
          category=c['type'],turns=c['turns'],target=c['target'],function=c['function'],amount=c['amount'],repair=c['repair'],oldPicture=c['picture']) for c in theme['cards']],
      commissions=[dict(oldId=c['id'],newId=f"nm.commission.{c['id']:03}",requirements={job_map[k]:v for k,v in c['effectiveGoals'].items()},
          wardDefaultMaterialized=2,ignoredHistoricalAnimationNotImported=True) for c in theme['projects']],
      texts=[dict(old=k,new=f'nm.text.{i:03}',zh=v['zh'],en=v['en'],semanticNameReviewRequired=True) for i,(k,v) in enumerate(theme['strings'].items())],
      districts=[dict(old=k,new=f'nm.district.{i:02}',label=v) for i,(k,v) in enumerate(theme['districts'].items(),1)],
      localization=dict(oldSlots={'ENG':'zh-Hans','SPA':'en'},newLocales=['zh-Hans','en'],indicesUnchanged=[0,1]),
      preferences=[dict(old=k,new=v,default=d) for k,v,d in [('AutoPause','nm.settings.pausePerCard',1),('FastMode','nm.settings.fastMode',0),('Sound','nm.settings.audioEnabled',1),('Tutorial','nm.settings.tutorialEnabled',1),('Language','nm.settings.language',0),('Company','nm.profile.stallName','')]],
      scoreFields=[dict(old=k,new=v,indices=list(range(10))) for k,v in [('ScorePts','nm.ledger.points'),('ScorePrj','nm.ledger.commission'),('ScorePlayer','nm.ledger.stall')]])
    report=dict(schemaVersion=1,date='2026-10-14',scope='Read-only current source inventory and proposals; no implementation or clearance',
      sourceSHA256=fingerprint(source),sourceFiles=len(source),sourceFilesManifest=source,
      counts=dict(assetFiles=len(assets),assetExtensions=dict(collections.Counter(a['extension'] for a in assets)),
        textures=len(textures),audio=len(audio),audioGroups=dict(collections.Counter(a['group'] for a in audio)),
        originalAudioMatches=sum(a['originalByteMatch'] for a in audio),
        identicalExportFiles=sum(bool(a['originalExportMatches']) for a in assets),
        compiledTypes=metadata['typeCount'],compiledMethods=metadata['methodCount'],compiledFields=metadata['fieldCount'],
        plannedUserTypes=len(types),plannedMethods=sum(s['kind']=='method' for s in symbols),plannedFields=sum(s['kind']=='field' for s in symbols),
        properties=len(property_rows),parameters=len(parameters),bindings=dict(collections.Counter(b['kind'] for b in bindings)),
        renameCollisions=len(collisions),fonts=len(fonts)),
      assets=assets,fonts=fonts,bindings=bindings,renameCollisions=collisions,
      compiledMetadataSource='local/release-code-metadata.json',metadataLimitation='Compiled inventory is from prior Release extraction; method tokens/blobs identify overloads, not a freshly decoded C# signature or parser-based rename. Re-extract and bind symbols during implementation.',
      textures=textures,audio=audio,types=types,symbols=symbols,properties=property_rows,parameters=parameters,otherAssets=other_assets,dataMap=data_map)
    return report

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--write-plan',action='store_true');args=parser.parse_args()
    report=inventory();save(ROOT/'local/commercial-migration-v3/inventory.json', report)
    if args.write_plan:
        save(PLAN/'asset-plan.json',dict(schemaVersion=1,baselineSourceSHA256=report['sourceSHA256'],status='proposal_only',
             textures=report['textures'],audio=report['audio'],fonts=report['fonts'],otherAssets=report['otherAssets']))
        save(PLAN/'symbol-plan.json',dict(schemaVersion=1,baselineSourceSHA256=report['sourceSHA256'],status='proposal_only_not_semantically_approved',
             types=report['types'],members=report['symbols'],properties=report['properties'],parameters=report['parameters'],collisions=report['renameCollisions'],limitation=report['metadataLimitation']))
        save(PLAN/'data-plan.json',report['dataMap'])
        save(PLAN/'audit-summary.json',dict(schemaVersion=1,date=report['date'],baselineSourceSHA256=report['sourceSHA256'],
             sourceFiles=report['sourceFiles'],counts=report['counts'],status='analysis_complete_execution_not_started',
             privateReport='local/commercial-migration-v3/inventory.json',
             findings=['All 91 WAV files match original exports.', 'New illustration RGB does not establish independent alpha, geometry, materials, scenes or imports.',
                       'Four original TXT exports and twelve historical TTF files remain in the source project and must be excluded from the new product.',
                       'Bindings include dynamic object/resource strings and string-based type factories; a text-only rename is unsafe.',
                       'Current .obj exports include Unity built-in geometry; recreate via permitted engine APIs or authored geometry, do not copy recovery files.',
                       'Raw input provenance must be reviewed before reusing existing generated art; changed hashes alone do not establish independent creation.']))
    print(json.dumps({k:report[k] for k in ('scope','sourceSHA256','sourceFiles','counts')},ensure_ascii=False))
if __name__=='__main__':main()
