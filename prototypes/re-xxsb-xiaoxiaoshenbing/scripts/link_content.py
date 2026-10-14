"""Join manifest assets, Unity objects, decoded tables, language hashes and IL systems."""
import collections
import hashlib
import json
from pathlib import Path
import re
import struct
import UnityPy
from common import PROJECT, LOCAL, APK, write


def language_hash(text):
    if not text:
        return 0
    b = text.encode('utf-16-le')
    units = list(struct.unpack('<'+'H'*(len(b)//2), b))
    value = float(len(units))
    for index in range(len(units), 0, -1):
        value = abs(value * 16777611.0 + float(units[index-1])*index) % 4294967296.0
    return int(abs(value*16777611.0+units[-1]) % 4294967296.0)


def main():
    manifest = json.loads((APK/'assets/PackageManifest_DefaultPackage_35156.json').read_text('utf8'))
    objects = json.loads((LOCAL/'assets/objects.json').read_text('utf8'))
    media = json.loads((LOCAL/'media/index.json').read_text('utf8'))
    by_object = {r['id']:r for r in objects}
    exports = {r['object_id']:r for r in media}
    assets = []
    grouped = collections.defaultdict(list)
    for row in manifest['AssetList']:
        grouped[row['BundleID']].append(row)
    failures = []
    for bundle_id, logical in grouped.items():
        bundle = manifest['BundleList'][bundle_id]
        stem, ext = bundle['BundleName'].rsplit('.',1)
        name = f"{stem}_{bundle['FileHash']}.{ext}"
        data = (APK/'assets'/name).read_bytes()
        import zlib
        crc = zlib.crc32(data)&0xffffffff
        assert hashlib.md5(data).hexdigest()==bundle['FileHash'] and len(data)==bundle['FileSize']
        assert bundle['FileCRC'].lower() in [f'{crc:08x}', struct.pack('<I',crc).hex()]
        env=UnityPy.load(str(APK/'assets'/name))
        containers={k.lower():p for k,p in env.container.items()}
        for row in logical:
            pointer=containers.get(row['AssetPath'].lower())
            linked={'address':row['Address'],'asset_path':row['AssetPath'],'bundle_id':bundle_id,
                'bundle':name,'dependencies':row['DependIDs']}
            if pointer is None:
                linked['status']='no-container-entry';failures.append(linked.copy())
            else:
                reader=pointer.deref()
                identifier=f"b{bundle_id:04}/{reader.assets_file.name}/{reader.path_id}"
                assert identifier in by_object
                linked.update(status='ok',object_id=identifier,path_id=str(reader.path_id),type=reader.type.name,
                    name=by_object[identifier]['name'],tree=by_object[identifier].get('tree'),media=exports.get(identifier,{}).get('files',[]))
            assets.append(linked)
    write(LOCAL/'content/assets-linked.json',assets)
    language=json.loads((LOCAL/'content/language-map.json').read_text('utf8'))
    language_links=[];display=[]
    for path in sorted((LOCAL/'content/tables').glob('*.json')):
        table=json.loads(path.read_text('utf8'))
        if table['kind']!='Rows':continue
        for row_index,row in enumerate(table['rows']):
            display_row={**row};texts={}
            for key,value in row.items():
                if isinstance(value,str):
                    hashed=str(int(value)) if value.lstrip('-').isdigit() else str(language_hash(value))
                    if hashed in language:
                        text=language[hashed]
                        language_links.append({'table':table['name'],'row':row_index,'column':key,'key':value,'hash':hashed,'text':text})
                        if key in ['name','name_cn','desc','describe','long_desc','short_desc','text_key','unlock_desc']:
                            texts[key]=text
            if texts:display_row['_localized']=texts
            display.append({'table':table['name'],'index':row_index,'value':display_row})
    write(LOCAL/'content/language-links.json',language_links)
    write(LOCAL/'content/display-rows.json',display)
    assemblies=json.loads((LOCAL/'code/hotfix-index.json').read_text('utf8'))
    systems=collections.defaultdict(lambda:{'types':0,'methods':0,'IL_methods':0,'calls':0,'fields':0})
    calls=0
    for record in assemblies:
        index=json.loads((LOCAL/record['index']).read_text('utf8'))
        for cls in index['types']:
            if record['kind']=='hotfix DLL':
                if cls['name'].startswith(('GamePlay.','GameCore.','Launcher.')):
                    prefix='.'.join(cls['name'].split('.')[:2])
                elif cls['name'].startswith('Proto.'):
                    prefix='Proto (generated protocol)'
                elif cls['name'].startswith('ConfigDefine.'):
                    prefix='ConfigDefine (generated schema)'
                else:
                    prefix='Compiler/generated or '+record['assembly']
                item=systems[prefix];item['types']+=1;item['fields']+=len(cls['fields']);item['methods']+=len(cls['methods'])
                item['IL_methods']+=sum(m['rva']!=0 for m in cls['methods']);item['calls']+=sum(len(m['calls']) for m in cls['methods'])
            calls+=sum(len(m['calls']) for m in cls['methods'])
    write(LOCAL/'code/system-map.json',dict(sorted(systems.items())))
    # Preserve the single unsupported serialized object as an explicit standalone raw artifact.
    for row in objects:
        if row['tree_status']=='ok':continue
        src=next(s for s in json.loads((LOCAL/'assets/input-containers.json').read_text('utf8')) if s['source_id']==row['source_id'])
        env=UnityPy.load(str(LOCAL/src['path']))
        reader=next(o for o in env.objects if o.path_id==row['path_id'] and o.assets_file.name==row['file'])
        raw=reader.get_raw_data();out=LOCAL/'assets/unparsed'/row['source_id']/(str(row['path_id'])+'.bin')
        out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(raw)
        row.update(raw=out.relative_to(LOCAL).as_posix(),raw_sha256=hashlib.sha256(raw).hexdigest())
    write(LOCAL/'assets/objects.json',objects)
    summary={'manifest_assets':len(assets),'manifest_assets_linked':sum(x['status']=='ok' for x in assets),
        'manifest_bundle_MD5_size_CRC_all_pass':True,'link_failures':failures,'localized_links':len(language_links),
        'language_hash_source':'GameCore.Lang.LangMgr.StringHash: UTF16 code units, IEEE double operations, modulo 2^32',
        'managed_IL_call_sites':calls,'business_system_groups':len(systems),
        'business_systems':dict(sorted(systems.items(),key=lambda kv:kv[1]['types'],reverse=True)),
        'unparsed_objects_retained':[{'id':x['id'],'raw':x.get('raw'),'sha256':x.get('raw_sha256'),'error':x.get('error')} for x in objects if x['tree_status']!='ok']}
    write(PROJECT/'evidence/content-linking.json',summary)
    print(json.dumps({k:v for k,v in summary.items() if k not in ['business_systems','link_failures','unparsed_objects_retained']},ensure_ascii=True))


if __name__=='__main__':main()
