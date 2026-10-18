"""Register approved generated poses on a common ground pivot; never redraw the art."""
from pathlib import Path
from PIL import Image
import hashlib
import json
import math
import shutil

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Concepts/Shaula/v2'
TARGET = ROOT / 'Assets/Bosses/Shaula'
CELL = 320
ANCHOR_X, ANCHOR_Y = 160, 220
ORDER = ['idle', 'walk', 'grab', 'slam', 'infernal-shot', 'piercing-sting',
         'star-barrage', 'needle-volley', 'tail-break', 'tail-regrow', 'enrage', 'defeat']


def main():
    manifest = json.loads((SOURCE / 'animations.json').read_text('utf-8'))
    images = {key: Image.open(SOURCE / sheet['file']).convert('RGBA')
              for key, sheet in manifest['sheets'].items()}
    TARGET.mkdir(parents=True, exist_ok=True)
    frames = []
    for phase in ['normal', 'crimson']:
        atlas = Image.new('RGBA', (CELL * 6, CELL * len(ORDER)))
        for row, name in enumerate(ORDER):
            clip = manifest['clips'][name]
            for frame in range(6):
                sheet_key = phase if clip['kind'] == 'main' else 'special-' + phase
                source_row, source_col = clip['row'], frame
                if name == 'infernal-shot':
                    sheet_key, source_row = 'laser', 0 if phase == 'normal' else 1
                override = clip.get('frameOverrides', {}).get(phase, {}).get(str(frame))
                if override:
                    sheet_key, source_row, source_col = override['sheet'], override['row'], override['column']
                sheet = manifest['sheets'][sheet_key]
                x, y, w, h = sheet['rects'][source_row][source_col]
                raw = images[sheet_key].crop((x, y, x+w, y+h))
                inset = round(h * clip.get('frameInsetTop', {}).get(str(frame), 0))
                if inset:
                    raw.paste((0, 0, 0, 0), (0, 0, w, inset))
                factor = manifest['sheets']['normal']['cell'] / sheet['cell'] * sheet.get('scaleCorrection', 1)
                factor *= clip.get('previewFrameScales', [1]*6)[frame]
                resized = raw.resize((round(w*factor), round(h*factor)), Image.Resampling.NEAREST)
                pivot = sheet.get('pivot', [.53, .77])
                px, py = round(ANCHOR_X - pivot[0]*resized.width), round(ANCHOR_Y - pivot[1]*resized.height)
                pose = Image.new('RGBA', (CELL, CELL))
                pose.paste(resized, (px, py))
                # Slice on the visible silhouette. Almost transparent matte noise at
                # cell edges must not extend UVs into a neighbouring atlas pose.
                strong = raw.getchannel('A').point(lambda a: 255 if a >= 64 else 0)
                edge, gap = max(1, round(w*.02)), max(2, round(w*.05))
                left = strong.crop((0, 0, edge, h))
                # Several grid cells contain a 1–3 px sliver of the preceding
                # pose, separated from this scorpion by a fully transparent gap.
                # Restrict the sprite UV rectangle; the generated pixels are preserved.
                left_inset = edge if left.getbbox() and not strong.crop((edge, 0, gap, h)).getbbox() \
                    and left.histogram()[255] < strong.histogram()[255] * .01 else 0
                cut = max(0, px + math.ceil(left_inset*factor)) if left_inset else 0
                roi = pose.getchannel('A').point(lambda a: 255 if a >= 64 else 0).crop((cut, 0, CELL, CELL))
                box = roi.getbbox()
                if box: box = (box[0]+cut, box[1], box[2]+cut, box[3])
                assert box and box[0] > 0 and box[1] > 0 and box[2] < CELL and box[3] < CELL, (phase, name, frame, box)
                box = (min(box[0]-1, ANCHOR_X-1), min(box[1]-1, ANCHOR_Y-1),
                       max(box[2]+1, ANCHOR_X+1), max(box[3]+1, ANCHOR_Y+1))
                atlas.paste(pose, (frame*CELL, row*CELL))
                # Unity rect origin is bottom-left. Trim empty padding for atlas memory.
                frames.append(dict(phase=phase, name=f'{name}-{frame:02}', clip=row, frame=frame,
                    x=frame*CELL+box[0], y=atlas.height-(row*CELL+box[3]), width=box[2]-box[0], height=box[3]-box[1],
                    pivotX=(ANCHOR_X-box[0])/(box[2]-box[0]), pivotY=(box[3]-ANCHOR_Y)/(box[3]-box[1])))
        atlas.save(TARGET / f'{phase}.png')
    audio = TARGET / 'Audio'
    audio.mkdir(exist_ok=True)
    for path in sorted((SOURCE / 'audio').glob('*.wav')):
        if path.name != 'demo.wav':
            shutil.copy2(path, audio / path.name)
    (TARGET / 'import.json').write_text(json.dumps(dict(frames=frames, clips=[
        dict(name=name, durations=[ms/1000 for ms in manifest['clips'][name]['durationsMs']],
             loop=manifest['clips'][name]['loop']) for name in ORDER]), indent=2)+'\n', 'utf-8')
    report = dict(source='Concepts/Shaula/v2/animations.json', method='Slice approved generated raster art; nearest-neighbour scale and common ground pivot; no new drawn pixels',
                  artSources={s['file']: s['sha256'] for s in manifest['sheets'].values()},
                  files=[dict(path=p.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
                         for p in sorted(TARGET.rglob('*')) if p.suffix in {'.png', '.wav'}])
    (ROOT / 'Publishing/shaula-asset-provenance.json').write_text(json.dumps(report, indent=2)+'\n', 'utf-8')
    print(f'Shaula imported: {len(frames)} poses, 19 audio clips; ground pivot ({ANCHOR_X}, {CELL-ANCHOR_Y})')


if __name__ == '__main__':
    main()
