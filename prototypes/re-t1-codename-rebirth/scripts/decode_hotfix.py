"""Recover authorized QXPM v2 sharded managed images with AES-GCM checks.

Build-specific wrap parameters are read from the original native binary. No
keys, permits or signatures are modified, and no online services are used.
"""
import hashlib, hmac, json, struct, sys, time
from pathlib import Path
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'local/tools/python'))
from elftools.elf.elffile import ELFFile

def native_read(va,n):
    with (ROOT/'local/apk/lib/arm64-v8a/libil2cpp.so').open('rb') as f:
        for s in ELFFile(f).iter_segments():
            if s['p_type']=='PT_LOAD' and s['p_vaddr']<=va<s['p_vaddr']+s['p_filesz']:
                f.seek(s['p_offset']+va-s['p_vaddr']);return f.read(n)
    raise ValueError('Unmapped native parameter')

def derive(prk,info):return hmac.digest(prk,info+b'\x01','sha256')

def recover(path):
    envelope=path.read_bytes()
    magic,ver,header=struct.unpack_from('<4sHH',envelope)
    assert (magic,ver,header)==(b'QXTL',2,112)
    assert struct.unpack_from('<I',envelope,8)[0]==7
    assert struct.unpack_from('<III',envelope,16)==(len(envelope),112,len(envelope)-112)
    d=envelope[112:]
    magic,version,header,kind,flags=struct.unpack_from('<4sHHII',d)
    assert (magic,version,header,flags)==(b'QXPM',2,192,0)
    assert d[16:32]==native_read(0x1237892,16) and d[32:48]==native_read(0x12378a2,16)
    assert d[80:96]==native_read(0x12378e2,16)
    ad=struct.pack('<4sHH',b'QXPK',1,96)+d[16:96]+d[8:12]+d[156:160]
    master=AESGCM(native_read(0x12378f2,32)).decrypt(d[96:108],d[124:156]+d[108:124],ad)
    size,blocks,shard_count,imports,string_bytes=struct.unpack_from('<5I',d,156)
    group_count=struct.unpack_from('<I',d,188)[0]
    shard_start=header+imports*8;group_start=shard_start+shard_count*56;string_start=group_start+group_count*44
    assert string_start+string_bytes==len(d)
    strings=d[string_start:]
    assembly_offset=struct.unpack_from('<I',d,176)[0]
    assembly=strings[assembly_offset:].split(b'\0',1)[0].decode('utf8')
    assert assembly
    shard_records=[d[shard_start+i*56:shard_start+(i+1)*56] for i in range(shard_count)]
    shards=[]
    for r in shard_records:
        suffix='.aotmeta.qxps.bytes' if '.aotmeta.' in path.name else '.qxps.bytes'
        raw=(path.parent/(r[:16].hex()+suffix)).read_bytes()
        assert len(raw)==struct.unpack_from('<I',r,16)[0]
        assert hashlib.sha256(raw).digest()==r[24:56]
        shards.append(raw)
    prk=hmac.digest(d[48:64],master,'sha256');records=bytearray(blocks*80)
    expected=0
    for i in range(group_count):
        r=d[group_start+i*44:group_start+(i+1)*44]
        first,count,sid,reserved=struct.unpack_from('<4I',r)
        assert first==expected and reserved==0 and len(shards[sid])==count*80
        info=struct.pack('<4sHH',b'QXPI',1,76)+d[16:64]+d[8:12]+struct.pack('<I',i)+r[:12]
        plain=AESGCM(derive(prk,info)).decrypt(r[16:28],shards[sid]+r[28:44],info)
        records[first*80:(first+count)*80]=plain;expected+=count
    assert expected==blocks
    output=bytearray(size);expected_offset=0;segment_kinds={}
    for i in range(blocks):
        r=records[i*80:(i+1)*80]
        offset,n,sid,cipher_offset,segment_flags=struct.unpack_from('<5I',r)
        assert offset==expected_offset and 0<n<=16384
        cipher=shards[sid][cipher_offset:cipher_offset+n]
        assert len(cipher)==n and hashlib.sha256(cipher).digest()==r[48:80]
        info=struct.pack('<4sHH',b'QXPB',1,76)+d[16:64]+d[8:12]+struct.pack('<II',segment_flags&255,i)+r[:8]
        plain=AESGCM(derive(prk,info)).decrypt(r[20:32],cipher+r[32:48],info)
        output[offset:offset+n]=plain;expected_offset+=n
        segment_kinds[str(segment_flags)]=segment_kinds.get(str(segment_flags),0)+1
    assert expected_offset==size and output[:2]==b'MZ'
    pe=struct.unpack_from('<I',output,60)[0];assert output[pe:pe+4]==b'PE\0\0'
    profile=path.parent.name;kind_name='aotmeta' if kind==2 else 'hotfix'
    target=ROOT/'local/managed'/profile/kind_name/(assembly+'.dll');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(output)
    return dict(profile=profile,assembly=assembly,kind=kind_name,bytes=len(output),SHA256=hashlib.sha256(output).hexdigest(),manifest=path.relative_to(ROOT/'local').as_posix(),manifestSHA256=hashlib.sha256(envelope).hexdigest(),shards=shard_count,blocks=blocks,indexGroups=group_count,shardHashesPassed=shard_count,blockCipherHashesPassed=blocks,GCMKeyWrapPassed=True,GCMIndexTagsPassed=group_count,GCMBlockTagsPassed=blocks,allBytesCovered=True,PEHeaderPassed=True,QXTLSignature='not independently verified',segmentKinds=segment_kinds,file=target.relative_to(ROOT/'local').as_posix())

def main():
    start=time.monotonic();records=[];errors=[]
    for f in sorted((ROOT/'local/assets/text/assets/res/hotfixes').rglob('*.qxpm.bytes')):
        try:records.append(recover(f));print(f'Recovered {records[-1]["profile"]} {records[-1]["assembly"]}, {records[-1]["blocks"]} blocks',flush=True)
        except Exception as ex:errors.append(dict(file=f.relative_to(ROOT/'local').as_posix(),error=type(ex).__name__+': '+str(ex)));print('ERROR',f.name,str(ex),flush=True)
    (ROOT/'local/managed/index.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n','utf8')
    summary=dict(manifests=len(records)+len(errors),recovered=len(records),failed=len(errors),bytes=sum(x['bytes'] for x in records),profiles={k:sum(x['profile']==k for x in records) for k in ['debug','release']},blocks=sum(x['blocks'] for x in records),shards=sum(x['shards'] for x in records),errors=errors,seconds=round(time.monotonic()-start,2),apkExecuted=False,signatureIndependentVerification=False)
    (ROOT/'evidence/hotfix-decoding.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n','utf8');print(json.dumps(summary,ensure_ascii=False));assert not errors

if __name__=='__main__':main()
