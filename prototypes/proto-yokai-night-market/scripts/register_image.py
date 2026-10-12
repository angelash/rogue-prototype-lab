"""Copy an image_gen result without editing, inspect its PNG metadata, and record provenance."""
import argparse, hashlib, json, shutil
from pathlib import Path
from datetime import datetime, timezone
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def save(p,v): p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def register(name, source=None):
    path=ROOT/'fidelity/art/jobs.json'; data=json.loads(path.read_text('utf-8'))
    job=next(j for j in data['jobs'] if j['name']==name)
    raw=ROOT/job['raw'];raw.parent.mkdir(parents=True,exist_ok=True)
    ledger=ROOT/'fidelity/art/generation-register.json'
    book=json.loads(ledger.read_text('utf-8')) if ledger.exists() else {'version':2,'tool':'image_gen.imagegen','images':[]}
    previous=next((r for r in book['images'] if r['name']==name),None)
    if source and previous and raw.exists():
        backup=ROOT/'fidelity/art/revisions'/(raw.stem+'.'+sha(raw)[:12]+'.png');backup.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(raw,backup);book.setdefault('revisions',[]).append(dict(previous,raw=backup.relative_to(ROOT).as_posix()))
    if source: shutil.copyfile(source,raw)
    with Image.open(raw) as im:
        width,height=im.size
        alpha=im.getchannel('A').getextrema() if 'A' in im.getbands() else (255,255)
    row={'name':name,'raw':job['raw'],'rawSHA256':sha(raw),'prompt':job['prompt'],'promptSHA256':sha(ROOT/job['prompt']),
         'rawSize':[width,height],'targetSize':[job['width'],job['height']],'alphaRange':alpha,
         'transparentRequested':job['transparent'],'references':[],'generatedAt':datetime.now(timezone.utc).isoformat(),
         'toolOutputFile':Path(source).name if source else '', 'review':'generated image visually inspected; runtime fit pending'}
    book['images']=[r for r in book['images'] if r['name']!=name]+[row];book['images'].sort(key=lambda r:r['name'])
    save(ledger,book);job['status']='raw_generated';job['final']='local/NightArt/'+Path(job['final']).name;save(path,data)
    print(json.dumps({'registered':name,'size':[width,height],'total':len(book['images'])},ensure_ascii=False))
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('name');parser.add_argument('source',nargs='?');args=parser.parse_args();register(args.name,args.source)
