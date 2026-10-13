"""Read copied FMOD banks with no audio output and decode embedded FSB samples."""
import argparse, ctypes as C, json, struct, uuid, wave
from pathlib import Path
from prepare import write, sha

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--project',required=True);a=ap.parse_args()
    p=Path(a.project).resolve();local=p/'local';data=local/'build-copy/Duckov_Data'
    dll=C.CDLL(str(data/'Plugins/x86_64/fmodstudio.dll'))
    ptr=C.c_void_p;integer=C.c_int;uint=C.c_uint
    def fn(name,types):
        f=getattr(dll,name);f.argtypes=types;f.restype=integer;return f
    def ok(code):
        if code:raise RuntimeError(f'FMOD result {code}')
    system=ptr();core=ptr()
    ok(fn('FMOD_Studio_System_Create',[C.POINTER(ptr),uint])(C.byref(system),131848))
    ok(fn('FMOD_Studio_System_GetCoreSystem',[ptr,C.POINTER(ptr)])(system,C.byref(core)))
    ok(fn('FMOD_System_SetOutput',[ptr,integer])(core,2)) # NOSOUND; never plays an event.
    ok(fn('FMOD_Studio_System_Initialize',[ptr,integer,uint,uint,ptr])(system,128,4,0,None))
    records=[];strings=[];bank=ptr()
    try:
        load=fn('FMOD_Studio_System_LoadBankFile',[ptr,C.c_char_p,uint,C.POINTER(ptr)])
        for path in [data/'StreamingAssets/Master.strings.bank',data/'StreamingAssets/Master.bank']:
            handle=ptr();ok(load(system,str(path).encode('utf-8'),0,C.byref(handle)))
            count=integer()
            ok(fn('FMOD_Studio_Bank_GetStringCount',[ptr,C.POINTER(integer)])(handle,C.byref(count)))
            for i in range(count.value):
                buf=C.create_string_buffer(4096);guid=(C.c_ubyte*16)();retrieved=integer()
                ok(fn('FMOD_Studio_Bank_GetStringInfo',[ptr,integer,ptr,ptr,integer,C.POINTER(integer)])(handle,i,guid,buf,len(buf),C.byref(retrieved)))
                strings.append(dict(path=buf.value.decode('utf-8'),guid=str(uuid.UUID(bytes_le=bytes(guid)))))
            if path.name=='Master.bank':bank=handle
        count=integer();ok(fn('FMOD_Studio_Bank_GetEventCount',[ptr,C.POINTER(integer)])(bank,C.byref(count)))
        array=(ptr*count.value)();retrieved=integer()
        ok(fn('FMOD_Studio_Bank_GetEventList',[ptr,C.POINTER(ptr),integer,C.POINTER(integer)])(bank,array,count,C.byref(retrieved)))
        for handle in array[:retrieved.value]:
            buf=C.create_string_buffer(4096);got=integer()
            ok(fn('FMOD_Studio_EventDescription_GetPath',[ptr,ptr,integer,C.POINTER(integer)])(handle,buf,len(buf),C.byref(got)))
            rec=dict(path=buf.value.decode('utf-8'))
            for key,method in [('lengthMs','GetLength'),('parameterCount','GetParameterDescriptionCount'),('is3D','Is3D'),('isOneshot','IsOneshot'),('isSnapshot','IsSnapshot')]:
                value=integer();result=fn('FMOD_Studio_EventDescription_'+method,[ptr,C.POINTER(integer)])(handle,C.byref(value))
                rec[key]=value.value if result==0 else {'result':result}
            records.append(rec)
    finally:fn('FMOD_Studio_System_Release',[ptr])(system)
    write(local/'fmod-events.json',dict(events=records,strings=strings))
    bankraw=(data/'StreamingAssets/Master.bank').read_bytes();offset=bankraw.find(b'FSB5')
    if offset<0:raise ValueError('No embedded FSB5 sample bank')
    # FSB5 v1 has a 60-byte header. Limit to its declared tables and data.
    magic,version,samples,headers,names,payload,mode=struct.unpack_from('<4s6I',bankraw,offset)
    size=(60 if version==1 else 64)+headers+names+payload
    raw=bankraw[offset:offset+size]
    (local/'Master.samples.fsb').write_bytes(raw)
    from fmod_toolkit import sound_to_wav
    from fmod_toolkit.fmod import pyfmodex
    sample_system=pyfmodex.System();sample_system.output=pyfmodex.enums.OUTPUTTYPE.NOSOUND
    sample_system.init(2,pyfmodex.flags.INIT_FLAGS.NORMAL,None)
    sound=sample_system.create_sound(bytes(raw),pyfmodex.flags.MODE.OPENMEMORY,
        exinfo=pyfmodex.structure_declarations.CREATESOUNDEXINFO(length=len(raw),numchannels=2,defaultfrequency=48000))
    try:decoded=sound_to_wav(sound,'Master')
    finally:sound.release();sample_system.release()
    exports=[];dest=local/'exports/FMOD';dest.mkdir(parents=True,exist_ok=True)
    for i,(name,contents) in enumerate(decoded.items()):
        safe=Path(name).name.replace(':','_');f=dest/f'{i:04d}_{safe}'
        if f.suffix.lower()!='.wav':f=f.with_suffix('.wav')
        f.write_bytes(contents)
        with wave.open(str(f),'rb') as w:rec=dict(channels=w.getnchannels(),rate=w.getframerate(),frames=w.getnframes(),seconds=round(w.getnframes()/w.getframerate(),4))
        exports.append(dict(path=f.relative_to(local).as_posix(),bytes=f.stat().st_size,sha256=sha(f),**rec))
    write(local/'fmod-samples.json',exports)
    write(p/'evidence/audio-summary.json',dict(events=len(records),strings=len(strings),fsbOffset=offset,fsbVersion=version,
        declaredSamples=samples,decodedSamples=len(exports),sampleBytes=sum(x['bytes'] for x in exports),noAudioOutput=True,
        bankEvents=[r['path'] for r in records],eventSampleMapping='Not reconstructed; sample identity and event identity indexed separately'))
    print(f'FMOD: {len(records)} events; decoded {len(exports)}/{samples} samples',flush=True)

if __name__=='__main__':main()
