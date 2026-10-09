"""Create only one requested, independent Unity project using the checked local baseline."""
import argparse
import hashlib
import json
from pathlib import Path
from create_first_batch_config import PROJECTS

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--project', required=True, choices=PROJECTS)
args = parser.parse_args()
folder, product, title = PROJECTS[args.project]
project = ROOT / 'prototypes' / folder / 'game' / product
baseline = ROOT / 'prototypes/proto-013-ring-toss/game/RingTossWorkshop'
records = []

def put(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() != data:
        raise SystemExit('Refusing to overwrite different existing file: ' + str(path))
    if not path.exists():
        path.write_bytes(data)
    records.append({'path': str(path.relative_to(project)).replace('\\', '/'),
                    'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})

put(project/'Packages/manifest.json', (baseline/'Packages/manifest.json').read_bytes())
put(project/'ProjectSettings/ProjectVersion.txt', (baseline/'ProjectSettings/ProjectVersion.txt').read_bytes())
for source in sorted((baseline/'ProjectSettings').glob('*.asset')):
    text = source.read_text(encoding='utf-8-sig')
    if source.name == 'ProjectSettings.asset':
        text = text.replace('Ring Toss Workshop Prototype', title + ' Prototype')
        for key in ('cloudProjectId', 'organizationId', 'projectName'):
            import re
            text = re.sub(r'(?m)^(  '+key+r':).*$', r'\1', text)
    put(project/'ProjectSettings'/source.name, text.encode('utf-8'))
assets = project/'Assets'/product
for kind, glob in (('Fonts','*.otf'), ('Audio','*.wav')):
    for source in sorted((baseline/'Assets/RingToss/Resources'/kind).glob(glob)):
        put(assets/'Resources'/kind/source.name, source.read_bytes())
for source in sorted((baseline/'Assets/StreamingAssets/ThirdPartyNotices').glob('*.txt')):
    put(project/'Assets/StreamingAssets/ThirdPartyNotices'/source.name, source.read_bytes())
put(assets/'Support/Prototype.Support.asmdef', b'{"name":"Prototype.Support","references":[],"autoReferenced":true}\n')
put(assets/'Support/PrototypeToolkit.cs', (ROOT/'scripts/unity-templates/PrototypeToolkit.cs').read_bytes())
builder = (ROOT/'scripts/unity-templates/ProjectBuilder.cs.txt').read_text(encoding='utf-8')
put(assets/'Editor/ProjectBuilder.cs', builder.replace('__PRODUCT__', product).replace('__TITLE__', title).encode('utf-8'))
manifest = {'version':'1.0', 'project':args.project, 'unityVersion':'2022.3.62f3c1',
            'baseline':'Project-owned 013 settings/modules; no Library, account, product or gameplay copied',
            'font':'Unmodified Noto Sans CJK SC 2.004, Adobe OFL; notices copied into StreamingAssets',
            'audio':'Seven project-original procedural short sounds; event mappings are project specific',
            'files':records}
out = ROOT/'.local/scaffolds'/(args.project+'.json')
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print('PROJECT_READY', str(project), 'files='+str(len(records)))
