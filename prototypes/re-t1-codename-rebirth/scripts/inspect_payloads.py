"""Validate textual payloads, FMOD RIFF banks and the actual collision layout."""
import collections, hashlib, json, math, struct, zlib
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1];LOCAL=ROOT/'local'

class Cursor:
    def __init__(self,d):self.d=d;self.p=0
    def read(self,f):
        n=struct.calcsize('<'+f);assert self.p+n<=len(self.d)
        v=struct.unpack_from('<'+f,self.d,self.p);self.p+=n;return v[0] if len(v)==1 else list(v)
    def count(self):n=self.read('I');assert n<=len(self.d);return n

def collision(d,block):
    r=Cursor(d);neighbor=r.read('B');assert neighbor in (0,1)
    groups={}
    def shape():
        kind=r.read('B');assert kind in (0,1,2,3)
        s={'type':kind,'group':r.read('i'),'position':r.read('3f'),'rotation':r.read('4f')}
        if kind==2:s['size']=r.read('3f')
        elif kind==0:s['radius']=r.read('f')
        else:s.update(radius=r.read('f'),height=r.read('f'))
        if neighbor:
            s['id']=r.read('i');n=r.read('H');s['neighbors']=[r.read('i') for _ in range(n)]
        return s
    n=r.count()
    if block:
        for _ in range(n):
            group=r.read('I');assert group not in groups;groups[group]=[shape() for _ in range(r.count())]
    else:groups[0]=[shape() for _ in range(n)]
    assert r.p==len(d)
    return {'format':'blocks' if block else 'flat','neighbors':bool(neighbor),'groups':groups,'shapeCount':sum(map(len,groups.values()))}

def main():
    rows=json.loads((LOCAL/'assets/text-assets.json').read_text('utf8'));out=LOCAL/'payloads';out.mkdir(exist_ok=True);results=[];types=collections.Counter();errors=[];shapes=0
    for x in rows:
        path=LOCAL/x['payload'];d=path.read_bytes();r={'input':x['payload'],'bytes':len(d),'sha256':hashlib.sha256(d).hexdigest()}
        try:
            if path.suffix=='.json':
                j=json.loads(d.decode('utf-8-sig'));r.update(kind='JSON',items=len(j) if hasattr(j,'__len__') else None)
            elif '/collision/client/' in x['payload']:
                candidates=[]
                for block in (False,True):
                    try:candidates.append(collision(d,block))
                    except (AssertionError,struct.error):pass
                assert len(candidates)==1,('Ambiguous/unknown collision layout',len(candidates))
                j=candidates[0];r.update(kind='collision',format=j['format'],shapes=j['shapeCount']);shapes+=j['shapeCount'];(out/(path.stem+'.json')).write_text(json.dumps(j,ensure_ascii=False,separators=(',',':')),'utf8')
            elif d.startswith(b'RIFF'):
                assert struct.unpack_from('<I',d,4)[0]+8==len(d)
                p=12;chunks=[]
                while p<len(d):
                    name=d[p:p+4].decode('ascii');n=struct.unpack_from('<I',d,p+4)[0];assert p+8+n<=len(d);chunks.append({'tag':name,'offset':p+8,'bytes':n});p+=8+n+(n%2)
                assert p==len(d);r.update(kind='RIFF-FMOD' if d[8:12]==b'FEV ' else 'RIFF',chunks=chunks,FSB5Offsets=[i for i in range(len(d)) if d.startswith(b'FSB5',i)])
            elif d.startswith(b'HCRH'):r.update(kind='HCRH-render-data',semanticStatus='Custom native render payload; preserved, header inventoried')
            elif '.qxps.' in path.name:r.update(kind='QXPS',semanticStatus='Shard bytes SHA-verified and consumed by decode_hotfix')
            elif d.startswith(b'QXTL'):r.update(kind='QXTL-QXPM',semanticStatus='Managed image restored and authenticated with AES-GCM; outer signature separately unverified')
            elif d.startswith(b'QXTM'):r.update(kind='QXTM-token-map',semanticStatus='Preserved token-map artifact; no native generic reconstruction claim')
            elif d.startswith(b'QCFG'):r.update(kind='QCFG',semanticStatus='Full schema and rows decoded separately')
            elif '/sdf/' in x['payload']:r.update(kind='SDF-data',semanticStatus='Custom voxel payload preserved; no converted volume claim')
            elif path.suffix=='.txt':d.decode('utf-8-sig');r.update(kind='UTF8-text')
            else:r.update(kind='other-binary',header=d[:32].hex(),semanticStatus='Preserved; meaning not inferred')
            r['status']='ok'
        except Exception as ex:r.update(status='failed',error=type(ex).__name__+': '+str(ex));errors.append(r.copy())
        types[r.get('kind','unclassified')]+=1;results.append(r)
    # Two identical SDK PNG entries have only the first signature byte damaged.
    repaired=[]
    for x in json.loads((ROOT/'evidence/raw-images.json').read_text('utf8')):
        if x['status']=='ok':continue
        path=LOCAL/'apk'/x['file'];d=path.read_bytes()
        if d[:8]!=b'\x00PNG\r\n\x1a\n':continue
        p=8
        while p<len(d):
            n=struct.unpack_from('>I',d,p)[0];assert p+n+12<=len(d)
            assert zlib.crc32(d[p+4:p+8+n])&0xffffffff==struct.unpack_from('>I',d,p+8+n)[0];p+=n+12
        assert p==len(d)
        target=out/(path.stem+'_signature-repaired.png');target.write_bytes(b'\x89'+d[1:])
        with Image.open(target) as im:im.verify()
        repaired.append({'input':x['file'],'output':target.relative_to(LOCAL).as_posix(),'inputSHA256':hashlib.sha256(d).hexdigest(),'outputSHA256':hashlib.sha256(target.read_bytes()).hexdigest(),'changedBytes':[{'offset':0,'before':0,'after':137}],'allChunkCRCsPassed':True})
    (out/'index.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),'utf8')
    evidence={'textAssets':len(rows),'kinds':dict(types.most_common()),'validationFailures':errors,'collisionShapes':shapes,'rawPNGSignatureRepairs':repaired,'scope':'JSON/collision/RIFF structural decoding; HCRH, SDF and QXTM retain opaque native payloads, not converted geometry or generic code'}
    (ROOT/'evidence/payload-inspection.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),'utf8');print(json.dumps({k:v for k,v in evidence.items() if k!='rawPNGSignatureRepairs'},ensure_ascii=False))

if __name__=='__main__':main()
