"""Translate the six local Unity 4 quality records into the initialized Unity 2022 asset."""
import json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
PROJECT=ROOT/'local/UnityProject'
def restore():
    p=PROJECT/'ProjectSettings/QualitySettings.asset'
    if not p.exists():raise RuntimeError('Initialize the Unity project before restoring quality tiers')
    text=p.read_text('utf-8');manifest=json.loads((PROJECT/'Assets/Editor/RecoverySource/manifest.json').read_text('utf-8'))
    source=next(o['tree'] for o in manifest['objects'] if o['type']=='QualitySettings')
    blocks=re.findall(r'  - serializedVersion: 3\n.*?(?=  - serializedVersion: 3\n|  m_TextureMipmapLimitGroupNames:)',text,re.S)
    assert len(blocks)==len(source['m_QualitySettings'])==6
    mapping={'blendWeights':'skinWeights','textureQuality':'globalTextureMipmapLimit'}
    for block,row in zip(blocks,source['m_QualitySettings']):
        replacement=block
        for key,value in row.items():
            key=mapping.get(key,key);value=int(value) if isinstance(value,bool) else value
            replacement,count=re.subn(r'(?m)^    '+re.escape(key)+r': .*$', '    '+key+': '+str(value),replacement)
            assert count==1,key
        text=text.replace(block,replacement,1)
    text=re.sub(r'm_CurrentQuality: \d+', 'm_CurrentQuality: 0',text)
    text=re.sub(r'(?m)^(    Standalone:) \d+',r'\1 0',text)
    p.write_text(text,encoding='utf-8',newline='\n');print('source six quality tiers restored')
if __name__=='__main__':restore()
