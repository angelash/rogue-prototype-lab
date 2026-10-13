"""Compare complete SDK bytecode dumps to independently parsed DEX records."""
import hashlib, json, re
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1];LOCAL=ROOT/'local'

def main():
    dex=json.loads((ROOT/'evidence/dex-summary.json').read_text('utf8'));records=[]
    for x in dex:
        source=LOCAL/'dex-disassembly'/(Path(x['file']).stem+'.txt');text=source.read_text('utf8',errors='replace')
        units=[int(v) for v in re.findall(r'insns size\s*:\s*(\d+) 16-bit code units',text)]
        classes=len(re.findall(r'^Class #\d+\s+-',text,re.M))
        assert classes==x['header']['classCount'] and len(units)==x['methodBodies']
        records.append({'file':x['file'],'classes':classes,'methodBodies':len(units),'instructionUnits':sum(units),'dumpBytes':source.stat().st_size,'dumpSHA256':hashlib.sha256(source.read_bytes()).hexdigest(),'emptyDEX':classes==0})
    log=(LOCAL/'logs/jadx.log').read_text('utf8',errors='replace');errors=re.findall(r'ERROR -\s+Method: (.+)',log)
    fallback=[]
    for x in dex:
        stem=Path(x['file']).stem;logfile=LOCAL/'logs'/('jadx-fallback-'+stem+'.log');text=logfile.read_text('utf8',errors='replace')
        empty=x['header']['classCount']==0
        status='empty-input' if empty and 'No classes for decompile' in text else 'ok' if 'ERROR -' not in text else 'failed'
        fallback.append({'file':x['file'],'status':status,'sources':len(list((LOCAL/'android-fallback'/stem).rglob('*.java')))})
    (LOCAL/'android-java/diagnostics.json').write_text(json.dumps({'normalErrors':errors,'fallback':fallback,'normalLog':log},ensure_ascii=False,indent=2),'utf8')
    evidence={'jadxVersion':'1.5.3','jadxReleaseURL':'https://github.com/skylot/jadx/releases/tag/v1.5.3','toolZipSHA256':'8280f3799c0273fe797a2bcd90258c943e451fd195f13d05400de5e6451d15ec','javaRuntime':'C:/Program Files/Java/jdk-17/bin/java.exe','normalJavaFiles':len(list((LOCAL/'android-java/sources').rglob('*.java'))),'normalMethodErrors':len(errors),'fallbackJavaFiles':sum(x['sources'] for x in fallback),'fallbackDEX':fallback,'disassemblyDEX':len(records),'disassemblyClasses':sum(x['classes'] for x in records),'disassemblyMethodBodies':sum(x['methodBodies'] for x in records),'disassemblyInstructionUnits':sum(x['instructionUnits'] for x in records),'allSDKCountsMatchIndependentDEXParser':True,'files':records,'scope':'Java is reconstructed and may be incomplete; all original bytecode and instruction dumps retained, including duplicate definitions and empty DEX.'}
    evidence['normalErrors']=int(re.search(r'finished with errors, count: (\d+)',log).group(1))
    evidence['explicitMethodErrorRecords']=evidence.pop('normalMethodErrors')
    (ROOT/'evidence/android-recovery.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),'utf8');print(json.dumps({k:v for k,v in evidence.items() if k not in ('files','fallbackDEX')},ensure_ascii=False))

if __name__=='__main__':main()
