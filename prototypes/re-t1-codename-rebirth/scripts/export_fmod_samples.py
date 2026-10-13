"""Export embedded FSB5 samples from RIFF bank SND chunks, offline."""
import hashlib, json, re, shutil, struct
from pathlib import Path
import fmod_toolkit

ROOT=Path(__file__).resolve().parents[1];LOCAL=ROOT/'local'

def main():
    index=json.loads((LOCAL/'payloads/index.json').read_text('utf8'));out=LOCAL/'assets/export/FMOD';out.mkdir(exist_ok=True);rows=[];errors=[]
    for bank in index:
        if not bank.get('FSB5Offsets'):continue
        d=(LOCAL/bank['input']).read_bytes()
        for ix,start in enumerate(bank['FSB5Offsets']):
            row={'input':bank['input'],'offset':start,'files':[]}
            try:
                chunk=next(c for c in bank['chunks'] if c['offset']<=start<c['offset']+c['bytes']);assert chunk['tag']=='SND '
                raw=d[start:chunk['offset']+chunk['bytes']];count=struct.unpack_from('<I',raw,8)[0]
                assert shutil.disk_usage(ROOT).free>2_000_000_000,'Keep room for version backup'
                name=re.sub(r'[^\w.-]','_',Path(bank['input']).stem);prefix=f'{len(rows):03}_{name}_{ix}'
                target=out/(prefix+'.fsb');target.write_bytes(raw)
                row['files'].append({'path':target.relative_to(LOCAL).as_posix(),'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest()})
                samples=fmod_toolkit.raw_to_wav(raw,prefix,2,44100,convert_pcm_float=False);assert len(samples)==count
                for filename,wav in samples.items():
                    assert wav[:4]==b'RIFF' and wav[8:12]==b'WAVE';dest=out/filename;dest.write_bytes(wav)
                    row['files'].append({'path':dest.relative_to(LOCAL).as_posix(),'bytes':len(wav),'sha256':hashlib.sha256(wav).hexdigest()})
                row.update(status='ok',sampleCount=count)
            except Exception as ex:row.update(status='failed',error=type(ex).__name__+': '+str(ex));errors.append(row.copy())
            rows.append(row)
    (out/'index.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),'utf8')
    evidence={'banksWithFSB5':len(rows),'passed':sum(x['status']=='ok' for x in rows),'failed':len(errors),'samples':sum(x.get('sampleCount',0) for x in rows),'exportedBytes':sum(f['bytes'] for x in rows for f in x['files']),'failures':errors,'scope':'Embedded FSB5 samples only; complete RIFF bank originals kept as TextAssets. No event/mixer runtime or listening test.'}
    (ROOT/'evidence/fmod-samples.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),'utf8');print(json.dumps(evidence,ensure_ascii=False),flush=True)

if __name__=='__main__':main()
