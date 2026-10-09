"""Archive actual Unity reports, project bytes and optional camera evidence for one project."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
from create_first_batch_config import PROJECTS
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--project',required=True,choices=PROJECTS)
p.add_argument('--tests',type=Path,required=True)
p.add_argument('--capture',type=Path)
p.add_argument('--window-capture',type=Path)
p.add_argument('--input-log',type=Path)
a=p.parse_args();folder,product,_=PROJECTS[a.project]
project=ROOT/'prototypes'/folder/'game'/product
out=ROOT/'docs/development'/('proto-'+a.project)/'evidence';out.mkdir(parents=True,exist_ok=True)
xml=a.tests.read_bytes();run=ET.fromstring(xml)
if run.attrib.get('result')!='Passed' or int(run.attrib.get('failed','1'))!=0:raise SystemExit('Cannot archive failed tests as pass')
build=project/'Builds/Windows64'
summary=json.loads((build/'build-summary.json').read_text(encoding='utf-8-sig'))
if summary['result']!='Succeeded' or summary['errors']:raise SystemExit('Build failed')
def entry(path,base):
    data=path.read_bytes();return {'path':str(path.relative_to(base)).replace('\\','/'),'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()}
sources=[]
for dirname in ('Assets','Packages','ProjectSettings'):
    sources.extend(entry(f,project) for f in sorted((project/dirname).rglob('*')) if f.is_file())
manifest={'project':a.project,'mode':'Windows x64 Mono StrictMode standalone','summary':summary,
          'tests':{k:run.attrib.get(k) for k in ('total','passed','failed','result','start-time','end-time')},
          'files':[entry(f,build) for f in sorted(build.rglob('*')) if f.is_file()],
          'projectSources':sources,'productionTools':[entry(ROOT/'scripts'/f,ROOT) for f in ('create-first-batch-project.py','create_first_batch_config.py','run-unity-project.ps1','capture-project-scene.ps1','generate-first-batch-music.py','register-project-resources.py','record-project-evidence.py','unity-templates/ProjectBuilder.cs.txt','unity-templates/PrototypeToolkit.cs')]}
(out/'test-results.xml').write_bytes(xml)
if a.capture:
    data=a.capture.read_bytes();(out/'scene.png').write_bytes(data)
    manifest['cameraEvidence']={'path':'scene.png','sha256':hashlib.sha256(data).hexdigest(),'cameraOnly':True,'nativeInputQA':False}
if a.window_capture:
    data=a.window_capture.read_bytes();(out/'player-window.png').write_bytes(data)
    manifest['windowEvidence']={'path':'player-window.png','sha256':hashlib.sha256(data).hexdigest(),'cameraOnly':False,'nativeInputQA':'see native-qa.json and input-events.log'}
if a.input_log:
    # Archive game-owned state transitions; avoid unrelated machine/device output.
    lines=[line for line in a.input_log.read_text(encoding='utf-8-sig',errors='replace').splitlines() if line.startswith(('GAME_COMMAND ','SAVE_CHECKPOINT ','LOAD_CHECKPOINT ','PLAYER_READY '))]
    (out/'input-events.log').write_text('\n'.join(lines)+'\n',encoding='utf-8')
(out/'build-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('EVIDENCE',a.project,manifest['tests'],'build',summary,'sources',len(sources))
