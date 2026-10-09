"""Verify original audio/font copies and preserve per-project source provenance."""
import argparse,hashlib,json
from pathlib import Path
from create_first_batch_config import PROJECTS
root=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',required=True,choices=PROJECTS);a=p.parse_args()
folder,product,_=PROJECTS[a.project];project=root/'prototypes'/folder/'game'/product
resources=project/'Assets'/product/'Resources';records=[]
def record(source,runtime,rights):
    left=source.read_bytes();right=runtime.read_bytes()
    if left!=right:raise SystemExit('Runtime copy differs: '+str(runtime))
    records.append({'source':str(source.relative_to(root)).replace('\\','/'),'runtime':str(runtime.relative_to(root)).replace('\\','/'),'bytes':len(right),'sha256':hashlib.sha256(right).hexdigest(),'rights':rights})
for source in sorted((root/'assets/proto-013-ring-toss/audio/original').glob('*.wav')):
    record(source,resources/'Audio'/source.name,'Original project procedural sound; synthesis source scripts/generate-prototype-audio.py. No third-party media.')
record(root/'assets'/folder/'audio/original/loop.wav',resources/'Audio/music.wav','Original fixed-score composition; synthesis script and music-register.json preserve parameters/seed; candidate mix.')
record(root/'prototypes/proto-013-ring-toss/game/RingTossWorkshop/Assets/RingToss/Resources/Fonts/NotoSansCJKsc-Regular.otf',resources/'Fonts/NotoSansCJKsc-Regular.otf','Unmodified Noto Sans CJK SC 2.004; Adobe 2014-2021, OFL1.1; original readonly highschool source. Upstream https://github.com/notofonts/noto-cjk ; notices preserved in this project StreamingAssets/ThirdPartyNotices.')
notices=[]
for f in sorted((project/'Assets/StreamingAssets/ThirdPartyNotices').glob('*.txt')):
    notices.append({'path':str(f.relative_to(root)).replace('\\','/'),'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
out=root/'sources/art'/folder/'runtime-resource-register.json';out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps({'project':a.project,'version':'0.2.0','date':'2026-10-10','copies':records,'notices':notices,'eventAuthority':'Actual project Presentation runtime and committed Core events; no reward authority in audio','audioQA':'File/copy/decode and playback invocation are distinct from actual listening; see development record.'},ensure_ascii=False,indent=2),encoding='utf-8')
print('RESOURCE_REGISTER',a.project,'copies',len(records))
