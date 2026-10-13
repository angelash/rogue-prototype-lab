"""Decode all supported media; keep raw compiled payloads in the copied build."""
import argparse, collections, gc, json, re, sqlite3, wave
from pathlib import Path
import UnityPy
from UnityPy.tools import extractor
from PIL import Image
from prepare import write, sha

EXPORTERS={'Texture2D':extractor.exportTexture2D,'Sprite':extractor.exportSprite,
           'Mesh':extractor.exportMesh,'AudioClip':extractor.exportAudioClip,
           'Font':extractor.exportFont,'TextAsset':extractor.exportTextAsset,
           'Shader':extractor.exportShader}

def export_texture(obj,fp):
    tex=obj.read()
    if tex.m_TextureFormat==24 and tex.m_Width and tex.m_Height:
        import texture2ddecoder
        raw=tex.get_image_data()
        Path(fp+'.bc6').write_bytes(raw) # Original HDR compression, including mips.
        pixels=texture2ddecoder.decode_bc6(raw,tex.m_Width,tex.m_Height)
        Image.frombytes('RGBA',(tex.m_Width,tex.m_Height),pixels,'raw','BGRA').transpose(Image.Transpose.FLIP_TOP_BOTTOM).save(fp+'.png')
    else:extractor.exportTexture2D(obj,fp)

EXPORTERS['Texture2D']=export_texture

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);args=ap.parse_args()
    p=Path(args.project).resolve();local=p/'local';data=local/'build-copy/Duckov_Data'
    db=sqlite3.connect(local/'assets.sqlite');db.execute('''CREATE TABLE IF NOT EXISTS media_status(object_id TEXT PRIMARY KEY,type TEXT,status TEXT,error TEXT)''')
    selected=list(db.execute('SELECT name,path FROM containers WHERE name IN (SELECT DISTINCT container FROM objects WHERE type IN ("Texture2D","Sprite","Mesh","AudioClip","Font","TextAsset","Shader"))'))
    total=0
    for container,path in selected:
        env=UnityPy.load(str(data/path));asset=next(a for a in env.assets if a.name==container)
        rows=db.execute('SELECT id,pid,type,name FROM objects WHERE container=? AND type IN ("Texture2D","Sprite","Mesh","AudioClip","Font","TextAsset","Shader")',(container,)).fetchall()
        for oid,pid,typ,name in rows:
            if db.execute('SELECT status FROM media_status WHERE object_id=?',(oid,)).fetchone() in (('ok',),('empty-payload',),('raw-only',)):continue
            stem=f'{pid}_'+(re.sub(r'[^\w .-]','_',name).strip(' .')[:85] or 'unnamed')
            out=local/'exports'/typ/container/stem;out.parent.mkdir(parents=True,exist_ok=True)
            try:
                EXPORTERS[typ](asset.objects[pid],str(out))
                outputs=[f for f in out.parent.glob(out.name+'*') if f.is_file()]
                if out.is_dir():outputs+=list(out.rglob('*'))
                outputs=[f for f in outputs if f.is_file() and f.stat().st_size>0]
                for f in outputs:
                    if f.suffix=='.png':
                        with Image.open(f) as im:im.verify()
                    elif f.suffix=='.wav':
                        with wave.open(str(f),'rb') as w:
                            if w.getnframes()<=0:raise ValueError('Empty decoded audio')
                    elif f.suffix=='.obj':
                        if '\nv ' not in f.read_text('utf-8',errors='replace'):raise ValueError('No OBJ vertices')
                db.execute('DELETE FROM exports WHERE object_id=?',(oid,))
                for f in outputs:db.execute('INSERT INTO exports VALUES(?,?,?,?)',(oid,f.relative_to(local).as_posix(),f.stat().st_size,sha(f)))
                db.execute('INSERT OR REPLACE INTO media_status VALUES(?,?,?,NULL)',(oid,typ,'ok' if outputs else 'empty-payload'))
            except Exception as ex:
                status='failed'
                if typ=='Shader':
                    raw=out.with_suffix('.shaderbin');raw.write_bytes(asset.objects[pid].get_raw_data())
                    db.execute('DELETE FROM exports WHERE object_id=?',(oid,))
                    db.execute('INSERT INTO exports VALUES(?,?,?,?)',(oid,raw.relative_to(local).as_posix(),raw.stat().st_size,sha(raw)))
                    status='raw-only'
                db.execute('INSERT OR REPLACE INTO media_status VALUES(?,?,?,?)',(oid,typ,status,f'{type(ex).__name__}: {ex}'))
            total+=1
            if total%250==0:db.commit();print(f'Media processed {total}: {container} {typ}',flush=True)
        db.commit();del env;gc.collect();print(f'Media complete {container}: {len(rows)} objects',flush=True)
    summary=dict(objects=db.execute('SELECT count(*) FROM media_status').fetchone()[0],
        byType=[dict(type=r[0],status=r[1],count=r[2]) for r in db.execute('SELECT type,status,count(*) FROM media_status GROUP BY type,status')],
        files=db.execute('SELECT count(*) FROM exports').fetchone()[0],bytes=db.execute('SELECT sum(bytes) FROM exports').fetchone()[0],
        failures=[dict(id=r[0],type=r[1],error=r[2]) for r in db.execute('SELECT object_id,type,error FROM media_status WHERE status="failed"')],
        rawOnly=[dict(id=r[0],type=r[1],error=r[2]) for r in db.execute('SELECT object_id,type,error FROM media_status WHERE status="raw-only"')],
        hdrPreview='BC6H: original compression retained in .bc6; PNG is an 8-bit preview.')
    write(p/'evidence/media-summary.json',summary);db.execute('PRAGMA wal_checkpoint(TRUNCATE)');db.close()
    print(f'Exported {summary["files"]} files; failed objects={len(summary["failures"])}',flush=True)

if __name__=='__main__':main()
