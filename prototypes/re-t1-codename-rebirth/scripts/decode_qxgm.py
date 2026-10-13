"""Decode this authorized build's QXGM mode-1 metadata; preserve original bytes.

The decoder derives the build-specific parameter from native instructions, and
checks every plaintext page digest plus the metadata header HMAC. The optional
QGSG signature remains separately labelled; this does not execute the APK.
"""
import hashlib, hmac, json, struct, sys
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'local/tools/python'))
from capstone import Cs, CS_ARCH_ARM64, CS_MODE_LITTLE_ENDIAN
from elftools.elf.elffile import ELFFile

MASK = (1 << 64) - 1
GOLD = 0x9e3779b97f4a7c15
def mix(x):
    x = ((x ^ (x >> 30)) * 0xbf58476d1ce4e5b9) & MASK
    x = ((x ^ (x >> 27)) * 0x94d049bb133111eb) & MASK
    return x ^ (x >> 31)

def native_parameter(path):
    with path.open('rb') as f:
        elf = ELFFile(f)
        def read(va, n):
            for s in elf.iter_segments():
                if s['p_type'] == 'PT_LOAD' and s['p_vaddr'] <= va < s['p_vaddr']+s['p_filesz']:
                    f.seek(s['p_offset'] + va - s['p_vaddr']); return f.read(n)
            raise ValueError(f'Unmapped native address {va:x}')
        md = Cs(CS_ARCH_ARM64, CS_MODE_LITTLE_ENDIAN); md.detail = True
        val = 0; parts = 0
        for ins in md.disasm(read(0x2066a78, 0x1c), 0x2066a78):
            if ins.mnemonic not in ('mov', 'movk') or ins.reg_name(ins.operands[0].reg) != 'x2': continue
            imm = ins.operands[1].imm; shift = ins.operands[1].shift.value
            if ins.mnemonic == 'mov': val = imm & MASK
            else: val = (val & ~(0xffff << shift)) | ((imm & 0xffff) << shift)
            parts += 1
        assert parts == 4
        return val, read(0xd7c3b8, 8), read(0xd7be90, 8)

def main():
    source = ROOT/'local/apk/assets/bin/Data/Managed/Metadata/global-metadata.dat'
    native = ROOT/'local/apk/lib/arm64-v8a/libil2cpp.so'
    data = source.read_bytes(); master, digest_prefix, table_prefix = native_parameter(native)
    magic, version, mode, flags, size, stored, payload, reserved, page_size, page_count, tag_size, hash_size = struct.unpack_from('<4s11I', data)
    assert (magic,version,mode,page_size,tag_size,hash_size,reserved) == (b'QXGM',2,1,4096,32,32,0)
    assert size == stored and page_count == (size+4095)//4096
    tables = 224 if flags & 16 else 128
    assert payload == tables + page_count*(tag_size+hash_size) and len(data) == payload+size
    tags = data[tables:tables+page_count*32]
    hashes = data[tables+page_count*32:payload]
    assert hashlib.sha256(table_prefix+struct.pack('<I',page_count)+hashes).digest() == data[64:96]
    auth_header = data[32:96] + bytes(32)
    auth_key = hashlib.sha256(struct.pack('<QQ',0x71782d676d32746d,master)+data[48:60]).digest()
    assert hmac.digest(auth_key, data[:32]+auth_header+tags+hashes,'sha256') == data[96:128]
    salt = struct.unpack_from('<Q',data,48)[0]
    result = bytearray(size); verified = 0
    for page in range(page_count):
        offset = page*page_size; n = min(page_size,size-offset)
        seed = mix(((page+1)*GOLD ^ master ^ salt ^ size<<32 ^ flags ^ 0x71782d676d326263)&MASK)
        with np.errstate(over='ignore'):
            x = np.arange(1,n+1,dtype=np.uint64)*np.uint64(GOLD)+np.uint64(seed)
            x = (x^(x>>np.uint64(30)))*np.uint64(0xbf58476d1ce4e5b9)
            x = (x^(x>>np.uint64(27)))*np.uint64(0x94d049bb133111eb)
            mask = (x>>np.uint64(56)).astype(np.uint8)
        plain = np.bitwise_xor(np.frombuffer(data,dtype=np.uint8,count=n,offset=payload+offset),mask).tobytes()
        digest = hashlib.sha256(digest_prefix+struct.pack('<III',page,offset,n)+plain).digest()
        assert digest == hashes[page*32:(page+1)*32], f'Page digest mismatch {page}'
        result[offset:offset+n] = plain; verified += 1
    assert result[:4] == b'\xaf\x1b\xb1\xfa'
    destination=ROOT/'local/decoded/global-metadata.dat'; destination.parent.mkdir(parents=True,exist_ok=True)
    destination.write_bytes(result)
    evidence = dict(source=str(source.relative_to(ROOT)).replace('\\','/'), sourceSHA256=hashlib.sha256(data).hexdigest(), nativeSHA256=hashlib.sha256(native.read_bytes()).hexdigest(), output=str(destination.relative_to(ROOT)).replace('\\','/'), outputBytes=size, outputSHA256=hashlib.sha256(result).hexdigest(), metadataVersion=struct.unpack_from('<I',result,4)[0], envelopeVersion=version,mode=mode,flags=flags,pages=page_count,pagesDigestPassed=verified,hashTableSHA256Passed=True,headerHMACPassed=True,QGSGSignatureVerification='not independently verified',nativeEntryVA='0x2066a74',nativeDecoderVA='0x2219230',apkExecuted=False)
    (ROOT/'evidence/qxgm-decoding.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n','utf8')
    print(json.dumps(evidence,ensure_ascii=False))

if __name__ == '__main__': main()
