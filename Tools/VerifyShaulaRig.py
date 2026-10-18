"""Check actual imported sprite bounds and synthesized PCM data without altering either."""
from pathlib import Path
import hashlib
import json
import re
import wave
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
def main():
    path = ROOT / 'Assets/Bosses/Shaula/rig-parts.png'
    image = Image.open(path)
    assert image.mode == 'RGBA' and image.size == (1536, 1024)
    assert path.read_bytes() == (ROOT / 'Concepts/Shaula/v3/rig-parts.png').read_bytes()
    meta = Path(str(path) + '.meta').read_text('utf-8')
    entries = re.findall(r'    - serializedVersion: 2\n      name: (.*?)\n      rect:\n        serializedVersion: 2\n        x: ([\d.]+)\n        y: ([\d.]+)\n        width: ([\d.]+)\n        height: ([\d.]+)', meta)
    assert len(entries) == 9, len(entries)
    alpha = image.getchannel('A').point(lambda a: 255 if a >= 115 else 0)
    rects = []
    for name, x, y, w, h in entries:
        x, y, w, h = map(lambda v: int(float(v)), (x, y, w, h))
        rect = (x, image.height - y - h, x + w, image.height - y)
        box = alpha.crop(rect).getbbox()
        assert box and box[0] > 0 and box[1] > 0 and box[2] < w and box[3] < h, (name, box)
        for other in rects:
            assert min(rect[2], other[2]) <= max(rect[0], other[0]) or min(rect[3], other[3]) <= max(rect[1], other[1])
        rects.append(rect)
    clips = []
    for item in json.loads((ROOT / 'Concepts/Shaula/v3/audio-manifest.json').read_text('utf-8'))['clips']:
        p = ROOT / item['file']; assert hashlib.sha256(p.read_bytes()).hexdigest() == item['sha256']
        with wave.open(str(p), 'rb') as wav:
            assert wav.getsampwidth() == 2
            sound = np.frombuffer(wav.readframes(wav.getnframes()), dtype='<i2') / 32767
            assert np.isfinite(sound).all() and max(abs(sound)) < .99
            assert abs(sound[0]) < .005 and abs(sound[-1]) < .005
            clips.append(dict(path=item['file'], seconds=wav.getnframes() / wav.getframerate(), peak=float(max(abs(sound)))))
    result = dict(result='PASS', spriteParts=len(entries), noOverlappingSlices=True, noClippedOpaqueEdges=True,
        sourcePixelsPreserved=True, audio=clips, scope='Imported rectangles and PCM integrity; subjective appearance/listening requires playtesting.')
    (ROOT / 'Publishing/shaula-v23-assets-checks.json').write_text(json.dumps(result, indent=2) + '\n', 'utf-8')
    print(json.dumps(result, indent=2))
if __name__ == '__main__': main()
