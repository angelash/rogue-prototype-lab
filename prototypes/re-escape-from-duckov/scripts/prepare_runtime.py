"""Prepare the native-loading Unity host with original matching managed DLLs."""
import hashlib, json, re, shutil
from pathlib import Path

def main():
    project=Path(__file__).resolve().parents[1];local=project/'local';host=local/'UnityProject'
    exporter=local/'AssetRipperExport/ExportedProject';plugins=host/'Assets/Plugins';plugins.mkdir(exist_ok=True)
    provenance=[]
    for exported in (exporter/'Assets/Plugins').glob('*.dll'):
        original=local/'build-copy/Duckov_Data/Managed'/exported.name
        source=original if original.exists() else exported
        shutil.copy2(source,plugins/exported.name)
        meta=exported.with_suffix('.dll.meta').read_text(encoding='utf-8')
        meta=meta.replace('Editor: Editor\n    second:\n      enabled: 0','Editor: Editor\n    second:\n      enabled: 1')
        (plugins/(exported.name+'.meta')).write_text(meta,encoding='utf-8')
        provenance.append({'file':exported.name,'source':str(source.relative_to(local)),'sha256':hashlib.sha256(source.read_bytes()).hexdigest()})
    (host/'Assets/Runtime/DuckovRecovery.Runtime.asmdef').write_text(json.dumps({'name':'DuckovRecovery.Runtime','autoReferenced':True}),encoding='utf-8')
    (host/'Assets/Editor/DuckovRecovery.Editor.asmdef').write_text(json.dumps({'name':'DuckovRecovery.Editor','references':['DuckovRecovery.Runtime'],'includePlatforms':['Editor']}),encoding='utf-8')
    # Keep the recovered built-in modules list; no editor package replacement of runtime DLLs.
    shutil.copy2(exporter/'Packages/manifest.json',host/'Packages/manifest.json')
    (local/'runtime-assembly-inputs.json').write_text(json.dumps(provenance,indent=2),encoding='utf-8')
    print('Prepared original matching runtime DLLs:',len(provenance))

if __name__=='__main__':main()
