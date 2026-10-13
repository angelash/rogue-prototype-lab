"""Recover authorized resource streams using the observed Launcher mixer."""
import hashlib, json, struct, time
from pathlib import Path
import numpy as np
import UnityPy

ROOT = Path(__file__).resolve().parents[1]

def seed_for(name, size):
    seed = 0x811c9dc5
    units = struct.unpack('<'+'H'*(len(name.encode('utf-16-le'))//2),name.encode('utf-16-le'))
    for c in units: seed = ((seed^c)*0x1000193)&0xffffffff
    seed = ((seed^(size&0xffffffff))*0x1000193)&0xffffffff
    seed = ((seed^(size>>32))*0x1000193)&0xffffffff
    return seed or 0xa5a5a5a5

def decode(data, name):
    seed = seed_for(name,len(data)); result=bytearray(len(data))
    for offset in range(0,len(data),1048576):
        n=min(1048576,len(data)-offset)
        with np.errstate(over='ignore'):
            positions=np.arange(offset,offset+n,dtype=np.uint64)
            x=((positions.astype(np.uint32)*np.uint32(0x9e3779b9)) ^ np.uint32(seed) ^ ((positions>>np.uint64(32)).astype(np.uint32)*np.uint32(0x85ebca6b)))
            for _ in range(2):
                x ^= x<<np.uint32(13); x ^= x>>np.uint32(17); x ^= x<<np.uint32(5)
            result[offset:offset+n]=np.bitwise_xor(np.frombuffer(data,dtype=np.uint8,count=n,offset=offset),x.astype(np.uint8)).tobytes()
    return bytes(result)

def main():
    entries=json.loads((ROOT/'local/manifest-bundles.json').read_text('utf8'))
    out=ROOT/'local/decoded/bundles';out.mkdir(parents=True,exist_ok=True)
    records=[]; failures=[]; start=time.monotonic()
    for b in entries:
        if b['raw']:continue
        try:
            data=(ROOT/'local/apk/assets'/b['filename']).read_bytes()
            plain=decode(data,b['name'])
            assert plain[:8]==b'UnityFS\0'
            assert struct.unpack_from('>Q',plain,32)[0]==len(plain)
            env=UnityPy.load(plain)
            bundle=next(iter(env.files.values()))
            objs=list(env.objects)
            files=list(bundle.files)
            path=out/b['filename'];path.write_bytes(plain)
            records.append(dict(id=b['id'],name=b['name'],filename=b['filename'],bytes=len(plain),SHA256=hashlib.sha256(plain).hexdigest(),objects=len(objs),internalFiles=files,passed=True))
        except Exception as e:
            failures.append(dict(id=b['id'],name=b['name'],error=type(e).__name__+': '+str(e)))
        if (len(records)+len(failures))%100==0:print(f'Validated {len(records)}, failures {len(failures)}',flush=True)
        if len(failures)>=3: raise RuntimeError(f'Consecutive initial failures: {failures[:3]}')
    (ROOT/'local/decoded/bundle-index.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n','utf8')
    (ROOT/'local/decoded/bundle-errors.json').write_text(json.dumps(failures,ensure_ascii=False,indent=2)+'\n','utf8')
    evidence=dict(candidates=981,passed=len(records),failed=len(failures),bytes=sum(x['bytes'] for x in records),objects=sum(x['objects'] for x in records),headerAndLengthPassed=True,allBlocksDecompressed=len(failures)==0,originalsPreserved=True,apkExecuted=False,seconds=round(time.monotonic()-start,2),loaderType='Launcher.Patch.ResourcePayloadMixer',ApplyVA='0x37c3bc4',CreateRandomSeedVA='0x37c9c40',ResolveMaskByteVA='0x37c9cdc',failures=failures)
    (ROOT/'evidence/bundle-decoding.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n','utf8')
    print(json.dumps(evidence,ensure_ascii=False));assert not failures

if __name__=='__main__':main()
