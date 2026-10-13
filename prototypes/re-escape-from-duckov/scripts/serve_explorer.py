"""Localhost-only, read-only evidence browser. Does not launch or modify the game."""
import argparse,functools,json,mimetypes,sqlite3,zlib
from pathlib import Path
from http.server import ThreadingHTTPServer,BaseHTTPRequestHandler
from urllib.parse import urlsplit,parse_qs,unquote

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);ap.add_argument('--port',type=int,default=8784);a=ap.parse_args()
    project=Path(a.project).resolve();local=project/'local'
    catalog=json.loads((local/'content-catalog.json').read_text('utf-8'))
    code=json.loads((local/'code-index.json').read_text('utf-8'))
    audio=json.loads((local/'fmod-events.json').read_text('utf-8'))['events']
    samples=json.loads((local/'fmod-samples.json').read_text('utf-8'))
    def identity(row):
        for key in ('id','objectID','path','name'):
            if row.get(key) is not None and row.get(key)!='':return str(row[key])
        return ''
    @functools.lru_cache(maxsize=3)
    def metadata(name):return json.loads((local/'code-metadata'/f'{name}.json').read_text('utf-8'))
    def connect():
        c=sqlite3.connect((local/'assets.sqlite').as_uri()+'?mode=ro',uri=True);c.row_factory=sqlite3.Row;return c
    class Handler(BaseHTTPRequestHandler):
        def log_message(self,*args):pass
        def send(self,data,mime='application/json; charset=utf-8',status=200):
            if not isinstance(data,bytes):data=json.dumps(data,ensure_ascii=False).encode('utf-8')
            self.send_response(status);self.send_header('Content-Type',mime);self.send_header('Content-Length',str(len(data)))
            self.send_header('Cache-Control','no-store');self.end_headers();self.wfile.write(data)
        def do_GET(self):
            url=urlsplit(self.path);params=parse_qs(url.query);get=lambda k,d='':params.get(k,[d])[0]
            try:
                if url.path=='/':return self.send((project/'scripts/explorer.html').read_bytes(),'text/html; charset=utf-8')
                if url.path=='/api/overview':
                    evidence={name:json.loads((project/'evidence'/f'{name}.json').read_text('utf-8')) for name in ('build-summary','asset-summary','code-summary','content-summary','media-summary','audio-summary')}
                    return self.send(evidence)
                if url.path=='/api/list':
                    cat=get('category');q=get('q').casefold();page=max(0,int(get('page','0')));size=50
                    if cat=='objects':
                        with connect() as c:
                            where=[];values=[]
                            if q:where.append('(name LIKE ? OR id LIKE ? OR script LIKE ?)');values.extend(['%'+q+'%']*3)
                            if get('type'):where.append('type=?');values.append(get('type'))
                            if get('script'):where.append('script=?');values.append(get('script'))
                            if get('container'):where.append('container=?');values.append(get('container'))
                            clause=(' WHERE '+' AND '.join(where)) if where else ''
                            count=c.execute('SELECT count(*) FROM objects'+clause,values).fetchone()[0]
                            rows=[dict(r) for r in c.execute('SELECT id,name,type,script,container,status,bytes FROM objects'+clause+' ORDER BY rowid LIMIT ? OFFSET ?',values+[size,page*size])]
                    else:
                        rows=code if cat=='code' else audio if cat=='audio' else samples if cat=='samples' else catalog.get(cat,[])
                        if isinstance(rows,dict):rows=[rows]
                        if q:rows=[r for r in rows if q in json.dumps(r,ensure_ascii=False).casefold()]
                        count=len(rows);rows=rows[page*size:(page+1)*size]
                        rows=[dict({k:v for k,v in r.items() if k not in ('data','metadata','tasks','rewards','graph')},key=identity(r)) for r in rows]
                    return self.send(dict(rows=rows,total=count,page=page))
                if url.path=='/api/detail':
                    cat=get('category');ident=get('id')
                    if cat=='objects':
                        with connect() as c:
                            row=c.execute('SELECT * FROM objects WHERE id=?',(ident,)).fetchone()
                            if not row:return self.send(dict(error='Unknown object'),status=404)
                            rec=dict(row);rec['tree']=json.loads(zlib.decompress(rec['tree'])) if rec['tree'] else None
                            rec['references']=[dict(r) for r in c.execute('SELECT field,dst,resolved FROM edges WHERE src=?',(ident,))]
                            rec['usedBy']=[dict(r) for r in c.execute('SELECT src,field FROM edges WHERE dst=? LIMIT 80',(ident,))]
                            rec['exportFiles']=[dict(r) for r in c.execute('SELECT path,bytes,sha256 FROM exports WHERE object_id=?',(ident,))]
                        return self.send(rec)
                    if cat=='code':
                        name=get('assembly');allowed={r['assembly'] for r in code}
                        if name not in allowed:raise ValueError('Unknown assembly')
                        info=metadata(name);row=next((t for t in info['types'] if t['name']==ident),None)
                        if row:
                            row=dict(row);ns,_,typ=ident.split('+')[0].rpartition('.')
                            source=local/'decompiled'/name/ns/(typ.split('`')[0]+'.cs')
                            row['sourceFiles']=[source.relative_to(local).as_posix()] if source.is_file() and source.stat().st_size else []
                            if name=='NodeCanvas' and ident.split('+')[0]=='UnitySourceGeneratedAssemblyMonoScriptTypes_v1':row['sourceFiles']=['decompiled-repairs/NodeCanvas/recover-0.cs']
                            row['ilFiles']=[f.relative_to(local).as_posix() for f in (local/'il'/name).glob('*.il')]
                        return self.send(row)
                    rows=audio if cat=='audio' else samples if cat=='samples' else catalog.get(cat,[])
                    if isinstance(rows,dict):return self.send(rows)
                    return self.send(next((r for r in rows if identity(r)==ident),None))
                if url.path.startswith('/media/'):
                    target=(local/unquote(url.path[7:])).resolve()
                    if not target.is_relative_to(local/'exports'):raise ValueError('Only exported media can be served')
                    if not target.is_file():return self.send(dict(error='Not found'),status=404)
                    return self.send(target.read_bytes(),mimetypes.guess_type(str(target))[0] or 'application/octet-stream')
                if url.path.startswith('/source/'):
                    target=(local/unquote(url.path[8:])).resolve()
                    if not any(target.is_relative_to(local/root) for root in ('decompiled','decompiled-repairs','il')) or target.suffix not in ('.cs','.il'):raise ValueError('Only local C#/IL files can be served')
                    if not target.is_file():return self.send(dict(error='Not found'),status=404)
                    return self.send(target.read_bytes(),'text/plain; charset=utf-8')
                return self.send(dict(error='Not found'),status=404)
            except Exception as ex:self.send(dict(error=str(ex)),status=400)
    server=ThreadingHTTPServer(('127.0.0.1',a.port),Handler)
    print(f'Explorer: http://127.0.0.1:{a.port} (read-only, local only)',flush=True)
    server.serve_forever()

if __name__=='__main__':main()
