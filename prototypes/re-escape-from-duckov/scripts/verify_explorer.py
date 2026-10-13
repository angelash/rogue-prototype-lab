"""Verify localhost API navigation, stable keys, filters and media confinement."""
import argparse,json,urllib.error,urllib.parse,urllib.request
from pathlib import Path
from prepare import write

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);ap.add_argument('--port',type=int,default=8784);a=ap.parse_args();p=Path(a.project)
    base=f'http://127.0.0.1:{a.port}';checks=[]
    def api(route,**kw):
        with urllib.request.urlopen(base+'/api/'+route+'?'+urllib.parse.urlencode(kw),timeout=60) as r:return json.load(r)
    def check(name,ok,detail):checks.append(dict(name=name,passed=bool(ok),details=detail))
    catalog=json.loads((p/'local/content-catalog.json').read_text('utf-8'))
    for cat,data in catalog.items():
        result=api('list',category=cat);count=len(data) if isinstance(data,list) else 1
        check(cat+'-count',result['total']==count,dict(expected=count,actual=result['total']))
        for row in result['rows'][:2]+result['rows'][-1:]:
            got=api('detail',category=cat,id=row['key'])
            check(cat+'-detail-'+row['key'],got is not None and (got.get('objectID')==row.get('objectID')) and (got.get('id')==row.get('id')),dict(key=row['key']))
    items=api('list',category='items',page=1);check('pagination',len(items['rows'])==50 and items['rows'][0]['id']!=catalog['items'][0]['id'],dict(page=items['page']))
    result=api('list',category='code',q='Health');row=next(r for r in result['rows'] if r['name']=='Health' and r['assembly']=='TeamSoda.Duckov.Core');got=api('detail',category='code',assembly=row['assembly'],id=row['name'])
    check('code-health-methods',any(m['name']=='Hurt' for m in got['methods']),dict(type=got['name'],methods=len(got['methods'])))
    with urllib.request.urlopen(base+'/source/'+urllib.parse.quote(got['sourceFiles'][0]),timeout=10) as response:
        body=response.read();ctype=response.headers.get('Content-Type','')
    check('read-only-csharp-body',b'class Health' in body and ctype.startswith('text/plain'),dict(bytes=len(body),type=ctype))
    result=api('list',category='objects',script='Duckov.Utilities.LootBoxLoader',type='MonoBehaviour');check('object-script-filter',result['total']==5653,dict(total=result['total']))
    got=api('detail',category='objects',id='resources.assets:5429');media=got['exportFiles'][0]
    with urllib.request.urlopen(base+'/media/'+urllib.parse.quote(media['path']),timeout=10) as response:body=response.read()
    check('sprite-media',body.startswith(b'\x89PNG') and len(body)==media['bytes'],dict(bytes=len(body)))
    for route in ['/media/../content-catalog.json','/media/%2e%2e%2fcontent-catalog.json','/source/../content-catalog.json']:
        try:urllib.request.urlopen(base+route);ok=False
        except urllib.error.HTTPError as ex:ok=ex.code==400
        check('media-confined-'+route,ok,dict(expectedHTTP=400))
    for cat,count in [('audio',373),('samples',568),('code',29961),('objects',1601352)]:
        result=api('list',category=cat);check(cat+'-total',result['total']==count,dict(total=result['total']))
    record=dict(date='2026-10-13',apiPassed=all(x['passed'] for x in checks),checks=checks,browserChecks=['Chinese item list and item configuration','Item icon -> Sprite -> PNG visible','QuestRelation graph details','Health code search and methods','Recipe registry detail','Pagination and audio control'],browserVerified=False)
    write(p/'evidence/explorer-qa.json',record)
    print(f'API checks: {len(checks)}, passed={record["apiPassed"]}',flush=True)
    if not record['apiPassed']:raise SystemExit(1)

if __name__=='__main__':main()
