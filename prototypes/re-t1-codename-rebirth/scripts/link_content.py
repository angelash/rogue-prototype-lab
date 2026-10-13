"""Resolve local language keys using the actual LangMgr double arithmetic.

Language matches and same-ID joins are recorded as candidates unless the
caller semantics were separately read. No text is guessed or rewritten.
"""
import collections, hashlib, json, math, sqlite3, struct
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1];LOCAL=ROOT/'local'

def language_hash(s):
    units=struct.unpack('<'+'H'*(len(s.encode('utf-16le'))//2),s.encode('utf-16le'));n=len(units)
    if not n:return 0
    value=float(n)
    for i in range(n,0,-1):value=abs(value*16777611.0+float(units[i-1])*i)%4294967296.0
    value=abs(value*16777611.0+units[-1])%4294967296.0
    return int(value)

def main():
    tables={f.stem:json.loads(f.read_text('utf8')) for f in (LOCAL/'config/tables').glob('*.json')}
    languages={str(x['id']):x['content'] for x in tables['language']['rows']}
    def label(s):
        if not isinstance(s,str):return None
        key=str(int(s)) if s.lstrip('-').isdigit() else str(language_hash(s))
        return {'key':s,'languageID':key,'text':languages[key]} if key in languages else None
    matches=[];by_table=collections.Counter()
    for table,j in tables.items():
        if table=='language' or j['kind']!='Row':continue
        for row in j['rows']:
            for field,v in row.items():
                l=label(v)
                if l:matches.append({'table':table,'rowID':row.get('id'),'field':field,**l});by_table[table]+=1
    items={x['id']:x for x in tables['tps_item_common']['rows']}
    content={'languageKeyMatches':matches,'maps':[{'id':x['id'],'sceneID':x['scene_id'],'type':x['dungeon_type'],'label':label(x['map_name']),'config':x} for x in tables['tps_map']['rows']],'heroes':[{'id':x['id'],'modelID':x['model'],'name':label(x['hero_name']),'codeName':label(x['name']),'config':x} for x in tables['hero']['rows']],'weapons':[{'id':x['id'],'itemExistsAtSameID':x['id'] in items,'name':label(items.get(x['id'],{}).get('item_name')),'config':x} for x in tables['tps_weapon']['rows']]}
    (LOCAL/'config/resolved-content.json').write_text(json.dumps(content,ensure_ascii=False,separators=(',',':')),'utf8')
    with sqlite3.connect(LOCAL/'deep-catalog.sqlite3') as db:
        db.executescript('DROP TABLE IF EXISTS config_labels; CREATE TABLE config_labels(tab TEXT,id TEXT,field TEXT,key TEXT,languageid TEXT,content TEXT); CREATE INDEX labelrow ON config_labels(tab,id);')
        db.executemany('INSERT INTO config_labels VALUES (?,?,?,?,?,?)',[(x['table'],str(x['rowID']),x['field'],x['key'],x['languageID'],x['text']) for x in matches])
    # SpriteConfigData is the authoritative name->atlas/sprite mapping.
    icons=next((x for x in (LOCAL/'assets/text').rglob('*icon_conf*') if x.is_file()),None)
    icon_count=0
    if icons:
        icon_data=json.loads(icons.read_bytes());icon_count=len(icon_data);assert isinstance(icon_data,dict)
        (LOCAL/'assets/icon-config.json').write_text(json.dumps(icon_data,ensure_ascii=False,separators=(',',':')),'utf8')
    code_tables={f.stem[5:] for f in (LOCAL/'source/all/release/hotfix/ConfigDefine/ConfigDefine').glob('data_*.cs') if not f.stem.endswith('Binder')}
    evidence={'languageRows':len(languages),'hashedConfigStringMatches':len(matches),'tablesWithMatches':len(by_table),'perTable':dict(by_table.most_common()),'mapLabelsResolved':sum(x['label'] is not None for x in content['maps']),'heroNamesResolved':sum(x['name'] is not None for x in content['heroes']),'weaponItemNamesResolved':sum(x['name'] is not None for x in content['weapons']),'iconConfigEntries':icon_count,'matchingSemantics':'Actual LangMgr hash + exact language lookup; arbitrary field matches are candidates, not proof of runtime visibility','configOnlyFields':{'task_action':['format']},'codeDataTypesWithoutQCFGTable':sorted(code_tables-tables.keys()),'qcfgTablesWithoutDataClass':sorted(tables.keys()-code_tables)}
    (ROOT/'evidence/content-linking.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),'utf8')
    print(json.dumps({k:v for k,v in evidence.items() if k!='perTable'},ensure_ascii=False));print('Map labels: '+json.dumps([(x['id'],x['label']['text'] if x['label'] else None) for x in content['maps']],ensure_ascii=False))

if __name__=='__main__':main()
