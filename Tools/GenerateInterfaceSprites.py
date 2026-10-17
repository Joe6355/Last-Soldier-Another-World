"""Original geometric pixel UI / magic effects; no source artwork is sampled.

Only Unity rectangle metadata is read. Original PNGs are retained outside Assets.
"""
from pathlib import Path
import hashlib
import json
import math
import random
import re
import shutil
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
BACKUP = ROOT / 'Builds/AssetReplacements/original-sprites'
OUTLINE = '#162d28'
GOLD = '#e5be6a'
CREAM = '#f5e6b9'
RECTS = re.compile(r'      name: ([^\n]+)\n      rect:\n        serializedVersion: 2\n'
                   r'        x: ([\d.]+)\n        y: ([\d.]+)\n        width: ([\d.]+)\n        height: ([\d.]+)'
                   r'.*?      internalID: (-?\d+)', re.S)


def shape(kind, frame=0, count=1):
    im = Image.new('RGBA', (64, 64)); d = ImageDraw.Draw(im)
    if kind == 'frame':
        d.rectangle((0, 0, 63, 63), outline=OUTLINE, width=2)
    elif kind in ('info', 'question', 'return', 'panel'):
        d.rounded_rectangle((1, 1, 62, 62), radius=6, fill='#1d3d33', outline='#769785', width=2)
        d.line((8, 5, 20, 5), fill=GOLD, width=2); d.line((44, 58, 56, 58), fill=GOLD, width=2)
        if kind == 'info':
            d.ellipse((28, 15, 36, 23), fill=CREAM); d.rectangle((29, 29, 35, 49), fill=CREAM)
        elif kind == 'question':
            d.arc((20, 14, 44, 38), 190, 365, fill=CREAM, width=5)
            d.line((40, 32, 32, 39, 32, 43), fill=CREAM, width=5); d.rectangle((30, 49, 34, 53), fill=CREAM)
        elif kind == 'return':
            d.arc((17, 17, 48, 48), 195, 510, fill=CREAM, width=5)
            d.polygon([(12, 23), (26, 23), (13, 36)], fill=CREAM)
    elif kind.startswith('arrow'):
        colour = {'arrow': '#aac8cc', 'arrow-poison': '#70c08b', 'arrow-holy': '#edba67'}[kind]
        d.line((32, 15, 32, 52), fill='#956441', width=5)
        d.polygon([(32, 4), (23, 22), (41, 22)], fill=colour, outline=OUTLINE)
        d.polygon([(27, 43), (23, 54), (32, 50), (41, 54), (37, 43)], fill=colour, outline=OUTLINE)
        d.line((32, 7, 32, 17), fill=CREAM, width=2)
    elif kind == 'mirror':
        d.rectangle((29, 40, 35, 59), fill='#9b7045', outline=OUTLINE)
        d.ellipse((14, 3, 50, 45), fill=GOLD, outline=OUTLINE, width=3)
        d.ellipse((19, 8, 45, 39), fill='#9acbc4', outline='#b9d7cb', width=2)
        d.line((23, 30, 37, 13), fill=CREAM, width=4); d.line((31, 32, 40, 21), fill='#e3f0dc', width=2)
    elif kind == 'potion':
        d.rectangle((25, 4, 39, 12), fill='#ad7a41', outline=OUTLINE, width=2)
        d.polygon([(26, 12), (38, 12), (38, 30), (51, 45), (49, 57), (15, 57), (13, 45), (26, 30)],
                  fill='#afdbd0', outline=OUTLINE)
        d.polygon([(23, 37), (41, 37), (47, 47), (45, 53), (19, 53), (17, 47)], fill='#e49c72')
        d.rectangle((28, 39, 36, 51), fill=CREAM); d.rectangle((24, 43, 40, 47), fill=CREAM)
        d.line((27, 17, 27, 29), fill='#edf5dd', width=2)
    elif kind == 'coin':
        d.ellipse((6, 6, 58, 58), fill='#90652e', outline=OUTLINE, width=3)
        d.ellipse((10, 8, 56, 54), fill=GOLD, outline='#f5de99', width=3)
        d.line((30, 19, 30, 42), fill='#b1873d', width=4)
        d.line((24, 24, 30, 18, 38, 24), fill=CREAM, width=3)
    elif kind == 'shield':
        for radius, colour, width in [(29, '#63a7a0b0', 2), (26, '#a3d6c0d0', 1), (23, '#467d7950', 2)]:
            d.ellipse((32-radius, 32-radius, 32+radius, 32+radius), outline=colour, width=width)
        for i in range(12):
            angle = i * math.pi / 6
            x, y = 32 + int(27*math.cos(angle)), 32 + int(27*math.sin(angle))
            d.rectangle((x-1, y-1, x+1, y+1), fill='#e7d09caf')
    elif kind in ('spark', 'explosion'):
        rng = random.Random(1000 + frame)
        phase = (frame + .4) / max(count, 1)
        if phase < .85:
            radius = max(1, round(17 * math.sin(min(1, phase) * math.pi)))
            d.ellipse((32-radius, 32-radius, 32+radius, 32+radius), fill=(255, 209, 99, int(190*(1-phase))))
            d.ellipse((30-radius//2, 30-radius//2, 34+radius//2, 34+radius//2), fill=(255, 247, 184, int(220*(1-phase))))
        for i in range(14 if kind == 'explosion' else 8):
            angle = i*2*math.pi/(14 if kind == 'explosion' else 8) + .17
            radius = 3 + phase*25
            x, y = 32+int(math.cos(angle)*radius), 32+int(math.sin(angle)*radius)
            size = max(1, int(4*(1-phase)))
            alpha = max(0, int(240*(1-phase)))
            colour = (255, 190 + rng.randrange(50), 80 + rng.randrange(60), alpha)
            d.rectangle((x-size, y-size, x+size, y+size), fill=colour)
    elif kind == 'barrel':
        d.ellipse((9, 48, 55, 61), fill='#102b2440')
        d.rounded_rectangle((16, 7, 48, 54), radius=7, fill='#93633d', outline=OUTLINE, width=3)
        for x in [24, 32, 40]: d.line((x, 12, x, 50), fill='#5c422f', width=2)
        d.rectangle((15, 17, 49, 23), fill='#789389', outline=OUTLINE)
        d.rectangle((15, 39, 49, 45), fill='#789389', outline=OUTLINE)
        d.ellipse((19, 6, 45, 15), fill='#c09155', outline=OUTLINE, width=2)
        d.polygon([(32, 26), (39, 33), (32, 40), (25, 33)], fill=GOLD)
    elif kind == 'altar':
        d.ellipse((5, 48, 59, 61), fill='#102b2440')
        d.polygon([(12, 47), (52, 47), (58, 56), (6, 56)], fill='#58786b', outline=OUTLINE)
        d.rectangle((22, 29, 42, 47), fill='#7b9987', outline=OUTLINE, width=2)
        d.polygon([(13, 26), (50, 26), (46, 33), (17, 33)], fill='#b3c6a6', outline=OUTLINE)
        d.polygon([(33, 4), (41, 17), (33, 25), (26, 16)], fill='#85d3b0', outline=OUTLINE)
        d.line((33, 8, 33, 20), fill=CREAM, width=2)
    elif kind == 'outpost':
        d.ellipse((8, 49, 55, 60), fill='#102b2440')
        d.rectangle((27, 8, 33, 54), fill='#946b43', outline=OUTLINE)
        d.polygon([(33, 9), (55, 13), (46, 21), (54, 29), (33, 25)], fill='#dbb961', outline=OUTLINE)
        d.rectangle((18, 48, 42, 55), fill='#698774', outline=OUTLINE, width=2)
        d.polygon([(38, 14), (44, 16), (41, 22), (37, 20)], fill='#275448')
    elif kind == 'rune':
        d.ellipse((7, 8, 57, 57), fill='#557666', outline=OUTLINE, width=3)
        d.arc((16, 17, 48, 49), 25, 335, fill='#abe2c1', width=3)
        d.line((32, 16, 32, 47), fill='#abe2c1', width=3)
    elif kind == 'tent':
        d.ellipse((3, 49, 61, 62), fill='#102b2440')
        d.polygon([(8, 27), (32, 3), (56, 27), (56, 54), (8, 54)], fill='#c9ad65', outline=OUTLINE)
        d.polygon([(8, 27), (32, 3), (32, 35), (8, 54)], fill='#e9d792', outline='#9b763e')
        d.polygon([(32, 3), (56, 27), (56, 54), (32, 35)], fill='#b89851', outline='#9b763e')
        d.polygon([(8, 54), (32, 24), (56, 54)], fill='#f1e0a2', outline='#8e6839')
        d.polygon([(23, 54), (32, 39), (40, 54)], fill='#423c2e', outline='#8e6839')
        d.line((32, 5, 32, 24), fill='#f8edbd', width=2)
        d.line((17, 23, 19, 38), fill='#b99a53', width=2)
        d.line((43, 24, 45, 39), fill='#a17d40', width=2)
        for x in [6, 56]:
            d.rectangle((x, 24, x+2, 57), fill='#775033')
            d.rectangle((x-1, 27, x+3, 30), fill='#b78d51')
    return im


def main():
    jobs = {
        'Assets/Sprits/ramka.png': 'frame',
        'Assets/Sprits/0.png': 'spark',
        'Assets/Sprits/Explosion.png': 'explosion',
        'Assets/Sprits/spr_shield.png': 'shield',
        'Assets/Sprits/ui/HUD Menus.png': 'panel',
        'Assets/Sprits/ui/Square Buttons/Info Square Button.png': 'info',
        'Assets/Sprits/ui/Square Buttons/Questionmark Square Button.png': 'question',
        'Assets/Sprits/ui/Square Buttons/Return Square Button.png': 'return',
        'Assets/Sprits/uiEL.png': 'equipment',
        'Assets/Sprits/Map/preview.png': 'tent',
        'Assets/UI/ForestGarrison/Arena/Altar.png': 'altar',
        'Assets/UI/ForestGarrison/Arena/Barrel.png': 'barrel',
        'Assets/UI/ForestGarrison/Arena/Outpost.png': 'outpost',
        'Assets/UI/ForestGarrison/Arena/GravityRune.png': 'rune',
    }
    records = []
    for filename, kind in jobs.items():
        path = ROOT / filename
        meta = Path(str(path) + '.meta'); meta_hash = hashlib.sha256(meta.read_bytes()).hexdigest()
        backup = BACKUP / filename.removeprefix('Assets/')
        if not backup.exists():
            backup.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(path, backup)
        with Image.open(backup) as original:
            size = original.size
        rects = [(m[1], *[int(float(value)) for value in m.group(2, 3, 4, 5)])
                 for m in RECTS.finditer(meta.read_text('utf-8-sig'))]
        if not rects:
            rects = [(path.stem, 0, 0, *size)]
        out = Image.new('RGBA', size)
        for index, (name, x, y, width, height) in enumerate(rects):
            tile_kind = kind
            if kind == 'equipment':
                tile_kind = ['mirror', 'potion', 'coin', 'arrow', 'arrow-poison', 'arrow-holy'][index]
            sprite = shape(tile_kind, index, len(rects))
            # Equipment has slender rectangles: fit the glyph without stretching its geometry.
            if kind == 'equipment':
                sprite = sprite.crop(sprite.getbbox())
                sprite.thumbnail((width, height), Image.Resampling.NEAREST)
            else:
                sprite = sprite.resize((width, height), Image.Resampling.NEAREST)
            at = (x+(width-sprite.width)//2, size[1]-y-height+(height-sprite.height)//2)
            out.alpha_composite(sprite, at)
        out.save(path)
        assert hashlib.sha256(meta.read_bytes()).hexdigest() == meta_hash
        assert out.getbbox() is not None
        records.append({'file':filename,'kind':kind,'dimensions':size,'rectangles':len(rects),
                        'source':'Original geometric pixel drawing in Tools/GenerateInterfaceSprites.py; no sampled image pixels',
                        'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'metaSha256':meta_hash,
                        'originalSha256':hashlib.sha256(backup.read_bytes()).hexdigest()})
        print(filename, 'OK', flush=True)
    (ROOT/'Publishing/interface-sprite-provenance.json').write_text(
        json.dumps({'createdOn':'2026-10-17','generator':'Tools/GenerateInterfaceSprites.py','files':records},indent=2)+'\n',encoding='utf-8')


if __name__ == '__main__':
    main()
