"""Read-only localhost UI for recovered code, configuration and assets."""
import argparse, json, mimetypes, sqlite3
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse

ROOT=Path(__file__).resolve().parents[1]; LOCAL=ROOT/'local'
PAGE=ROOT/'scripts/catalog.html'

class Handler(BaseHTTPRequestHandler):
    def send(self,data,kind='application/json; charset=utf-8',code=200):
        if isinstance(data,(list,dict)):data=json.dumps(data,ensure_ascii=False).encode('utf8')
        elif isinstance(data,str):data=data.encode('utf8')
        self.send_response(code);self.send_header('Content-Type',kind);self.send_header('Content-Length',str(len(data)));self.send_header('X-Content-Type-Options','nosniff');self.end_headers();self.wfile.write(data)
    def do_GET(self):
        u=urlparse(self.path);q={k:v[0] for k,v in parse_qs(u.query).items()}
        try:
            if u.path=='/':return self.send(PAGE.read_bytes(),'text/html; charset=utf-8')
            if u.path=='/api/summary':
                d=json.loads((ROOT/'evidence/deep-coverage.json').read_text('utf8'));d['tables']=json.loads((LOCAL/'config/catalog.json').read_text('utf8'));d['media']=json.loads((ROOT/'evidence/media-export.json').read_text('utf8')) if (ROOT/'evidence/media-export.json').exists() else None
                return self.send(d)
            if u.path=='/api/search':
                kind=q.get('kind','types');word=q.get('q','')[:200];pattern='%'+word.replace('\\','\\\\').replace('%','\\%').replace('_','\\_')+'%';limit=min(200,max(1,int(q.get('limit',50))))
                with sqlite3.connect(f'file:{(LOCAL/"deep-catalog.sqlite3").as_posix()}?mode=ro',uri=True) as db:
                    db.row_factory=sqlite3.Row
                    if kind=='types':sql="SELECT * FROM types WHERE profile=? AND (name LIKE ? ESCAPE '\\' OR assembly LIKE ? ESCAPE '\\') LIMIT ?";args=(q.get('profile','release'),pattern,pattern,limit)
                    elif kind=='methods':sql="SELECT * FROM methods WHERE profile=? AND (owner LIKE ? ESCAPE '\\' OR name LIKE ? ESCAPE '\\') LIMIT ?";args=(q.get('profile','release'),pattern,pattern,limit)
                    elif kind=='calls':sql="SELECT c.*, m.owner, m.name FROM calls c JOIN methods m ON c.profile=m.profile AND c.grp=m.grp AND c.assembly=m.assembly AND c.caller=m.token WHERE c.profile=? AND target LIKE ? ESCAPE '\\' LIMIT ?";args=(q.get('profile','release'),pattern,limit)
                    elif kind=='assets':sql="SELECT * FROM assets WHERE type LIKE ? ESCAPE '\\' OR name LIKE ? ESCAPE '\\' OR address LIKE ? ESCAPE '\\' OR script LIKE ? ESCAPE '\\' LIMIT ?";args=(pattern,pattern,pattern,pattern,limit)
                    elif kind=='config':sql="SELECT DISTINCT r.* FROM config_rows r LEFT JOIN config_labels l ON r.tab=l.tab AND r.id=l.id WHERE r.tab=? AND (r.id LIKE ? ESCAPE '\\' OR r.data LIKE ? ESCAPE '\\' OR l.content LIKE ? ESCAPE '\\') LIMIT ?";args=(q.get('tab','language'),pattern,pattern,pattern,limit)
                    else:return self.send({'error':'Unknown index'},code=400)
                    rows=[dict(r) for r in db.execute(sql,args)]
                    if kind=='config':
                        for x in rows:x['labels']=[dict(l) for l in db.execute('SELECT field,key,languageid,content FROM config_labels WHERE tab=? AND id=?',(x['tab'],x['id']))]
                if kind=='assets' and (LOCAL/'assets/export/index.json').exists():
                    if not hasattr(self.server,'media_index'):
                        self.server.media_index={(x['bundleID'],x['pathID']):x for x in json.loads((LOCAL/'assets/export/index.json').read_text('utf8'))}
                    for x in rows:x['export']=self.server.media_index.get((x['bundle'],x['pathid']))
                return self.send({'rows':rows,'limit':limit,'truncated':len(rows)==limit})
            if u.path.startswith('/files/'):
                rel=unquote(u.path[len('/files/'):]);target=(LOCAL/rel).resolve();safe_rel=target.relative_to(LOCAL.resolve()).as_posix()
                assert safe_rel.startswith(('source/all/','config/tables/','assets/export/')) and target.is_file()
                ext=target.suffix.lower();assert ext in ('.cs','.json','.png','.wav','.ogg','.m4a','.gz','.native','.raw','.npy')
                kind='text/plain; charset=utf-8' if ext=='.cs' else mimetypes.guess_type(str(target))[0] or 'application/octet-stream'
                return self.send(target.read_bytes(),kind)
            return self.send({'error':'Not found'},code=404)
        except (AssertionError,ValueError,FileNotFoundError):return self.send({'error':'Invalid local request'},code=400)
    def log_message(self,*args):pass

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--port',type=int,default=8769);a=p.parse_args()
    server=ThreadingHTTPServer(('127.0.0.1',a.port),Handler)
    print(f'Read-only catalog: http://127.0.0.1:{a.port}',flush=True)
    try:server.serve_forever()
    except KeyboardInterrupt:server.server_close()
