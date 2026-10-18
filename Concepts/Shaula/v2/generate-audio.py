"""Original Shaula concept audio; reuses the existing synth helpers, never edits Assets."""
from pathlib import Path
import hashlib
import importlib.util
import json
import math
import wave
import numpy as np

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
spec=importlib.util.spec_from_file_location('game_synth',ROOT/'Tools/GenerateGameAudio.py')
synth=importlib.util.module_from_spec(spec);spec.loader.exec_module(synth)
RATE=synth.RATE
AUDIO=OUT/'audio';AUDIO.mkdir(exist_ok=True)
rng=np.random.default_rng(20261018)

def timeline(seconds): return np.arange(round(seconds*RATE))/RATE
def chirp(seconds,start,end,gain=.2,decay=8):
    t=timeline(seconds);f=end+(start-end)*np.exp(-t*14)
    return gain*np.sin(2*math.pi*np.cumsum(f)/RATE)*np.exp(-t*decay)
def noise(seconds,width=9):
    x=rng.normal(0,1,round(seconds*RATE))
    return np.convolve(x,np.ones(width)/width,mode='same')
def layer(buf,sound,at=0,gain=1):
    offset=round(at*RATE);length=min(len(sound),len(buf)-offset)
    if length>0:buf[offset:offset+length]+=sound[:length]*gain
def click(seconds=.13,pitch=1100):
    t=timeline(seconds)
    return (synth.tone(pitch,seconds,'bell',.16)+noise(seconds,3)*np.exp(-t*90)*.2)
def thump(seconds=.5,pitch=150):
    t=timeline(seconds)
    return chirp(seconds,pitch,48,.55,13)+noise(seconds,27)*np.exp(-t*12)*.6

sounds={};descriptions={}
def put(name,sound,label,role):
    sound=synth.soften_edges(np.asarray(sound,dtype=float),.004)
    peak=float(np.max(np.abs(sound)))
    if peak>.72:sound*=.72/peak
    sounds[name]=sound;descriptions[name]=dict(label=label,role=role)

put('step-a',click(.16,690)+thump(.16,95)*.22,'Шаг · сухой хитин A','Quiet; use variants sparsely, not eight simultaneous leg clicks')
put('step-b',click(.16,810)+thump(.16,110)*.18,'Шаг · сухой хитин B','Alternative to step-a')
t=timeline(1.65);x=noise(1.65,41)*np.sin(math.pi*t/1.65)**2*.24
layer(x,thump(.5,120),.15);layer(x,click(.3,730),.8);layer(x,click(.3,570),1.03)
put('arrival',x,'Появление Шаулы','Heavy shell movement and muted low impact, no creature scream')
t=timeline(.4);x=noise(.4,7)*np.sin(math.pi*t/.4)**2*.14
layer(x,click(.15,900),0);layer(x,click(.15,620),.19)
put('claw-ready',x,'Клешни · подготовка','Dry mechanical shell scrape; warning before grab')
put('claw-hit',thump(.36,210)+click(.36,800)*.65,'Клешни · удар','Dry punch, no flesh or wet crunch')
x=thump(.7,170);layer(x,click(.22,480),.04,.8);layer(x,click(.22,910),.13,.35)
put('ground-slam',x,'Удар по земле','Low thud and dry stone chips; short enough for repeated attacks')
t=timeline(1.5);f=240+1350*(t/1.5)**1.5;phase=2*math.pi*np.cumsum(f)/RATE
x=(np.sin(phase)+.17*np.sin(phase*2.01))*(.03+.22*(t/1.5)**2)
x+=noise(1.5,13)*.08*(t/1.5)**2
put('laser-charge',x,'Лазер · заряд 1,5 с','Distinct rising warning, follows the complete charge window')
t=timeline(.45);x=chirp(.45,2100,130,.2,19)+thump(.45,180)*.7
x+=noise(.45,3)*np.exp(-t*65)*.45
x+=np.sin(2*math.pi*690*t+3.2*np.sin(2*math.pi*83*t))*np.exp(-t*25)*.13
put('laser-fire',x,'Лазер · мощный белый разряд','Immediate dry crack plus low body; no long piercing whistle')
t=timeline(.55);x=thump(.55,130)*.65+synth.tone(1240,.55,'bell',.11)+synth.tone(1910,.55,'bell',.05)
put('laser-impact',x,'Лазер · попадание','Short impact and pale magical ring; quieter than the shot')
x=np.zeros(round(.65*RATE));layer(x,click(.2,1700),0);layer(x,click(.2,910),.07,.65)
layer(x,chirp(.48,1300,330,.13,8),.12)
put('tail-break',x,'Хвост · исчезновение','Dry magical snap and descending shimmer, no bodily injury sound')
x=np.zeros(round(1.82*RATE))
for at,note in zip([0,.3,.54,.76,.98],[57,62,65,69,74]):layer(x,synth.tone(synth.hz(note),.55,'bell',.13),at)
layer(x,thump(.6,110),1.22,.55)
put('tail-regrow',x,'Хвост · регенерация','Ascending segments; shockwave accent at 1.22 seconds')
t=timeline(1.5);x=noise(1.5,39)*np.sin(math.pi*t/1.5)**2*.22+chirp(1.5,70,150,.13,1.5)
layer(x,synth.tone(660,1.05,'bell',.12),.4);layer(x,click(.25,870),.85)
put('rage',x,'Багровая ярость','Low swell, shell click and clear phase-change tone; no scream')
x=np.zeros(round(.85*RATE))
for at,note in [(0,77),(.16,81),(.32,86)]:layer(x,synth.tone(synth.hz(note),.5,'bell',.14),at)
put('star-cast',x,'Звёздный обстрел','Airy three-note star signal; distinct from laser charge')
put('needle',chirp(.15,1600,720,.12,25)+noise(.15,5)*.07*np.exp(-timeline(.15)*35),'Магическая игла','Short light tick, much smaller than the main laser')
t=timeline(.32);x=noise(.32,7)*np.sin(math.pi*t/.32)**2*.28
layer(x,click(.14,610),.17,.65)
put('sting',x,'Жало · выпад','Air swish and dry ground contact, no punctured flesh')
put('armor-block',click(.18,920)*.45,'Панцирь · попадание','Quiet dull shell tick; no constant loud ringing')
x=np.zeros(round(2.1*RATE))
for at,note in [(0,81),(.18,77),(.4,74),(.7,69),(1.25,86),(1.48,89)]:layer(x,synth.tone(synth.hz(note),.6,'bell',.12),at)
put('transform',x,'Финальное превращение','Gentle descending magic then two light tiny-scorpion clicks; no death cry')

records=[]
for name,sound in sounds.items():
    path=AUDIO/(name+'.wav');synth.write_wav(path,sound)
    records.append(dict(file='audio/'+path.name,**descriptions[name],seconds=len(sound)/RATE,
                        peak=float(np.max(np.abs(sound))),rms=float(np.sqrt(np.mean(sound**2))),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))

# Two original, sparse 8-bar loops. No external samples or existing game music copied.
beat=60/104;length=round(32*beat*RATE)
for phase in [1,2]:
    buffer=np.zeros((length,2))
    roots=[45,45,41,43,45,48,41,43]
    for bar,base in enumerate(roots):
        for step in [0,2]:synth.add(buffer,synth.tone(synth.hz(base-12),beat*1.5,'bass',.10),beat*(bar*4+step),loop=True)
        for step in range(4):
            note=[base+12,base+19,base+15,base+19][step]
            synth.add(buffer,synth.tone(synth.hz(note),beat*1.8,'pluck',.045),beat*(bar*4+step),pan=(-.2 if step%2 else .2),loop=True)
            synth.add(buffer,thump(.16,105)*.13,beat*(bar*4+step),loop=True)
        if phase==2:
            for step in range(8):synth.add(buffer,click(.07,1200)*.09,beat*(bar*4+step*.5),pan=.25,loop=True)
    buffer=synth.soften_edges(buffer,.008)
    peak=np.max(np.abs(buffer))
    if peak>.4:buffer*=.4/peak
    path=AUDIO/f'music-phase-{phase}.wav';synth.write_wav(path,buffer)
    records.append(dict(file='audio/'+path.name,label=f'Музыка · фаза {phase}',role='Original optional 8-bar fantasy loop; preview separately, not installed in game',
                        seconds=len(buffer)/RATE,bpm=104,bars=8,peak=float(np.max(np.abs(buffer))),rms=float(np.sqrt(np.mean(buffer**2))),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))

demo=np.zeros((round(25*RATE),2))
events=[(0,'arrival'),(2,'step-a'),(2.3,'step-b'),(3,'claw-ready'),(3.52,'claw-hit'),(4.5,'ground-slam'),
        (6,'laser-charge'),(7.5,'laser-fire'),(7.52,'laser-impact'),(10,'tail-break'),(11.2,'tail-regrow'),
        (14,'rage'),(16,'star-cast'),(17,'needle'),(17.14,'needle'),(17.28,'needle'),(17.42,'needle'),(17.56,'needle'),(17.7,'needle'),
        (19,'sting'),(20,'armor-block'),(22,'transform')]
for at,name in events:synth.add(demo,sounds[name],at,pan=0)
peak=np.max(np.abs(demo))
if peak>.8:demo*=.8/peak
synth.write_wav(AUDIO/'demo.wav',demo)
report=dict(method='Original deterministic synthesis; reuses existing Tools/GenerateGameAudio.py helpers, no samples or recordings',
            rate=RATE,pcmBits=16,gameIntegrated=False,autoplay=False,listeningReviewPerformed=False,
            sfxCount=len(sounds),musicLoopCount=2,clips=records,demo=dict(file='audio/demo.wav',seconds=25,events=events))
(OUT/'audio-manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n','utf-8')
for record in records:
    with wave.open(str(OUT/record['file']),'rb')as wav:
        samples=np.frombuffer(wav.readframes(wav.getnframes()),dtype='<i2')/32767
        assert wav.getframerate()==RATE and wav.getsampwidth()==2 and len(samples)>0
        assert np.max(np.abs(samples))<.99 and np.isfinite(samples).all()
        assert abs(samples[0])<.005 and abs(samples[-1])<.005
print(json.dumps(dict(result='PASS',sfx=len(sounds),musicLoops=2,demoSeconds=25,clipping=False,gameChanged=False),indent=2))
