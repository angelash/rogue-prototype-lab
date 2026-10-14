"""Recover the authorized embedded byte wrapper; independently verify the whole DEX."""
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import zlib
from common import PROJECT,LOCAL,APK,write
from analyze_structure import dex_metadata
from analyze_android import run


def main():
    original=APK/'assets/39285EFA.dex';wrapped=original.read_bytes()
    mask=bytes(a^b for a,b in zip(wrapped[:4],b'dex\n'))
    decoded=bytes(value^mask[index%4] for index,value in enumerate(wrapped))
    assert decoded[:8]==b'dex\n035\0' and struct.unpack_from('<I',decoded,32)[0]==len(decoded)
    assert hashlib.sha1(decoded[32:]).digest()==decoded[12:32]
    assert zlib.adler32(decoded[12:])&0xffffffff==struct.unpack_from('<I',decoded,8)[0]
    output=LOCAL/'android/decoded/39285EFA.dex';output.parent.mkdir(parents=True,exist_ok=True);output.write_bytes(decoded)
    summary,classes=dex_metadata(output)
    java=Path('C:/Program Files/Java/jdk-17/bin/java.exe');jar=LOCAL/'tools/jadx/lib/jadx-1.5.3-all.jar'
    normal=LOCAL/'android/java-embedded';fallback=LOCAL/'android/fallback/39285EFA'
    base=[str(java),'-Xmx1g','-cp',str(jar),'jadx.cli.JadxCLI','--no-res','-j','4','--use-source-name-as-class-name-alias','never','--log-level','warn']
    normal_exit=run([*base,'-d',str(normal),str(output)],LOCAL/'logs/jadx-embedded.txt')
    fallback_exit=run([*base,'--decompilation-mode','fallback','--no-inline-anonymous','--no-move-inner-classes','--no-debug-info','-d',str(fallback),str(output)],LOCAL/'logs/jadx-fallback-39285EFA.txt')
    disassembly=LOCAL/'android/disassembly/39285EFA.txt'
    assert run(['D:/androidSdk/build-tools/34.0.0/dexdump.exe','-d','-f',str(output)],disassembly)==0
    text=disassembly.read_text('utf8',errors='replace');units=[int(x) for x in re.findall(r'insns size\s*:\s*(\d+) 16-bit code units',text)]
    assert len(re.findall(r'^Class #\d+\s+-',text,re.M))==summary['header']['classCount']
    assert len(units)==summary['methodBodies'] and sum(units)==summary['instructionUnits']
    record={'date':'2026-10-14','original':'local/apk/assets/39285EFA.dex','original_bytes':len(wrapped),
        'original_sha256':hashlib.sha256(wrapped).hexdigest(),'decoded':output.relative_to(PROJECT).as_posix(),
        'decoded_sha256':hashlib.sha256(decoded).hexdigest(),'encoding':'four-byte repeating XOR inferred from the standard header; entire DEX signature/checksum/size verified',
        'sha1_match':summary['sha1Matches'],'adler32_match':summary['adler32Matches'],'classes':summary['header']['classCount'],
        'method_bodies':summary['methodBodies'],'instruction_units':summary['instructionUnits'],
        'normal_exit':normal_exit,'fallback_exit':fallback_exit,'normal_java_files':len(list(normal.rglob('*.java'))),
        'fallback_java_files':len(list(fallback.rglob('*.java'))),'SDK_counts_match':True,
        'scope':'Offline byte-container recovery, without modifying the APK, signing/billing checks or accessing services.'}
    write(PROJECT/'evidence/embedded-dex-recovery.json',record)
    structure=json.loads((PROJECT/'evidence/structure-summary.json').read_text('utf8'))
    for entry in structure['nonstandardDEX']:
        entry.update(status='recovered with whole-file DEX validation',recovery='evidence/embedded-dex-recovery.json')
    write(PROJECT/'evidence/structure-summary.json',structure)
    print(json.dumps(record,ensure_ascii=True))


if __name__=='__main__':main()
