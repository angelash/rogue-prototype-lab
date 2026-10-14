"""Build a complete local read-only search database for code/configuration/assets."""
import collections
import json
from pathlib import Path
import sqlite3
from concurrent.futures import ThreadPoolExecutor
from common import PROJECT, LOCAL, write


def main():
    output=LOCAL/'catalog.sqlite3'
    db=sqlite3.connect(output)
    db.executescript('PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF; PRAGMA temp_store=MEMORY; PRAGMA cache_size=-65536;')
    db.executescript('''
    DROP TABLE IF EXISTS source; DROP TABLE IF EXISTS source_fts;
    DROP TABLE IF EXISTS method; DROP TABLE IF EXISTS method_fts;
    DROP TABLE IF EXISTS config_table; DROP TABLE IF EXISTS config_row;
    DROP TABLE IF EXISTS asset; DROP TABLE IF EXISTS payload;
    CREATE TABLE source(id INTEGER PRIMARY KEY,path TEXT,kind TEXT,assembly TEXT,text TEXT);
    CREATE VIRTUAL TABLE source_fts USING fts5(path,text,content='source',content_rowid='id');
    CREATE TABLE method(id INTEGER PRIMARY KEY,assembly TEXT,type TEXT,name TEXT,token TEXT,kind TEXT,has_il INTEGER,meta TEXT);
    CREATE VIRTUAL TABLE method_fts USING fts5(assembly,type,name,content='method',content_rowid='id');
    CREATE TABLE config_table(name TEXT PRIMARY KEY,kind TEXT,row_count INTEGER,meta TEXT);
    CREATE TABLE config_row(table_name TEXT,idx INTEGER,key TEXT,text TEXT,PRIMARY KEY(table_name,idx));
    CREATE TABLE asset(id TEXT PRIMARY KEY,name TEXT,type TEXT,source TEXT,path_id TEXT,address TEXT,meta TEXT,media TEXT);
    CREATE INDEX asset_name ON asset(name); CREATE INDEX asset_type ON asset(type);
    CREATE TABLE payload(id INTEGER PRIMARY KEY,name TEXT,kind TEXT,path TEXT,meta TEXT);
    ''')
    counts=collections.Counter()
    for base,kind,suffix in [(LOCAL/'code/decompiled','C# reconstructed','.cs'),(LOCAL/'android/java','Java reconstructed','.java'),(LOCAL/'android/java-embedded','Java embedded recovered','.java'),(LOCAL/'android/fallback','DEX fallback','.java')]:
        paths=sorted(base.rglob('*'+suffix))
        def read_source(path):return path,path.read_text('utf8',errors='replace')
        with ThreadPoolExecutor(max_workers=8) as workers:
            for path,text in workers.map(read_source,paths):
                relative=path.relative_to(LOCAL).as_posix();assembly=path.relative_to(base).parts[0]
                db.execute('INSERT INTO source(path,kind,assembly,text) VALUES(?,?,?,?)',(relative,kind,assembly,text));counts[kind]+=1
                if counts[kind]%2000==0:print(kind,counts[kind],'files indexed',flush=True)
        print(kind,'complete',counts[kind],flush=True)
    db.execute("INSERT INTO source_fts(source_fts) VALUES('rebuild')")
    print('Source full-text index complete',flush=True)
    indexes=json.loads((LOCAL/'code/hotfix-index.json').read_text('utf8'))
    for item in indexes:
        if item['status']!='ok':continue
        index=json.loads((LOCAL/item['index']).read_text('utf8'))
        for cls in index['types']:
            for method in cls['methods']:
                db.execute('INSERT INTO method(assembly,type,name,token,kind,has_il,meta) VALUES(?,?,?,?,?,?,?)',
                    (index['assembly'],cls['name'],method['name'],method['token'],item['kind'],int(method['rva']!=0),json.dumps(method,ensure_ascii=True)))
                counts['managed_methods']+=1
        print('Managed methods indexed',index['assembly'],flush=True)
    native=json.loads((LOCAL/'il2cpp/arm64/script.json').read_text('utf8'))['ScriptMethod']
    for method in native:
        cls,sep,name=method['Name'].partition('$$')
        db.execute('INSERT INTO method(assembly,type,name,token,kind,has_il,meta) VALUES(?,?,?,?,?,?,?)',
            ('IL2CPP ARM64',cls,name if sep else cls,hex(method['Address']),'native address',0,json.dumps(method,ensure_ascii=True)))
    counts['native_methods']=len(native)
    db.execute("INSERT INTO method_fts(method_fts) VALUES('rebuild')")
    display=json.loads((LOCAL/'content/display-rows.json').read_text('utf8'))
    by_display={(x['table'],x['index']):x['value'] for x in display}
    for path in sorted((LOCAL/'content/tables').glob('*.json')):
        table=json.loads(path.read_text('utf8'));metadata={k:v for k,v in table.items() if k!='rows'}
        db.execute('INSERT INTO config_table VALUES(?,?,?,?)',(table['name'],table['kind'],table['row_count'],json.dumps(metadata,ensure_ascii=True)))
        if table['kind']=='KV':records=[{'key':k,'value':v} for k,v in table['rows'].items()]
        else:records=table['rows']
        for i,row in enumerate(records):
            value=by_display.get((table['name'],i),row)
            key=str(row.get(table.get('key_column') or 'key',i))
            db.execute('INSERT INTO config_row VALUES(?,?,?,?)',(table['name'],i,key,json.dumps(value,ensure_ascii=False)))
            counts['configuration_records']+=1
    assets=json.loads((LOCAL/'assets/objects.json').read_text('utf8'))
    logical=json.loads((LOCAL/'content/assets-linked.json').read_text('utf8'))
    addresses=collections.defaultdict(list)
    for asset in logical:
        if asset['status']=='ok':addresses[asset['object_id']].append(asset['address'])
    media={r['object_id']:r for r in json.loads((LOCAL/'media/index.json').read_text('utf8'))}
    for asset in assets:
        meta={**asset,'path_id':str(asset['path_id'])}
        db.execute('INSERT INTO asset VALUES(?,?,?,?,?,?,?,?)',(asset['id'],asset['name'],asset['type'],asset['source_id'],str(asset['path_id']),
            ' '.join(addresses[asset['id']]),json.dumps(meta,ensure_ascii=True),json.dumps(media.get(asset['id'],{}),ensure_ascii=True)))
    payloads=json.loads((LOCAL/'content/payload-index.json').read_text('utf8'))
    for payload in payloads:
        db.execute('INSERT INTO payload(name,kind,path,meta) VALUES(?,?,?,?)',(payload['name'],payload['kind'],payload['payload'],json.dumps(payload,ensure_ascii=True)))
    counts.update(assets=len(assets),configuration_tables=len(list((LOCAL/'content/tables').glob('*.json'))),payloads=len(payloads))
    db.commit();integrity=db.execute('PRAGMA integrity_check').fetchone()[0];db.close();assert integrity=='ok'
    summary={'database':'local/catalog.sqlite3','bytes':output.stat().st_size,'counts':dict(counts),'sqlite_integrity':integrity,
        'scope':'Every reconstructed source file, managed method/native address, decoded table row, Unity object and payload. Large path IDs are stored as strings; original bytes remain separate.'}
    write(PROJECT/'evidence/catalog.json',summary);print(json.dumps(summary,ensure_ascii=True))


if __name__=='__main__':main()
