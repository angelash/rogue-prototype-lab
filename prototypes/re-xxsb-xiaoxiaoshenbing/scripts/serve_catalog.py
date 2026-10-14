"""Serve the private catalog only on loopback, with read-only GET routes."""
import argparse
import json
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from pathlib import Path
import sqlite3
from urllib.parse import urlparse,parse_qs
from common import PROJECT,LOCAL


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        parsed=urlparse(self.path);q=parse_qs(parsed.query)
        def arg(name,default=''):return q.get(name,[default])[0]
        def send(data,kind='application/json; charset=utf-8',status=200):
            if not isinstance(data,bytes):data=json.dumps(data,ensure_ascii=True).encode('utf8')
            self.send_response(status);self.send_header('Content-Type',kind);self.send_header('X-Content-Type-Options','nosniff');self.send_header('Cache-Control','no-store');self.end_headers();self.wfile.write(data)
        if parsed.path=='/':return send((Path(__file__).parent/'catalog.html').read_bytes(),'text/html; charset=utf-8')
        db=sqlite3.connect(f'file:{LOCAL.as_posix()}/catalog.sqlite3?mode=ro',uri=True);db.row_factory=sqlite3.Row
        def query(sql,params=()):return [dict(row) for row in db.execute(sql,params)]
        try:
            key=arg('q')[:200];like='%'+key+'%';limit=min(max(int(arg('limit','100')),1),200);offset=max(int(arg('offset','0')),0)
            if parsed.path=='/api/status':
                evidence={}
                for path in sorted((PROJECT/'evidence').glob('*.json')):
                    if path.name in ['apk-files.json','extraction.json','preparation.json','native-summary.json','dex-summary.json']:continue
                    obj=json.loads(path.read_text('utf8'));evidence[path.stem]={k:v for k,v in obj.items() if k not in ['files','records','failures','business_systems','table_rows']}
                return send(evidence)
            if parsed.path=='/api/sources':
                if key and key.isascii():
                    phrase='"'+key.replace('"','""')+'"'
                    return send(query('SELECT s.id,s.path,s.kind,s.assembly,length(s.text) AS characters FROM source s JOIN source_fts ON s.id=source_fts.rowid WHERE source_fts MATCH ? LIMIT ? OFFSET ?',(phrase,limit,offset)))
                return send(query('SELECT id,path,kind,assembly,length(text) AS characters FROM source WHERE path LIKE ? OR text LIKE ? LIMIT ? OFFSET ?',(like,like,limit,offset)))
            if parsed.path=='/api/source':
                return send(query('SELECT * FROM source WHERE id=?',(int(arg('id')),)))
            if parsed.path=='/api/methods':
                return send(query('SELECT id,assembly,type,name,token,kind,has_il FROM method WHERE type LIKE ? OR name LIKE ? LIMIT ? OFFSET ?',(like,like,limit,offset)))
            if parsed.path=='/api/method':return send(query('SELECT * FROM method WHERE id=?',(int(arg('id')),)))
            if parsed.path=='/api/tables':return send(query('SELECT name,kind,row_count,meta FROM config_table WHERE name LIKE ?',(like,)))
            if parsed.path=='/api/rows':
                return send(query('SELECT idx,key,text FROM config_row WHERE table_name=? AND text LIKE ? LIMIT ? OFFSET ?',(arg('table'),like,limit,offset)))
            if parsed.path=='/api/assets':
                return send(query('SELECT id,name,type,source,path_id,address FROM asset WHERE (name LIKE ? OR address LIKE ?) AND (?="" OR type=?) LIMIT ? OFFSET ?',(like,like,arg('type'),arg('type'),limit,offset)))
            if parsed.path=='/api/asset':return send(query('SELECT * FROM asset WHERE id=?',(arg('id'),)))
            if parsed.path=='/api/payloads':return send(query('SELECT * FROM payload WHERE name LIKE ?',(like,)))
            if parsed.path in ['/api/tree','/api/payload','/media']:
                if parsed.path=='/api/tree':
                    row=query('SELECT meta FROM asset WHERE id=?',(arg('id'),));meta=json.loads(row[0]['meta']);relative=meta.get('tree') or meta.get('raw')
                elif parsed.path=='/api/payload':
                    row=query('SELECT path FROM payload WHERE id=?',(int(arg('id')),));relative=row[0]['path']
                else:
                    row=query('SELECT media FROM asset WHERE id=?',(arg('id'),));media=json.loads(row[0]['media']);relative=media['files'][int(arg('index','0'))]['path']
                path=(LOCAL/relative).resolve();path.relative_to(LOCAL.resolve())
                kind='image/png' if path.suffix=='.png' else 'audio/wav' if path.suffix=='.wav' else 'text/plain; charset=utf-8' if path.suffix in ['.json','.txt'] else 'application/octet-stream'
                return send(path.read_bytes(),kind)
            send({'error':'route not found'},status=404)
        except (ValueError,KeyError,IndexError,TypeError,FileNotFoundError,sqlite3.Error,OverflowError) as error:
            send({'error':type(error).__name__},status=400)
        finally:db.close()
    def log_message(self,*args):pass


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--port',type=int,default=8775);args=parser.parse_args()
    print(f'Private read-only catalog: http://127.0.0.1:{args.port}/',flush=True)
    ThreadingHTTPServer(('127.0.0.1',args.port),Handler).serve_forever()
