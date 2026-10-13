"""PE import/export inventory without running native game components."""
import argparse,struct
from pathlib import Path
from prepare import write,sha

def inspect_pe(path):
    raw=path.read_bytes()
    def u16(o):return struct.unpack_from('<H',raw,o)[0]
    def u32(o):return struct.unpack_from('<I',raw,o)[0]
    if raw[:2]!=b'MZ':return None
    pe=u32(0x3c)
    if raw[pe:pe+4]!=b'PE\0\0':return None
    machine=u16(pe+4);count=u16(pe+6);opt=pe+24;optlen=u16(pe+20)
    is64=u16(opt)==0x20b;dirs=opt+(112 if is64 else 96);sections=[]
    for i in range(count):
        o=opt+optlen+40*i
        sections.append(dict(name=raw[o:o+8].split(b'\0')[0].decode('ascii',errors='replace'),
             virtualSize=u32(o+8),rva=u32(o+12),bytes=u32(o+16),offset=u32(o+20)))
    def offset(rva):
        for s in sections:
            if s['rva']<=rva<s['rva']+max(s['virtualSize'],s['bytes']):return s['offset']+rva-s['rva']
        return rva if rva<len(raw) else None
    def string(o):return raw[o:raw.index(b'\0',o)].decode('utf-8',errors='replace')
    exports=[];imports=[]
    er=u32(dirs);ir=u32(dirs+8);clr=u32(dirs+14*8)
    if er:
        eo=offset(er);namecount=u32(eo+24);names=offset(u32(eo+32));ordinals=offset(u32(eo+36));functions=offset(u32(eo+28));base=u32(eo+16)
        for i in range(namecount):
            ordinal=u16(ordinals+2*i)
            exports.append(dict(name=string(offset(u32(names+4*i))),ordinal=base+ordinal,rva=u32(functions+4*ordinal)))
    if ir:
        io=offset(ir)
        while any(raw[io:io+20]):
            thunk=u32(io) or u32(io+16);name=string(offset(u32(io+12)));to=offset(thunk);symbols=[];width=8 if is64 else 4
            while True:
                value=struct.unpack_from('<Q' if is64 else '<I',raw,to)[0]
                if not value:break
                symbols.append(f'ordinal:{value&0xffff}' if value&(1<<(width*8-1)) else string(offset(value)+2));to+=width
            imports.append(dict(library=name,symbols=symbols));io+=20
    return dict(machine=f'0x{machine:04x}',bits=64 if is64 else 32,managed=bool(clr),sections=sections,exports=exports,imports=imports)

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);a=ap.parse_args();p=Path(a.project).resolve();copy=p/'local/build-copy';rows=[]
    for f in sorted(copy.rglob('*')):
        if f.suffix.lower() not in ('.exe','.dll'):continue
        info=inspect_pe(f)
        if info and not info['managed']:rows.append(dict(path=f.relative_to(copy).as_posix(),sha256=sha(f),bytes=f.stat().st_size,**info))
    write(p/'local/native-pe.json',rows)
    write(p/'evidence/native-summary.json',[dict(path=x['path'],bits=x['bits'],bytes=x['bytes'],sha256=x['sha256'],exports=len(x['exports']),imports=sum(len(d['symbols']) for d in x['imports'])) for x in rows])
    print(f'Native PE inventory: {len(rows)} files')

if __name__=='__main__':main()
