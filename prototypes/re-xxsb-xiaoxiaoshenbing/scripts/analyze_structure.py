"""Standard DEX/ELF metadata; no execution, decryption or network access."""
import collections
import hashlib
import json
import re
import struct
import zlib
from pathlib import Path
from common import PROJECT, write

def uleb(data, pos):
    value=0;shift=0
    while True:
        b=data[pos];pos+=1;value|=(b&127)<<shift
        if b<128:return value,pos
        shift+=7;assert shift<40

def dex_metadata(path):
    d=path.read_bytes();assert d[:4]==b'dex\n'
    h=struct.unpack_from('<20I',d,32)
    names=['fileSize','headerSize','endianTag','linkSize','linkOff','mapOff','stringCount','stringOff',
        'typeCount','typeOff','protoCount','protoOff','fieldCount','fieldOff','methodCount','methodOff',
        'classCount','classOff','dataSize','dataOff']
    hdr=dict(zip(names,h));assert hdr['fileSize']==len(d)
    strings=[]
    for i in range(hdr['stringCount']):
        off=struct.unpack_from('<I',d,hdr['stringOff']+i*4)[0]
        _,pos=uleb(d,off);end=d.index(0,pos)
        strings.append(d[pos:end].replace(b'\xc0\x80',b'\0').decode('utf-8',errors='surrogatepass'))
    types=[strings[struct.unpack_from('<I',d,hdr['typeOff']+i*4)[0]] for i in range(hdr['typeCount'])]
    def typelist(off):
        if not off:return []
        count=struct.unpack_from('<I',d,off)[0]
        return [types[x] for x in struct.unpack_from('<'+'H'*count,d,off+4)]
    protos=[]
    for i in range(hdr['protoCount']):
        _,ret,off=struct.unpack_from('<III',d,hdr['protoOff']+i*12)
        protos.append({'returns':types[ret],'parameters':typelist(off)})
    fields=[]
    for i in range(hdr['fieldCount']):
        cls,ty,n=struct.unpack_from('<HHI',d,hdr['fieldOff']+i*8)
        fields.append({'id':i,'class':types[cls],'type':types[ty],'name':strings[n]})
    methods=[]
    for i in range(hdr['methodCount']):
        cls,pro,n=struct.unpack_from('<HHI',d,hdr['methodOff']+i*8)
        methods.append({'id':i,'class':types[cls],'name':strings[n],**protos[pro]})
    classes=[];body_count=0;instruction_units=0
    for i in range(hdr['classCount']):
        cls,access,base,interfaces,source,annotations,off,values=struct.unpack_from('<8I',d,hdr['classOff']+i*32)
        c={'name':types[cls],'access':access,'base':types[base] if base!=0xffffffff else None,
            'interfaces':typelist(interfaces),'source':strings[source] if source!=0xffffffff else None,
            'fields':[],'methods':[]}
        if off:
            counts=[];pos=off
            for _ in range(4):v,pos=uleb(d,pos);counts.append(v)
            for count in counts[:2]:
                idx=0
                for _ in range(count):
                    delta,pos=uleb(d,pos);flags,pos=uleb(d,pos);idx+=delta
                    c['fields'].append({**fields[idx],'access':flags})
            for count in counts[2:]:
                idx=0
                for _ in range(count):
                    delta,pos=uleb(d,pos);flags,pos=uleb(d,pos);code,pos=uleb(d,pos);idx+=delta
                    m={**methods[idx],'access':flags,'codeOffset':code,'instructionUnits':0}
                    if code:
                        regs,ins,outs,tries,debug,units=struct.unpack_from('<4H2I',d,code)
                        m.update(registers=regs,instructionUnits=units,debugInfoOffset=debug)
                        assert code+16+2*units<=len(d);body_count+=1;instruction_units+=units
                    c['methods'].append(m)
        classes.append(c)
    result={'file':path.name,'header':hdr,'sha1Matches':hashlib.sha1(d[32:]).digest()==d[12:32],
        'adler32Matches':zlib.adler32(d[12:])&0xffffffff==struct.unpack_from('<I',d,8)[0],
        'declaredMethods':sum(len(c['methods']) for c in classes),'declaredFields':sum(len(c['fields']) for c in classes),
        'methodBodies':body_count,'instructionUnits':instruction_units,'classes':classes}
    write(PROJECT/'local/dex'/f'{path.stem}.json',result)
    write(PROJECT/'local/dex-strings'/f'{path.stem}.json',strings)
    return {k:v for k,v in result.items() if k!='classes'},classes

def elf_metadata(path):
    d=path.read_bytes();assert d[:4]==b'\x7fELF'
    wide=d[4]==2;end='<' if d[5]==1 else '>'
    fmt=end+('HHIQQQIHHHHHH' if wide else 'HHIIIIIHHHHHH')
    h=struct.unpack_from(fmt,d,16);machine=h[1];shoff=h[5];shsize=h[10];shnum=h[11];strid=h[12]
    sfmt=end+('IIQQQQIIQQ' if wide else 'IIIIIIIIII')
    sections=[]
    for i in range(shnum):
        v=struct.unpack_from(sfmt,d,shoff+i*shsize)
        sections.append(dict(zip(['nameOff','type','flags','address','offset','bytes','link','info','alignment','entryBytes'],v)))
    ns=sections[strid];names=d[ns['offset']:ns['offset']+ns['bytes']]
    def cstr(b,off):return b[off:b.index(0,off)].decode('utf-8',errors='replace')
    for s in sections:s['name']=cstr(names,s['nameOff'])
    symbols=[];needed=[]
    for s in sections:
        if s['type'] in [2,11] and s['entryBytes']:
            st=sections[s['link']];strings=d[st['offset']:st['offset']+st['bytes']]
            for i in range(s['bytes']//s['entryBytes']):
                off=s['offset']+i*s['entryBytes']
                if wide:n,info,other,idx,value,size=struct.unpack_from(end+'IBBHQQ',d,off)
                else:n,value,size,info,other,idx=struct.unpack_from(end+'IIIBBH',d,off)
                if n: symbols.append({'name':cstr(strings,n),'value':value,'bytes':size,'type':info&15,
                    'binding':info>>4,'sectionID':idx,'table':s['name']})
        if s['type']==6:
            st=sections[s['link']];strings=d[st['offset']:st['offset']+st['bytes']]
            fmt=end+('qQ' if wide else 'iI');size=struct.calcsize(fmt)
            for off in range(s['offset'],s['offset']+s['bytes'],size):
                tag,value=struct.unpack_from(fmt,d,off)
                if tag==1:needed.append(cstr(strings,value))
    result={'file':str(path.relative_to(PROJECT/'local/apk')),'bitness':64 if wide else 32,
        'machine':machine,'needed':needed,'sections':sections,'symbols':symbols,'bytes':len(d)}
    write(PROJECT/'local/elf'/path.parent.name/f'{path.name}.json',result)
    return {k:v for k,v in result.items() if k not in ['sections','symbols']}, {'symbols':len(symbols),
        'definedFunctions':sum(s['type']==2 and s['sectionID']!=0 for s in symbols),
        'hasSymtab':any(s['name']=='.symtab' for s in sections),
        'hasDebug':any(s['name'].startswith('.debug') for s in sections)}

def main():
    apk=PROJECT/'local/apk';dex=[];packages=collections.Counter();defined_classes=[];unsupported=[]
    for path in sorted(apk.rglob('*.dex')):
        if path.open('rb').read(4)!=b'dex\n':
            unsupported.append({'file':path.relative_to(apk).as_posix(),'bytes':path.stat().st_size,
                'header':path.open('rb').read(16).hex(),'status':'nonstandard-container; retained for further loader analysis'})
            continue
        summary,classes=dex_metadata(path);dex.append(summary)
        defined_classes.extend(c['name'] for c in classes)
        packages.update('/'.join(c['name'].strip('L;').split('/')[:3]) for c in classes)
        print(path.name,len(classes),'classes',flush=True)
    elf=[]
    for path in sorted(apk.glob('lib/*/*.so')):
        a,b=elf_metadata(path);elf.append({**a,**b})
    summary={'dexFiles':len(dex),'dexClasses':sum(x['header']['classCount'] for x in dex),
        'dexDeclaredMethods':sum(x['declaredMethods'] for x in dex),'dexDeclaredFields':sum(x['declaredFields'] for x in dex),
        'dexMethodBodies':sum(x['methodBodies'] for x in dex),'dexInstructionUnits':sum(x['instructionUnits'] for x in dex),
        'dexChecksumsAllMatch':all(x['sha1Matches'] and x['adler32Matches'] for x in dex),
        'dexUniqueClasses':len(set(defined_classes)),'topPackages':dict(packages.most_common(45)),
        'nativeLibraries':len(elf),'nativeABI':dict(collections.Counter(Path(x['file']).parent.name for x in elf)),
        'nonstandardDEX':unsupported}
    write(PROJECT/'evidence/structure-summary.json',summary)
    write(PROJECT/'evidence/dex-summary.json',dex);write(PROJECT/'evidence/native-summary.json',elf)
    print(json.dumps(summary,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
