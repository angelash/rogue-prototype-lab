"""Resolve authoritative registries and all additional content families from static evidence."""
import argparse,collections,csv,json,re,sqlite3,zlib
from pathlib import Path
from prepare import write

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);a=ap.parse_args()
    p=Path(a.project).resolve();local=p/'local';db=sqlite3.connect(local/'assets.sqlite')
    exts={r[0]:json.loads(r[1]) for r in db.execute('SELECT name,externals FROM containers')}
    def ref(container,ptr):
        if not ptr or not ptr.get('m_PathID'):return None
        fileid=ptr['m_FileID'];name=container if not fileid else Path(exts[container][fileid-1]['path'].replace('\\','/')).name
        return f'{name}:{ptr["m_PathID"]}'
    def object_at(oid):
        row=db.execute('SELECT id,name,script,type,status,tree,container FROM objects WHERE id=?',(oid,)).fetchone()
        if not row:return None
        return dict(objectID=row[0],assetName=row[1],script=row[2],type=row[3],status=row[4],data=json.loads(zlib.decompress(row[5])) if row[5] else None,container=row[6])
    def records(script):
        return [object_at(r[0]) for r in db.execute('SELECT id FROM objects WHERE type="MonoBehaviour" AND script=?',(script,)).fetchall()]
    def one(script):
        rows=records(script)
        if len(rows)!=1:raise ValueError(f'Expected one {script}, got {len(rows)}')
        if rows[0]['status']!='ok':raise ValueError(f'{script} incomplete: {rows[0]["status"]}')
        return rows[0]
    # Full text remains local. Public evidence contains only coverage and keys/counts.
    languages={};localization_stats=[]
    for f in sorted((local/'build-copy/Duckov_Data/StreamingAssets/Localization').glob('*.csv')):
        with f.open('r',encoding='utf-8-sig',newline='') as stream:rows=list(csv.DictReader(stream))
        keys=[r['key'] for r in rows];languages[f.stem]={r['key']:r['value'] for r in rows}
        localization_stats.append(dict(language=f.stem,rows=len(rows),keys=len(set(keys)),empty=sum(not r['value'] for r in rows),
            duplicates=[k for k,n in collections.Counter(keys).items() if n>1],sheets=dict(collections.Counter(r['sheet'] for r in rows))))
    zh=languages['ChineseSimplified'];base_keys=set(zh)
    for s in localization_stats:
        s['missingVsSimplified']=len(base_keys-set(languages[s['language']]))
        s['extraVsSimplified']=len(set(languages[s['language']])-base_keys)
    write(local/'localizations.json',languages)
    catalog={};summaries={};registries={}
    def add(category,rows):
        catalog[category]=rows;summaries[category]=len(rows)
    def enrich(rec,namekey=None,idkey='id'):
        data=rec['data'] or {};ptr=data.get('m_GameObject');go=object_at(ref(rec['container'],ptr)) if ptr else None
        return dict(**rec,id=data.get(idkey),name=zh.get(data.get(namekey,''),data.get(namekey,'')) if namekey else (rec['assetName'] or (go['assetName'] if go else rec['script'])),
                    description=zh.get(data.get('description',''),data.get('description','')))
    registry=one('ItemStatsSystem.ItemAssetsCollection');registries['items']=registry['objectID'];items=[]
    for entry in registry['data']['entries']:
        meta=entry['metaData'];oid=ref(registry['container'],entry['prefab']);item=object_at(oid)
        icon=ref(registry['container'],meta['icon'])
        parts=[]
        if item:
            go=ref(item['container'],item['data'].get('m_GameObject'))
            parts=[dict(objectID=r[0],type=r[1],script=r[2]) for r in db.execute('SELECT o.id,o.type,o.script FROM edges e JOIN objects o ON o.id=e.dst WHERE e.src=? AND e.field LIKE ".m_Component%"',(go,))]
        items.append(dict(id=entry['typeID'],name=zh.get(meta['displayName'],meta['displayName']),description=zh.get(meta['description'],meta['description']),
            objectID=oid,iconObject=icon,metadata=meta,data=item['data'] if item else None,components=parts,
            categoryScripts=[r['script'] for r in parts if r['script']]))
    add('items',items)
    registry=one('Duckov.Quests.QuestCollection');registries['quests']=registry['objectID'];quests=[]
    for ptr in registry['data']['list']:
        q=object_at(ref(registry['container'],ptr))
        if not q:raise ValueError('Unresolved registered quest')
        rec=enrich(q,'displayName')
        rec['tasks']=[object_at(ref(q['container'],r)) for r in q['data']['tasks']]
        rec['rewards']=[object_at(ref(q['container'],r)) for r in q['data']['rewards']]
        quests.append(rec)
    add('quests',quests)
    for category,script,field,idkey in [('recipes','CraftingFormulaCollection','list','id'),
        ('buildings','Duckov.Buildings.BuildingDataCollection','infos','id'),
        ('merchants','StockShopDatabase','merchantProfiles','merchantID'),
        ('crops','Duckov.Crops.CropDatabase','entries','id'),('sceneInfos','SceneInfoCollection','entries','id')]:
        registry=one(script);registries[category]=registry['objectID'];rows=[]
        for entry in registry['data'][field]:
            namekey=entry.get('displayName',entry.get('nameKey',''))
            rows.append(dict(id=entry.get(idkey,entry.get('id')),name=zh.get(namekey,namekey) or str(entry.get(idkey,entry.get('id',''))),
                objectID=registry['objectID'],data=entry))
        add(category,rows)
    for category,script,namekey,idkey in [
        ('enemyPresets','CharacterRandomPreset','nameKey','m_Name'),
        ('buffs','Duckov.Buffs.Buff','displayName','id'),
        ('perkTrees','Duckov.PerkTrees.PerkTree',None,'perkTreeID'),
        ('perks','Duckov.PerkTrees.Perk','displayName','id'),
        ('rules','Duckov.Rules.RulesetFile',None,'m_Name'),
        ('levelConfigs','LevelConfig',None,'m_Name'),
        ('lootLoaders','Duckov.Utilities.LootBoxLoader',None,'m_Name'),
        ('lootSpawners','Duckov.Utilities.LootSpawner',None,'m_Name')]:
        add(category,[enrich(r,namekey,idkey) for r in records(script)])
    add('bossPresets',[r for r in catalog['enemyPresets'] if r['data'].get('isBoss')])
    gameplay=one('Duckov.Utilities.GameplayDataSettings');registries['gameplay']=gameplay['objectID'];catalog['gameplay']=gameplay
    # Data-driven family list includes further fishing, pets, minigames, unlocks,
    # achievements, body/face, map/teleport and economy configuration objects.
    families=db.execute('SELECT script,count(*) FROM objects WHERE type="MonoBehaviour" GROUP BY script ORDER BY script').fetchall()
    add('families',[dict(id=s,name=s or '(unresolved script)',count=n) for s,n in families])
    graph_records=[]
    for script,count in families:
        if not script or not any(x in script for x in ('Graph','DialogueTree','BehaviourTree')):continue
        for row in records(script):
            if '_serializedGraph' in (row['data'] or {}):
                rec=enrich(row);graph=row['data']['_serializedGraph']
                try:rec['graph']=json.loads(graph)
                except (ValueError,TypeError):rec['graph']=graph
                graph_records.append(rec)
    add('graphs',graph_records)
    build=object_at(db.execute('SELECT id FROM objects WHERE type="BuildSettings"').fetchone()[0])
    scenes=[dict(id=i,name=Path(path).stem,path=path,objectContainer=f'level{i}',objects=db.execute('SELECT count(*) FROM objects WHERE container=?',(f'level{i}',)).fetchone()[0]) for i,path in enumerate(build['data']['scenes'])]
    add('scenes',scenes)
    all_ids={r['id'] for r in items};issues=[]
    for category in ('items','quests','recipes','buildings','merchants','sceneInfos'):
        ids=[r['id'] for r in catalog[category]]
        duplicates=[i for i,n in collections.Counter(ids).items() if n>1]
        if duplicates:issues.append(dict(category=category,kind='duplicate-id',ids=duplicates))
    for r in catalog['recipes']:
        data=r['data'];refs=[data['result']['id']]+[x['id'] for x in data.get('cost',{}).get('items',[])]
        missing=[x for x in refs if x not in all_ids]
        if missing:issues.append(dict(category='recipes',id=r['id'],kind='unknown-item-id',ids=missing))
    task_types=collections.Counter(t['script'] for q in quests for t in q['tasks'] if t)
    reward_types=collections.Counter(t['script'] for q in quests for t in q['rewards'] if t)
    write(local/'content-catalog.json',catalog)
    write(p/'evidence/content-summary.json',dict(categories=summaries,registries=registries,localization=localization_stats,
        taskTypes=dict(task_types),rewardTypes=dict(reward_types),issues=issues,
        itemPrefabNull=sum(not x['objectID'] for x in items),questTasks=sum(len(q['tasks']) for q in quests),questRewards=sum(len(q['rewards']) for q in quests),
        questValidRewards=sum(bool(r) for q in quests for r in q['rewards']),
        nullQuestSlots=[dict(quest=q['id'],objectID=q['objectID'],kind=k,index=i) for q in quests for k in ('tasks','rewards') for i,r in enumerate(q[k]) if r is None],
        note='Registry entries and static objects; no claim of runtime reachability.'))
    # Full symbol/IL call index remains local; this is a metadata-only code map.
    public=json.loads((p/'evidence/game-code-index.json').read_text())
    path=p/'reports/code-map.md';path.parent.mkdir(parents=True,exist_ok=True)
    with path.open('w',encoding='utf-8') as f:
        f.write('# 游戏程序集完整类型地图\n\n每项包含原始类型符号、继承、方法/字段数量与 metadata token。编译器生成的嵌套类型包含在分母中；全部方法/调用/字符串在本机 code-metadata 中。\n\n')
        for assembly in sorted({r['assembly'] for r in public}):
            f.write(f'## {assembly}\n\n')
            for r in (r for r in public if r['assembly']==assembly):
                f.write(f'- `{r["name"]}` ← `{r["baseType"]}`；方法 {r["methods"]}，字段 {r["fields"]}，token `{r["token"]}`。\n')
    print(json.dumps(summaries,ensure_ascii=False),flush=True)

if __name__=='__main__':main()
