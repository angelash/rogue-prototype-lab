"""Index every decoded Unity object and export lossless TextAsset payloads.

Large numeric arrays are summarized in semantic JSON; decoded bundles retain
their complete serialized bytes. Every tree failure is explicitly recorded.
"""
import collections, gzip, hashlib, json, re, time
from pathlib import Path
import UnityPy

ROOT=Path(__file__).resolve().parents[1]

def compact(value):
    if isinstance(value,(bytes,bytearray)):
        return dict(byteCount=len(value),SHA256=hashlib.sha256(value).hexdigest())
    if isinstance(value,dict):return {str(k):compact(v) for k,v in value.items()}
    if isinstance(value,(list,tuple)):
        if len(value)>256 and all(isinstance(v,(int,float)) for v in value):
            return dict(elementCount=len(value),sample=value[:16],denseArraySummarized=True)
        return [compact(v) for v in value]
    return value

def safe_path(path):
    parts=path.replace('\\','/').split('/')
    assert not any(x in ('..','') for x in parts)
    return Path(*[re.sub(r'[<>:"|?*\x00-\x1f]','_',x) for x in parts])

def main():
    start=time.monotonic(); entries=json.loads((ROOT/'local/decoded/bundle-index.json').read_text('utf8'))
    out=ROOT/'local/assets';(out/'trees').mkdir(parents=True,exist_ok=True)
    rows=[];errors=[];containers=[];texts=[];types=collections.Counter()
    for b in entries:
        env=UnityPy.load(str(ROOT/'local/decoded/bundles'/b['filename']))
        named={}
        for address,ptr in env.container.items():named.setdefault(ptr.path_id,[]).append(address)
        with gzip.open(out/'trees'/f"{b['id']:04}.jsonl.gz",'wt',encoding='utf8',errors='backslashreplace') as writer:
            for file in env.assets:
                containers.append(dict(bundleID=b['id'],file=file.name,unityVersion=file.unity_version,objects=len(file.objects),externals=[x.path for x in file.externals]))
                for obj in file.objects.values():
                    row=dict(bundleID=b['id'],file=file.name,pathID=obj.path_id,type=obj.type.name,bytes=obj.byte_size,addresses=named.get(obj.path_id,[]),name='',treeStatus='pending')
                    types[row['type']]+=1
                    try:
                        tree=obj.read_typetree();row['name']=tree.get('m_Name','');row['treeStatus']='ok'
                        if row['type']=='MonoScript':row.update({k:tree.get(k) for k in ['m_ClassName','m_Namespace','m_AssemblyName']})
                        if row['type']=='MonoBehaviour':row['script']=tree.get('m_Script')
                        if row['type']=='TextAsset':
                            asset=obj.read();raw=asset.m_Script.encode('utf8','surrogateescape')
                            dest=(out/'text'/safe_path(row['addresses'][0])) if row['addresses'] else out/'text-unaddressed'/str(b['id'])/(str(obj.path_id)+'_'+re.sub(r'[^\w.-]','_',row['name']))
                            dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(raw)
                            row['payload']=dest.relative_to(ROOT/'local').as_posix();row['payloadBytes']=len(raw);row['payloadSHA256']=hashlib.sha256(raw).hexdigest();row['payloadHeader']=raw[:16].hex();texts.append(row.copy())
                            tree=dict(tree);tree['m_Script']=dict(byteCount=len(raw),SHA256=row['payloadSHA256'],losslessFile=row['payload'])
                        writer.write(json.dumps(dict(identity=row,tree=compact(tree)),ensure_ascii=False,separators=(',',':'))+'\n')
                    except Exception as ex:
                        row['treeStatus']='failed';row['error']=type(ex).__name__+': '+str(ex);errors.append(row.copy())
                    rows.append(row)
        if len(containers)%50==0:print(f'Containers {len(containers)}, objects {len(rows)}, errors {len(errors)}',flush=True)
    for name,data in [('objects',rows),('errors',errors),('containers',containers),('text-assets',texts)]:
        (out/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,separators=(',',':'))+'\n','utf8',errors='backslashreplace')
    evidence=dict(bundles=len(entries),containers=len(containers),objects=len(rows),types=dict(types.most_common()),treesPassed=sum(x['treeStatus']=='ok' for x in rows),treesFailed=len(errors),textAssetsExported=len(texts),textAssetBytes=sum(x['payloadBytes'] for x in texts),seconds=round(time.monotonic()-start,2),denseArrays='Semantic JSON summaries only; full original values retained in decoded bundles',APKExecuted=False)
    (ROOT/'evidence/bundle-objects.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n','utf8')
    print(json.dumps(evidence,ensure_ascii=False))

if __name__=='__main__':main()
