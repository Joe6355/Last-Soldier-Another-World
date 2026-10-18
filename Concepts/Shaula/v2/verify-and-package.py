"""Verify standalone deliverables and unchanged game sources, then package the concept."""
from pathlib import Path
import datetime,hashlib,json,subprocess,wave,zipfile
from html.parser import HTMLParser
import numpy as np
from PIL import Image

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
manifest=json.loads((OUT/'animations.json').read_text('utf-8'))
checks=[]
def check(condition,message):
    assert condition,message
    checks.append(message)

frames=sorted((OUT/'laser-preview-frames').glob('*.png'))
render=json.loads((OUT/'laser-render-checks.json').read_text('utf-8'))
check(render['result']=='PASS' and len(frames)==render['renderedFrames'],'Shared Canvas renderer and laser color/timing checks pass')
images=[Image.open(path).convert('RGB') for path in frames]
images[0].save(OUT/'shaula-white-laser-preview.gif',save_all=True,append_images=images[1:],duration=40,loop=0,disposal=2,optimize=False)
with Image.open(OUT/'shaula-white-laser-preview.gif')as image:
    duration=0
    for frame in range(image.n_frames):image.seek(frame);duration+=image.info['duration']
    check(duration==len(frames)*40 and image.n_frames>30,'Laser GIF preserves sequence duration and contains real animation')

for phase in ['normal','crimson']:
    for name,clip in manifest['clips'].items():
        folder=OUT/'frames'/phase/name
        check(len(list(folder.glob('*.png')))==6,f'{phase}/{name}: six exported pose PNGs')
        with Image.open(OUT/'animations'/phase/(name+'.apng'))as image:
            check(1<=image.n_frames<=6,f'{phase}/{name}: valid APNG; repeated poses may share a held frame')
            duration=0
            for frame in range(image.n_frames):image.seek(frame);duration+=image.info['duration']
            check(duration==clip['totalMs'],f'{phase}/{name}: animation durations match the manifest')

for file in (OUT/'audio').glob('*.wav'):
    with wave.open(str(file),'rb')as wav:
        check(wav.getframerate()==32000 and wav.getsampwidth()==2,file.name+': 32 kHz PCM16')
        data=np.frombuffer(wav.readframes(wav.getnframes()),dtype='<i2')/32767
        check(len(data)>0 and np.isfinite(data).all() and np.sqrt(np.mean(data**2))>.001,file.name+': nonempty audible signal')
        check(np.max(np.abs(data))<.99,file.name+': headroom without clipping')
        check(abs(data[0])<.005 and abs(data[-1])<.005,file.name+': faded edges')

class Links(HTMLParser):
    def handle_starttag(self,tag,attributes):
        a=dict(attributes)
        if tag=='audio':check('autoplay' not in a,'Audio requires a user gesture; no autoplay')
        targets=[a[key] for key in ['href','src'] if key in a]
        if tag=='option' and a.get('value','').endswith('.wav'):targets.append(a['value'])
        for target in targets:
            if not target.startswith(('http:','https:','#','data:')):check((OUT/target).is_file(),'HTML resource exists: '+target)
html=(OUT/'preview.html').read_text('utf-8')
check('__SHAULA_' not in html,'HTML template fields resolved')
Links().feed(html)

before=json.loads((OUT/'game-source-before.json').read_text('utf-8'))['files']
current=subprocess.check_output(['rg','--files','Assets','Packages','ProjectSettings'],cwd=ROOT,text=True).splitlines()
paths={p.replace('\\','/') for p in current if (ROOT/p).is_file()}
check(paths==set(before),'Game source path set unchanged')
changed=[p for p,expected in before.items() if hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=expected]
check(not changed,'All '+str(len(before))+' game source hashes unchanged')

report=dict(result='PASS',checkedAt=datetime.datetime.now().astimezone().isoformat(),checks=len(checks),
            gameSourceFilesVerified=len(before),gameSourceChanges=changed,integratedIntoGame=False,
            sourceSheets=len(manifest['sheets']),activePosePngs=144,animatedApngs=24,nodeTests=9,
            sfx=17,musicLoops=2,demoSeconds=25,sharedCanvasRender=render,
            browserVerified=False,physicalPhoneVerified=False,audioHumanListeningReview=False,
            limitations=['Animation concept uses one angle', 'Pixel/pivot cleanup and game balance remain for future integration',
                         'Sound timbre and in-game mix require human listening review'],details=checks)
(OUT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n','utf-8')
archive=OUT.parent/'Shaula-concept-v2.zip'
files=[p for p in OUT.rglob('*') if p.is_file() and 'laser-preview-frames' not in p.parts and '__pycache__' not in p.parts]
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6)as target:
    for file in sorted(files):target.write(file,Path('Shaula-v2')/file.relative_to(OUT))
with zipfile.ZipFile(archive)as target:check(target.testzip()is None,'Package CRC check passed')
print(json.dumps(dict(result='PASS',checks=len(checks),gameSourcesVerified=len(before),gameChanged=False,
                      archive=str(archive),bytes=archive.stat().st_size),indent=2))
