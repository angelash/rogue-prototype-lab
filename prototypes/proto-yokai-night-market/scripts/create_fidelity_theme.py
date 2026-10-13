"""Original night-market text/art specification. Numeric behavior comes from the local reference."""
from __future__ import annotations
import json, re, shutil, hashlib, textwrap, xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
REPO=ROOT.parents[1]
SOURCE=REPO/'prototypes/re-crunch-time'
PROJECT=ROOT/'local/UnityProject'

def read(p):return json.loads(p.read_text('utf-8'))
def write(p,v):
    p.parent.mkdir(parents=True,exist_ok=True)
    content=(v if isinstance(v,str) else json.dumps(v,ensure_ascii=False,indent=2)).encode('utf-8')
    if not p.exists() or p.read_bytes()!=content:p.write_bytes(content)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def copy_if_changed(source,target):
    if not target.exists() or sha(source)!=sha(target):shutil.copyfile(source,target)

UI={
 '':'', 'Pause':'设置','Resume':'继续','NextTurn':'交班','AutoPause':'逐牌停顿','FastMode':'快进',
 'Tutorial':'教学','PlayAgain':'再开一摊','DisableAutoPause':'关闭逐牌停顿','Continue':'继续',
 'ClickToUse':'按任意键使用调停牌','Recruiting':'招募帮工','SelectingProjects':'抽取开张委托',
 'YourTurn':'轮到你的摊位','AITurn':'隔壁摊位行动','YouWin':'你的夜市开张了','YouLose':'隔壁先开张了',
 'AI':'隔壁摊','PLAYER':'你的摊','Player':'你','YOURPROJECT':'你的委托','EDIT':'改名',
 'Design':'备货','Programming':'料理','Art':'灯饰','Testing':'护阵','Sound':'招徕',
 'SOUND DESIGNER':'招徕帮工','SOUND DESIGNERGirl':'招徕帮工','PROGRAMMER':'料理帮工','PROGRAMMERGirl':'料理帮工',
 'DESIGNER':'备货帮工','DESIGNERGirl':'备货帮工','ARTIST':'灯饰帮工','TESTER':'护阵帮工','TESTERGirl':'护阵帮工',
 'OUCH':'闹场','INSTA-OUCH':'挖角','COOL':'搭把手','SPECIAL':'修补','DIRECTION':'调停','HEADQUARTERS':'巡街','PROJECT':'委托',
 'Week':'第','MoneySymbol':'文','NewRecord':'新纪录','SCORECARDTITLE':'夜市功劳簿',
 'TUTO_Welcome':'欢迎来到妖怪夜市！','TUTO_Obj':'目标：比隔壁摊先完成五项筹备。',
 'TUTO_Para':'每个摊位抽到一份不同的委托。\n左边数字是已完成工作，右边是所需工作。',
 'TUTO_Turno':'每轮先自动修补、推进在岗帮工，再补牌。\n随后可以连续出牌，准备好就交班。',
 'TUTO_Cada':'帮工需工作两轮或四轮，完成一项筹备后离岗。\n第一个上岗的帮工决定本轮可招募的岗位。',
 'TUTO_Termina':'先完成招徕、料理、备货、灯饰、护阵的摊位获胜。\n筹备完成时立即结算。',
 'TUTO2_Cont':'继续认识夜市里的牌。','TUTO2_Dev':'帮工：上岗完成筹备。',
 'TUTO2_Neg':'闹场使隔壁停工；挖角作用于在岗帮工。',
 'TUTO2_Pos':'搭把手：推进、完工、开放岗位或减需求。',
 'TUTO2_Esp':'修补解除事件；调停在巡街倒计时内反制。',
 'TUTO2_Juega':'点牌使用；空格或右键交班，Tab 快进。',
 'TUTO2_Buena':'铃声响起，准备开摊！',
 'REASON_-1':'可以使用','REASON_0':'还没轮到你','REASON_1':'五个帮工位已满',
 'REASON_2':'本轮已选定另一个岗位','REASON_3':'隔壁五个闹场位已满',
 'REASON_4':'隔壁没有在岗帮工','REASON_5':'隔壁还没有完成工作','REASON_6':'同号帮工正在隔壁上岗',
 'REASON_7':'没有可推进的帮工','REASON_8':'手里没有帮工牌','REASON_9':'没有匹配事件或反制时机',
 'AFFECT':'受影响：','TUTO_Dept_Anna':'夜市帮工','TUTO_Dept_Abel':'夜市帮工','TUTO_Dept_Rosa':'夜市帮工',
 'TUTO_Dept_Mikis':'夜市帮工','TUTO_Dept_English':'夜市帮工','TUTO_Dept_Marc':'夜市帮工','ExitIn':'即将离开：'
}
EN={
 '':'','Pause':'Options','Resume':'Resume','NextTurn':'Pass Shift','AutoPause':'Pause per Card','FastMode':'Fast Mode',
 'Tutorial':'Tutorial','PlayAgain':'Open Again','DisableAutoPause':'Disable Card Pauses','Continue':'Continue',
 'ClickToUse':'Press any key to use the mediator','Recruiting':'Hiring helpers','SelectingProjects':'Drawing opening orders',
 'YourTurn':'Your stall moves','AITurn':'The rival stall moves','YouWin':'Your market is open!','YouLose':'The rival opened first!',
 'AI':'Rival stall','PLAYER':'Your stall','Player':'You','YOURPROJECT':'Your order','EDIT':'Rename',
 'Design':'Supplies','Programming':'Cooking','Art':'Decor','Testing':'Wards','Sound':'Calling',
 'SOUND DESIGNER':'CALLER','SOUND DESIGNERGirl':'CALLER','PROGRAMMER':'COOK','PROGRAMMERGirl':'COOK',
 'DESIGNER':'SUPPLIER','DESIGNERGirl':'SUPPLIER','ARTIST':'DECORATOR','TESTER':'WARD KEEPER','TESTERGirl':'WARD KEEPER',
 'OUCH':'TROUBLE','INSTA-OUCH':'INTERVENTION','COOL':'A HELPING HAND','SPECIAL':'REPAIR','DIRECTION':'MEDIATOR','HEADQUARTERS':'INSPECTION','PROJECT':'ORDER',
 'Week':'Shift ','MoneySymbol':' copper','NewRecord':'New record','SCORECARDTITLE':'Market Ledger',
 'TUTO_Welcome':'Welcome to the Yokai Night Market!',
 'TUTO_Obj':'Finish all five preparations before the rival stall.',
 'TUTO_Para':'Each stall draws a different opening order.\nThe left number is work done; the right is work needed.',
 'TUTO_Turno':'A shift starts with automatic repairs and helper progress,\nthen draws cards. Play as many cards as you can, then pass.',
 'TUTO_Cada':'Helpers take two or four shifts to finish one task and leave.\nThe first helper hired limits this shift to that department.',
 'TUTO_Termina':'Finish calling, cooking, supplies, decor and wards to win.\nThe result is settled immediately when the order is ready.',
 'TUTO2_Cont':'Meet the market cards.','TUTO2_Dev':'Helpers work on one preparation at your stall.',
 'TUTO2_Neg':'Trouble pauses rival helpers. Interventions target working helpers.',
 'TUTO2_Pos':'Helping hands progress work or simplify preparations.',
 'TUTO2_Esp':'Repairs remove trouble; mediators cancel inspections.',
 'TUTO2_Juega':'Click to play; Space or right-click passes; Tab fast-forwards.',
 'TUTO2_Buena':'Ring the bell and open your stall!',
 'REASON_-1':'Ready to play','REASON_0':'Wait for your shift','REASON_1':'All five helper slots are occupied',
 'REASON_2':'Another department was chosen this shift','REASON_3':'All rival trouble slots are occupied',
 'REASON_4':'The rival has no working helpers','REASON_5':'The rival has no completed tasks',
 'REASON_6':'The same helper is working for the rival','REASON_7':'No unblocked helper can progress',
 'REASON_8':'No helper card in hand','REASON_9':'No matching trouble or response window',
 'AFFECT':'Affects: ','TUTO_Dept_Anna':'Market helper','TUTO_Dept_Abel':'Market helper','TUTO_Dept_Rosa':'Market helper',
 'TUTO_Dept_Mikis':'Market helper','TUTO_Dept_English':'Market helper','TUTO_Dept_Marc':'Market helper','ExitIn':'Leaving in '
}
ROLE=['招客','备餐','备货','挂灯','布符']; ROLE_EN=['guest','food','supply','lantern','ward']
def compact_chinese(value):
    # Source word wrapping is Latin-space based. Explicit line breaks keep CJK
    # inside the unchanged TextMesh region without changing that production logic.
    out=[]
    for paragraph in value.split('\n'):
        while len(paragraph)>20:
            cut=max((i+1 for i,c in enumerate(paragraph[:20]) if c in '，；：、'),default=20)
            if cut<10:cut=20
            out.append(paragraph[:cut]);paragraph=paragraph[cut:]
        out.append(paragraph)
    return '\n'.join(out)

def description(c):
    typ, turns, function, target, amount=c['Type'],c['Turns'],c['Function'],c['Target'],c['Num']
    if typ<5:return (f'干活 {turns} 轮，完成一项{ROLE[typ]}工作。', f'Work for {turns} turns to finish one {ROLE_EN[typ]} task.')
    if typ==6:
        role='所有岗位' if target<0 else ROLE[target]
        role_en='all departments' if target<0 else ROLE_EN[target]
        return (f'隔壁的{role}帮工停工 {turns} 轮。',f'Block rival helpers in {role_en} for {turns} turns.')
    if typ==7:
        return [('选隔壁一名在岗帮工，带到自己的空位。','Take one working rival helper into your empty slot.'),
                ('选隔壁一名在岗帮工，让他回到隔壁手牌。','Return one working rival helper to their hand.'),
                ('选隔壁一名在岗帮工，将这张牌弃掉。','Discard one working rival helper.')][function]
    if typ==8:
        return [('自己所有没被闹场拦住的帮工各干一次活。','Advance every unblocked helper at your stall once.'),
                ('本轮可以招任意岗位的帮工。','Hire helpers from any department this turn.'),
                ('自己所有没被闹场拦住的帮工立即完工。','Finish the current task of every unblocked helper.'),
                ('选自己的一个岗位，少做一项就能开摊。','Choose a department and reduce its requirement by one.')][function]
    if typ==9:
        names='阴雨漏棚、暴雨封街、河雾淹摊或伞海堵门' if c['ID']<85 else '灯线短路或灯芯熄灭'
        english='Remove any of these:\n阴雨漏棚 / 暴雨封街\n河雾淹摊 / 伞海堵门.' if c['ID']<85 else 'Remove either:\n灯线短路 / 灯芯熄灭.'
        return (f'解除这些闹场：{names}。',english)
    if typ==10:return ('收到巡街牌时，在倒计时内使用。巡街无效，两张牌都弃掉。','Use during the inspection countdown. Cancel the inspection and discard both cards.')
    if typ==11:
        if function==0:return (f'隔壁的{ROLE[target]}工作要多做 {amount} 项。',f'Add {amount} tasks to the rival {ROLE_EN[target]} requirement.')
        return ('选隔壁一个做过工作的岗位，扣掉一项已完成工作。','Choose a rival department with completed work; remove one completed task.')
    raise ValueError(typ)

PICTURE_SUBJECTS={
 'Meeting':'a circle of small mischievous yokai crowding a night-market stall, blocking its work while insisting on an unnecessary meeting; no writing',
 'Attack':'a blustery spirit scattering canvas awnings and lightweight baskets between lantern stalls, comic disruption with no violence',
 'Crash':'a night-market paper lantern electrical cord with a visible loose connection, a concerned small light keeper finding the broken fitting',
 'Sick':'an adult working yokai wrapped in a patched blanket, kindly being sent away from a stall to rest with a warm bowl, one helper leaving work',
 'Light Off':'a lantern-lined night-market stall suddenly dark with an extinguished paper lamp, one surprised yokai holding a dark lantern',
 'Hunger':'a night-market helper with an empty bowl and grumbling belly, momentarily distracted from the workbench',
 'All finish':'several friendly adult yokai making the final coordinated touches to a finished market stall, successful completed preparation',
 'Bug':'knotted ward ropes and tangled little protective talismans over a night-market stall, a ward keeper studying the knot; no legible writing',
 'Phone':'a caller yokai finding a small cracked silent copper bell, ears drooping slightly, the stall calling work temporarily interrupted',
 'Holidays':'a supplies yokai gazing wistfully at drifting festive lanterns and leaving a half-organized basket of goods',
 'List':'a friendly market clerk crossing one small item off an unlettered checklist with a brush, simplifying preparations; no writing or numbers',
 'All forward':'adult yokai helpers energetically pushing ahead together at one night-market workbench, visible bowls baskets lanterns and a bell',
 'Resignation':'a frustrated adult yokai helper walking away from a market stall with a rolled cloth bundle, a clear empty space left at the workbench',
 'Door':'a wily fox yokai warmly inviting an adult rival helper through an open fabric stall curtain to join their stall, nonviolent recruitment',
 'All depts':'an adaptable adult yokai wearing a patched apron, setting a bell ladle basket lantern and ward rope on a neat stall workbench',
 'Negotiator':'狸掌柜, an adult tanuki market mediator in a sage waistcoat, confidently raising one open paw between a market inspector and a stall, calm diplomacy'
}
STYLE='Original 2D hand-painted folk-fantasy game art, clean woodcut-like contours, subtle rough paper texture, rounded adult yokai proportions. Honey lantern light from upper left, faint cool teal rim light. Muted indigo #17243B, parchment #F1E4C5, honey #E9BC58, terracotta #D77854, sage #72AF88, gray violet #9A8BC2. Warm industrious mood, large readable shapes, restrained detail.'
AVOID='No licensed characters, original game artwork, office objects, computers, cyberpunk neon, photography, 3D rendering, gore, extra limbs, watermarks, branding, pseudo-transparent checkerboards.'

def make_theme():
    cat=read(SOURCE/'local/content-catalog.json'); old=read(ROOT/'data/catalog.json')
    originals={c['sourceID']:c for c in old['cards']}; contracts={c['sourceID']:c for c in old['contracts']}
    strings={}; cards=[]
    for t in cat['translations']:
        if t['source']=='Texts':
            key=t['id'];assert key in UI and key in EN, key
            strings[key]={'zh':UI[key],'en':EN[key]}
    changes={63:'隔摊借工',64:'铃诱跳槽',65:'送回歇息',66:'热汤请假',67:'闹脾气回家',68:'解散小工'}
    for c in cat['cards']:
        oldcard=originals[c['ID']];cn,en=description(c);cn=compact_chinese(cn);en='\n'.join(textwrap.fill(line,36,break_long_words=False,break_on_hyphens=False) for line in en.split('\n'));name=changes.get(c['ID'],oldcard['name'])
        strings['Name'+str(c['ID'])]={'zh':name,'en':name}
        strings['Quote'+str(c['ID'])]={'zh':oldcard['quote'],'en':{'worker':'Every good stall needs steady hands.','disruption':'A busy market brings surprises.','instant':'A helper can always find another stall.','positive':'Many hands make a brighter night.','repair':'A little care puts everything right.','counter':'Let us settle this before the bell rings.','inspection':'The night market keeps its own accounts.'}.get(oldcard['kind'],'The lanterns will be lit tonight.')}
        strings['Description'+str(c['ID'])]={'zh':cn,'en':en}
        cards.append({'id':c['ID'],'type':c['Type'],'turns':c['Turns'],'target':c['Target'],'function':c['Function'],'amount':c['Num'],'repair':c['Repair'],'picture':c['Picture'],'name':name,'description':cn})
    for p in cat['projects']:
        c=contracts[p['ID']]
        strings['Title'+str(p['ID'])]={'zh':c['name'],'en':c['name']}
        strings['SubTitle'+str(p['ID'])]={'zh':c['subtitle'],'en':'Prepare the stall and greet the creatures of the night.'}
    # Template-only translations present in the original XML are preserved as original night-market text.
    for t in cat['translations']:
        key=t['id']
        if key not in strings:
            if key=='Name0':strings[key]={'zh':'夜市帮工','en':'Market Helper'}
            elif key=='Quote0':strings[key]={'zh':'今晚一起把摊位做好。','en':'Let us make a good stall tonight.'}
            elif key=='Description0':strings[key]={'zh':'帮工将在夜市派上用场。','en':'A helper for the night market.'}
            else:raise ValueError('Unmapped text '+key)
    copy=read(ROOT/'fidelity/data/copy.json')
    assert len(copy['quotes'])==95 and len(copy['projectSubtitles'])==28
    for key,pair in copy['ui'].items():
        assert key in strings and len(pair)==2, key
        strings[key]=dict(zip(('zh','en'),pair))
    for i,pair in enumerate(copy['quotes'],1):strings['Quote'+str(i)]=dict(zip(('zh','en'),pair))
    for i,pair in enumerate(copy['projectSubtitles'],1):strings['SubTitle'+str(i)]=dict(zip(('zh','en'),pair))
    for key,name in copy['names'].items():
        strings['Name'+key]={'zh':name,'en':name};cards[int(key)-1]['name']=name
    theme={'version':2,'copyRevision':copy['date'],'languages':[{'index':0,'name':'简体中文','xmlSlot':'ENG'},{'index':1,'name':'English','xmlSlot':'SPA'}],
           'optionDisplayAlias':{'Sound':{'zh':'声音','en':'Sound'}},
           'strings':strings,'cards':cards,'companies':['铜铃社','月灯坊','百味摊','赤伞堂','狸掌柜铺','井边小馆','纸月社','巷尾杂货'],
           'districts':{'Oui':'铜铃巷','X720':'河灯渡','Doors OS':'雨棚街','EnjoyStation':'纸月坡','Touch OS':'鬼门台'},
           'projects':[{'id':p['ID'],'effectiveGoals':p['effectiveTasks'],'ignoredAnimation':p['ignoredAnimation'],'testing':2} for p in cat['projects']]}
    write(ROOT/'fidelity/data/theme.json',theme)
    for name in ['Texts','TextsCards','TextsProjects']:
        root=ET.Element('Texts')
        for t in cat['translations']:
            if t['source']!=name:continue
            text=ET.SubElement(root,'Text');ET.SubElement(text,'ID').text=t['id']
            ET.SubElement(text,'ENG').text=strings[t['id']]['zh'];ET.SubElement(text,'SPA').text=strings[t['id']]['en']
        ET.indent(root);write(PROJECT/('Assets/Recovery/Media/night-'+name+'.xml'),ET.tostring(root,encoding='unicode'))
    for name,items in [('CardsPlay',cat['cards']),('CardsProject',cat['projects'])]:
        root=ET.Element('Cards')
        for c in items:
            e=ET.SubElement(root,'Card')
            for key,value in c['raw'].items():
                if key=='Platform':value=theme['districts'][value]
                ET.SubElement(e,key).text=value
        ET.indent(root);write(PROJECT/('Assets/Recovery/Media/night-'+name+'.xml'),ET.tostring(root,encoding='unicode'))
    # An existing restored scene loads logical Resources copies. Keep those
    # bytes synchronized on subsequent packaging edits without replacing .meta.
    for name in ['Texts','TextsCards','TextsProjects','CardsPlay','CardsProject']:
        runtime=PROJECT/('Assets/Resources/'+name+'.xml')
        if runtime.exists():copy_if_changed(PROJECT/('Assets/Recovery/Media/night-'+name+'.xml'),runtime)
    manifest=read(PROJECT/'Assets/Editor/RecoverySource/manifest.json')
    palette=read(ROOT/'fidelity/data/presentation.json')['textColorOverrides']
    materials=read(ROOT/'fidelity/data/presentation.json')['materialColorOverrides']
    names=['铃羽','灯芽','纸鸢','符青与符白','铜铃兄弟','门石小队']
    # Six baked tutorial example titles are not dictionary keys or runtime card data.
    example_ids=['mainData:1204','mainData:1239','mainData:1285','mainData:1306','mainData:1304','mainData:1196']
    theme['staticTextOverrides']=dict(zip(example_ids,names))
    write(ROOT/'fidelity/data/theme.json',theme)
    textnames={o['id']:o['name'] for o in manifest['objects'] if o['type']=='TextAsset'}
    font=REPO/'prototypes/proto-013-ring-toss/game/RingTossWorkshop/Assets/RingToss/Resources/Fonts/NotoSansCJKsc-Regular.otf'
    fontfolder=PROJECT/'Assets/Recovery/NightFonts';fontfolder.mkdir(parents=True,exist_ok=True)
    for o in manifest['objects']:
        if o['id'] in materials:
            for pair in o['tree']['m_SavedProperties']['m_Colors']:
                if pair[0]['name']=='_Color':pair[1]={channel:materials[o['id']][channel] for channel in 'rgba'}
        if o['type']=='TextAsset':o['assetPath']='Assets/Recovery/Media/night-'+textnames[o['id']]+'.xml'
        if o['type']=='Font':
            target=fontfolder/(o['id'].replace(':','_').replace(' ','_')+'.otf');copy_if_changed(font,target)
            o['assetPath']=target.relative_to(PROJECT).as_posix()
        if o['type']=='TextMesh':
            original=read(SOURCE/'local/typetrees'/o['container']/(str(o['pathID'])+'_TextMesh.json'))
            # The production TextInfo constructor caches these values as lookup keys.
            # Translate dictionary values, never the serialized initial key.
            o['tree']['m_Text']=theme['staticTextOverrides'].get(o['id'],original['m_Text'])
            if o['id'] in palette:
                color=palette[o['id']]
                o['tree']['m_Color']={'rgba':sum(round(color[channel]*255) << (8*i) for i,channel in enumerate('rgba'))}
    write(PROJECT/'Assets/Editor/RecoverySource/manifest.json',manifest)
    # Keep the exact eight-entry random pool and call count; only its displayed contents change.
    code=PROJECT/'Assets/Recovery/Scripts/ProjectCard.cs';text=code.read_text('utf-8')
    text,count=re.subn(r'm_asCompanyName = new string\[8\] \{[^;]+;', 'm_asCompanyName = new string[8] { '+', '.join(json.dumps(x,ensure_ascii=False) for x in theme['companies'])+' };',text,count=1)
    assert count==1;write(code,text)
    code=PROJECT/'Assets/Recovery/Scripts/Option.cs';text=code.read_text('utf-8')
    original='m_oMain.GetComponent<TextMesh>().text = hLanguage.sGetText(m_sTextID);'
    replacement='m_oMain.GetComponent<TextMesh>().text = m_sTextID == "Sound" ? (hLanguage.iLanguage == 0 ? "声音" : "Sound") : hLanguage.sGetText(m_sTextID);'
    assert original in text or replacement in text
    write(code,text.replace(original,replacement))
    notices=PROJECT/'Assets/StreamingAssets/ThirdPartyNotices';notices.mkdir(parents=True,exist_ok=True)
    for file in (REPO/'prototypes/proto-013-ring-toss/game/RingTossWorkshop/Assets/StreamingAssets/ThirdPartyNotices').glob('Noto*.txt'):
        copy_if_changed(file,notices/file.name)
    specs=read(ROOT/'fidelity/image-specs.json');jobs=[]
    for asset in specs['textures']:
        name=asset['name'];first=next((c for c in cat['cards'] if c['Picture']==name),None)
        if first:
            if name in PICTURE_SUBJECTS:subject=PICTURE_SUBJECTS[name]
            else:
                art=originals[first['ID']]['art'];prompt=(ROOT/'art/prompts'/f'{art}.txt').read_text('utf-8')
                subject=next(line.removeprefix('Subject: ') for line in prompt.splitlines() if line.startswith('Subject: '))
            content='Fully opaque full-bleed landscape illustration, one main scene. Central face and tool safe inside 80 percent. No frame or text. Background: softly suggested timber stalls, paper lanterns and hanging cloth.'
            alpha=False;kind='illustration'
        elif name.startswith('Table_'):
            subject='a top-down dark indigo wooden night-market planning table with subtle carved timber and cloth texture, low contrast and low detail across all play areas; warm lantern suggestion only at the outer edge'
            content='Fully opaque full-bleed flat background; no cards, figures, objects, writing, labels or UI baked in. Preserve uncluttered play areas.';alpha=False;kind='table'
        elif name.startswith('Title_') or name=='SubTitle':
            subject='a flat original lantern-and-moon emblem for the yokai night market, small charming market-roof silhouette in the center and sparse lanterns around it'
            content='Transparent landscape title emblem, centered balanced silhouette with broad negative space. No letters or writing; the exact title is native Unity text.';alpha=True;kind='title'
        elif name in ['Released','Done','Blocked','Cancelled']:
            subject={'Released':'an ornate round warm-gold opening seal with a small open lantern stall motif','Done':'a sage-green completion seal with a simple tied lantern knot motif','Blocked':'a terracotta-red blocked-work seal with a clear crossed rope motif','Cancelled':'a gray-violet closing seal with a folded paper umbrella motif'}[name]
            content='Transparent square stamp asset. Broad clean silhouette, motif inside central 70 percent, no text, no characters, no numbers.';alpha=True;kind='stamp'
        else:
            subject='a flat functional night-market game interface texture for '+name+', muted paper-and-timber styling, simple readable geometry and restrained edge decoration'
            content='A single isolated flat texture with true transparency around its functional silhouette. No surrounding mockup, no labels, no writing, no shadows outside the asset. Text and numbers are native Unity overlays. The exact original dimensions and pivot are applied by the technical export.';alpha=True;kind='interface'
        prompt=f'Use case: stylized-concept\nAsset key: {name}\nTarget runtime dimensions: {asset["width"]} x {asset["height"]} pixels.\nSubject: {subject}\nComposition: {content}\nStyle: {STYLE}\nText: none; do not generate letters or numbers.\nAvoid: {AVOID}\n'
        safe=name.replace(' ','_');promptPath=ROOT/'fidelity/art/prompts'/f'{safe}.txt'
        if not promptPath.exists():write(promptPath,prompt)
        jobs.append({'id':asset['id'],'name':name,'kind':kind,'width':asset['width'],'height':asset['height'],'transparent':alpha,'prompt':promptPath.relative_to(ROOT).as_posix(),
                     'raw':f'fidelity/art/generated/{safe}.raw.png','final':f'local/NightArt/{safe}.png','status':'raw_generated' if (ROOT/'fidelity/art/generated'/f'{safe}.raw.png').exists() else 'specified'})
    write(ROOT/'fidelity/art/jobs.json',{'version':2,'jobs':jobs})
    write(ROOT/'fidelity/data/theme-verification.json',{'cardCount':len(cards),'projectCount':len(theme['projects']),'textCount':len(strings),'allSourceKeysCovered':all(t['id'] in strings for t in cat['translations']),'effectiveTestingPreserved':all(p['testing']==2 for p in theme['projects']),
             'languageSlotsPreserved':2,'companyPoolPreserved':8,'fontSHA256':sha(font),'images':len(jobs)})
    print(json.dumps({'themeCards':len(cards),'projects':len(theme['projects']),'textKeys':len(strings),'imageJobs':len(jobs)},ensure_ascii=False))

if __name__=='__main__':make_theme()
