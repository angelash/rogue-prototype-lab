"""Check read-only catalog routes against recovered file/config identities."""
import hashlib
import json
import sqlite3
from urllib.parse import urlencode
from urllib.request import urlopen
from urllib.error import HTTPError
from common import PROJECT,LOCAL,write


def main():
    base='http://127.0.0.1:8775'
    def get(route,**params):
        with urlopen(base+'/api/'+route+'?'+urlencode(params),timeout=30) as response:return json.load(response)
    checks={}
    sources=get('sources',q='DamageCalculator.cs');checks['source_search']=len(sources)==1 and 'GamePlay/Combat/DamageCalculator.cs' in sources[0]['path']
    source=get('source',id=sources[0]['id'])[0];checks['source_exact_bytes_text']=source['text']==(LOCAL/source['path']).read_text('utf8')
    methods=get('methods',q='CalcBaseDamage');checks['method_search']=any(m['type']=='GamePlay.Combat.DamageCalculator' for m in methods)
    method=get('method',id=methods[0]['id'])[0];checks['method_calls']=bool(json.loads(method['meta'])['calls'])
    tables=get('tables',q='data_hero');checks['table_rows']=next(x for x in tables if x['name']=='data_hero')['row_count']==37
    rows=get('rows',table='data_hero',q='文丑');checks['chinese_row']=len(rows)==1 and json.loads(rows[0]['text'])['id']==1001
    all_tables=get('tables');checks['table_coverage']=len(all_tables)==150
    payloads=get('payloads');checks['payload_coverage']=len(payloads)==92
    assets=get('assets',q='blue_6',type='Sprite');checks['asset_search']=len(assets)==1 and isinstance(assets[0]['path_id'],str)
    asset=get('asset',id=assets[0]['id'])[0];checks['large_path_id_exact']=asset['path_id']==str(json.loads(asset['meta'])['path_id'])
    media=json.loads(asset['media']);file=media['files'][0]
    with urlopen(base+'/media?'+urlencode({'id':asset['id'],'index':0})) as response:
        data=response.read();checks['PNG_route_exact']=response.headers['Content-Type']=='image/png' and hashlib.sha256(data).hexdigest()==file['sha256']
    metadata=json.loads(asset['meta'])
    with urlopen(base+'/api/tree?'+urlencode({'id':asset['id']})) as response:
        checks['tree_route_exact']=response.read()==(LOCAL/metadata['tree']).read_bytes()
    page_one=get('sources',offset=0);page_two=get('sources',offset=100)
    checks['pagination']=len(page_one)==len(page_two)==100 and not ({r['id'] for r in page_one}&{r['id'] for r in page_two})
    try:urlopen(base+'/api/source?id=invalid');checks['invalid_id']=False
    except HTTPError as error:checks['invalid_id']=error.code==400
    db=sqlite3.connect(f'file:{LOCAL.as_posix()}/catalog.sqlite3?mode=ro',uri=True)
    counts={t:db.execute('SELECT count(*) FROM '+t).fetchone()[0] for t in ['source','method','config_row','asset']};db.close()
    checks['catalog_counts']=counts=={'source':32913,'method':218214,'config_row':10625,'asset':59312}
    result={'date':'2026-10-14','API_checks':checks,'passed':sum(checks.values()),'total':len(checks),'counts':counts,
        'browser_observation_record':'evidence/browser-observation.json; actual browser observation is recorded separately, not inferred by this API script.',
        'scope':'Route identity, pagination, exact media/tree bytes, 64-bit IDs and observed browser flow. Canvas syntax/SDK checks and gameplay/runtime verification are separate.'}
    write(PROJECT/'evidence/explorer-qa.json',result);print(json.dumps(result,ensure_ascii=True))
    assert all(checks.values())


if __name__=='__main__':main()
