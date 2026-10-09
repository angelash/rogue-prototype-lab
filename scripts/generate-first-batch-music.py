"""Render one project's original, deterministic prototype music loop; no external media."""
import argparse
import hashlib
import json
import math
import random
import struct
import wave
from array import array
from pathlib import Path
from create_first_batch_config import PROJECTS

ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--project',required=True,choices=PROJECTS)
a=p.parse_args();folder,product,title=PROJECTS[a.project]
settings={'032':(96,60,13032),'051':(104,55,13051),'027':(108,57,13027),'121':(88,50,13121),'126':(90,60,13126)}
bpm,base,seed=settings[a.project];rate=48000;beat=60/bpm;duration=32*beat
samples=array('f',[0])*round(duration*rate);rng=random.Random(seed)
motif=[0,4,7,9,7,4,2,4,0,4,7,12,9,7,4,2]
chords=[0,5,7,0,0,5,7,0]
def voice(at,length,note,amp,decay):
    freq=440*2**((note-69)/12);start=round(at*rate);end=min(len(samples),start+round(length*rate))
    for i in range(start,end):
        t=(i-start)/rate
        envelope=min(1,t/.012)*math.exp(-decay*t)*min(1,(end-i)/(rate*.045))
        samples[i]+=amp*envelope*(math.sin(math.tau*freq*t)+.18*math.sin(math.tau*freq*2*t))
for bar,chord in enumerate(chords):
    t=bar*4*beat
    for interval in (0,4,7):voice(t,4*beat,base-12+chord+interval,.018,.7)
    for b in (0,2):voice(t+b*beat,beat,base-24+chord,.05,4)
    for b in range(4):voice(t+b*beat,beat*.8,base+motif[(bar*4+b)%len(motif)],.055+rng.uniform(-.004,.004),4.5)
peak=max(abs(x) for x in samples);gain=min(1,.3/peak)
pcm=b''.join(struct.pack('<h',round(max(-1,min(1,x*gain))*32767)) for x in samples)
out=ROOT/'assets'/folder/'audio/original/loop.wav';out.parent.mkdir(parents=True,exist_ok=True)
temp=out.with_suffix('.candidate.wav')
with wave.open(str(temp),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(pcm)
data=temp.read_bytes()
if out.exists() and out.read_bytes()!=data:raise SystemExit('Refusing to overwrite different loop: '+str(out))
if not out.exists():out.write_bytes(data)
temp.unlink()
runtime=ROOT/'prototypes'/folder/'game'/product/'Assets'/product/'Resources/Audio/music.wav'
runtime.parent.mkdir(parents=True,exist_ok=True)
if runtime.exists() and runtime.read_bytes()!=data:raise SystemExit('Refusing different runtime music')
runtime.write_bytes(data)
record={'version':'0.2','creator':'Original project composition and standard-library synthesis','thirdPartyMedia':False,'seed':seed,'bpm':bpm,'midiBase':base,'motif':motif,'chords':chords,'sampleRate':rate,'channels':1,'pcmBits':16,'seconds':duration,'peakDbFS':20*math.log10(peak*gain),'sha256':hashlib.sha256(data).hexdigest(),'source':str(out.relative_to(ROOT)).replace('\\','/'),'runtime':str(runtime.relative_to(ROOT)).replace('\\','/'),'listeningQA':'Candidate; Player ear/headphone mix still needs review'}
(out.parent/'music-register.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print('ORIGINAL_MUSIC',a.project,'seconds='+str(duration),'peakDbFS='+str(record['peakDbFS']))
