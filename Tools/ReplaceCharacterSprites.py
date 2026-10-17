"""Replace unverified character art with licensed Tiny 16 derivatives.

Run with the repository's Python + Pillow: --apply, then --verify.
Source downloads are recorded in Builds/AssetReplacements/sources/downloads.json.
The Unity .meta files are read-only: GUIDs, sprite rectangles and pivots stay intact.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import shutil
from pathlib import Path

from PIL import Image, ImageDraw, __version__ as pillow_version

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Builds/AssetReplacements/sources"
BACKUP = ROOT / "Builds/AssetReplacements/original-sprites"
MANIFEST = ROOT / "Publishing/character-replacements.json"
SOURCE_HASHES = {
    "tiny16-basic-characters.png": "76893cf9d886433c383c776ed04ab3290219d098f01087f8f93962ba42f63797",
    "tiny16-expanded-skeleton.png": "5cee791c28b4b6cb425703a7931babbbf8839e4b7c5b31f166fa43920865a54c",
    "tiny16-more-character-animations.png": "6e83a1ece1e08239a566fa9b2ac6a891a9e2560b06801165e0737ec52e07a041",
}
PORTRAIT_HASH = "70ee1c297c74683ab33bca18d27a114d4a33fe908f1798d4b6c7523c0b45c9a9"
FILES = [
    "PlayerAll.png", "allEnemy.png", "Banditka/Beka.png",
    "Banditka/BekaWolk.png", "Banditka/Bandit Faceset.png", "Boss/Assassin.png",
    "Healer.png", "HealerAtack.png", "nun-black-NESW.png", "SciDE.png",
    "ShoperAllAnim.png",
]
INK = (25, 32, 30, 255)
BONE = (220, 230, 197, 255)
GOLD = (239, 193, 97, 255)
GREEN = (124, 185, 89, 255)
TEAL = (123, 212, 188, 255)
VIOLET = (167, 132, 204, 255)
WOOD = (151, 91, 43, 255)


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rects(path: Path, size: tuple[int, int]) -> list[dict]:
    text = Path(str(path) + ".meta").read_text("utf-8-sig")
    pattern = (r"      name: ([^\r\n]+)\r?\n      rect:\r?\n"
               r"        serializedVersion: \d+\r?\n"
               r"        x: ([\d.-]+)\r?\n        y: ([\d.-]+)\r?\n"
               r"        width: ([\d.-]+)\r?\n        height: ([\d.-]+)")
    result = []
    for match in re.finditer(pattern, text):
        x, y, w, h = [round(float(s)) for s in match.groups()[1:]]
        result.append(dict(name=match.group(1), x=x, y=y, width=w, height=h))
    return result or [dict(name=path.stem, x=0, y=0, width=size[0], height=size[1])]


def cell(sheet: Image.Image, col: int, row: int) -> Image.Image:
    return sheet.crop((16 * col, 16 * row, 16 * (col + 1), 16 * (row + 1)))


def recolour(image: Image.Image, palette: dict) -> Image.Image:
    out = image.copy()
    out.putdata([palette.get(p[:3], p) if p[3] else (0, 0, 0, 0)
                 for p in image.get_flattened_data()])
    return out


def fit(image: Image.Image, width: int, height: int) -> Image.Image:
    bounds = image.getbbox()
    if bounds is None:
        return Image.new("RGBA", (max(1, width), max(1, height)))
    image = image.crop(bounds)
    factor = min(width / image.width, height / image.height)
    return image.resize((max(1, round(image.width * factor)),
                         max(1, round(image.height * factor))), Image.Resampling.NEAREST)


def paste_foot(canvas: Image.Image, image: Image.Image, area=None):
    x, y, w, h = area or (0, 0, canvas.width, canvas.height)
    image = fit(image, max(1, w), max(1, h))
    canvas.alpha_composite(image, (x + (w - image.width) // 2, y + h - image.height))


def spark(draw: ImageDraw.ImageDraw, x: int, y: int, radius: int, colour):
    draw.line((x - radius, y, x + radius, y), fill=colour, width=1)
    draw.line((x, y - radius, x, y + radius), fill=colour, width=1)
    draw.point((x, y), fill=(255, 248, 212, 255))


def shield(width: int, height: int) -> Image.Image:
    image = Image.new("RGBA", (width, height))
    d = ImageDraw.Draw(image)
    d.polygon([(0, 1), (width - 1, 1), (width - 2, height * 2 // 3),
               (width // 2, height - 1), (1, height * 2 // 3)], fill=INK)
    d.polygon([(2, 3), (width - 3, 3), (width - 3, height * 2 // 3),
               (width // 2, height - 3), (2, height * 2 // 3)], fill=WOOD)
    if width > 5:
        d.line((width // 2, 3, width // 2, height - 4), fill=(203, 139, 63, 255))
        d.point((3, 4), fill=GOLD)
    return image


def bomb(width: int, height: int, phase=0) -> Image.Image:
    image = Image.new("RGBA", (width, height)); d = ImageDraw.Draw(image)
    cy = max(2, height - width // 2 - 1)
    d.ellipse((0, max(2, height - width), width - 1, height - 1), fill=INK)
    d.ellipse((2, max(3, height - width + 2), width - 2, height - 2), fill=(62, 76, 70, 255))
    d.line((width // 2, max(1, height - width - 2), width // 2, max(2, height - width)), fill=WOOD)
    spark(d, min(width - 1, width // 2 + phase % 2), max(0, height - width - 2), 1, GOLD)
    return image


class LicensedSprites:
    def __init__(self):
        self.basic = Image.open(SOURCE / "tiny16-basic-characters.png").convert("RGBA")
        self.skeleton = Image.open(SOURCE / "tiny16-expanded-skeleton.png").convert("RGBA")
        # This upstream sheet uses a flat green key rather than an alpha channel.
        self.skeleton.putdata([(0, 0, 0, 0) if pixel[:3] == (109, 170, 44) else pixel
                               for pixel in self.skeleton.get_flattened_data()])
        self.actions = Image.open(SOURCE / "tiny16-more-character-animations.png").convert("RGBA")
        self.portrait = Image.open(ROOT / "Assets/Sprits/LPCportrait33.png").convert("RGBA")

    def bone(self, phase=0, direction=0, attacking=False):
        col = (3 + phase % 2) if attacking else phase % 3
        return recolour(cell(self.skeleton, col, direction % 4), {
            (117, 113, 97): (142, 157, 130, 255),
            (133, 149, 161): (181, 197, 164, 255),
            (222, 238, 214): BONE,
        })

    def traveller(self, phase=0, direction=0):
        return cell(self.basic, 3 + phase % 3, direction % 4)

    def wizard(self, phase=0, direction=0):
        return recolour(cell(self.basic, 6 + phase % 3, direction % 4), {
            (48, 52, 109): (37, 78, 74, 255), (89, 125, 206): (58, 135, 121, 255),
            (109, 194, 202): TEAL,
        })

    def archer(self, phase=0, direction=0):
        base = cell(self.basic, phase % 3, direction % 4)
        base = recolour(base, {(117, 113, 97): (79, 112, 53, 255),
                              (218, 212, 94): (142, 170, 83, 255)})
        d = ImageDraw.Draw(base)
        # A new hood and bow, drawn for this project. No pixels from old art.
        d.polygon([(5, 0), (10, 0), (12, 3), (11, 7), (4, 7), (3, 3)], fill=INK)
        d.polygon([(5, 1), (10, 1), (11, 3), (10, 6), (5, 6), (4, 3)], fill=(79, 112, 53, 255))
        d.rectangle((6, 3, 9, 5), fill=(210, 170, 136, 255))
        d.point((6, 4), fill=INK); d.point((9, 4), fill=INK)
        d.line((5, 1, 9, 1), fill=(142, 170, 83, 255))
        d.line([(12, 7), (14, 9), (14, 11), (12, 14)], fill=(163, 112, 54, 255))
        d.line((12, 7, 12, 14), fill=(210, 201, 155, 255))
        return base

    def skeleton_mage(self, phase=0, teal=False):
        base = Image.new("RGBA", (20, 21))
        base.alpha_composite(self.bone(phase, attacking=phase > 2), (0, 5))
        d = ImageDraw.Draw(base); colour = TEAL if teal else VIOLET
        d.polygon([(4, 6), (6, 0), (9, 5), (12, 7), (3, 7)], fill=INK)
        d.polygon([(5, 5), (6, 2), (9, 6)], fill=colour)
        d.line((16, 8, 16, 19), fill=WOOD)
        spark(d, 16, 6, 2 + phase % 2, colour)
        return base

    def assassin(self, phase=0, attack=False):
        base = cell(self.actions, 3 if attack else phase % 3, 2)
        base = recolour(base, {(117, 113, 97): (59, 65, 87, 255),
                              (218, 212, 94): (138, 146, 176, 255),
                              (210, 125, 44): (114, 107, 146, 255)})
        d = ImageDraw.Draw(base)
        d.line((10, 7, 14 if attack else 11, 11), fill=(185, 204, 204, 255))
        d.line((9, 8, 11, 8), fill=GOLD)
        return base


def enemy_tile(source: LicensedSprites, name: str, w: int, h: int, phase: int) -> tuple[Image.Image, str]:
    n = int(name.rsplit("_", 1)[-1]); canvas = Image.new("RGBA", (w, h))
    if n == 1:
        return bomb(w, h), "project-drawn bomb with fuse"
    if n == 2:
        return shield(w, h), "project-drawn wooden shield; reinforcement shader compatible"
    if n in (3, 8):
        d = ImageDraw.Draw(canvas)
        d.ellipse((0, 0, w - 1, h - 1), fill=(84, 155, 90, 200))
        d.ellipse((max(1, w // 5), max(1, h // 5), w - 1 - max(1, w // 5), h - 1 - max(1, h // 5)),
                  fill=(173, 224, 136, 220))
        return canvas, "project-drawn green spell orb or poison pool"
    if n in (89, 90, 91, 92, 93, 94, 95, 96):
        slime = cell(source.basic, phase % 3, 4 if n < 93 else 5)
        paste_foot(canvas, slime)
        return canvas, "licensed green slime animation"
    if n in (4, 5):
        d = ImageDraw.Draw(canvas); d.ellipse((0, 0, w - 1, h - 1), fill=(69, 123, 56, 255))
        d.ellipse((1, 1, max(1, w - 2), max(1, h - 2)), fill=(174, 224, 104, 255))
        return canvas, "project-drawn green magic projectile"
    if n in (29, 33, 36, 38, 45, 46, 47, 48):
        paste_foot(canvas, source.bone(phase), (0, h // 3, w, h - h // 3))
        b = bomb(max(7, w // 2), max(9, h // 4), phase)
        canvas.alpha_composite(b, ((w - b.width) // 2, max(0, h // 3 - b.height)))
        return canvas, "licensed skeleton carrying project-drawn bomb"
    if n in (57, 61, 65, 68, 73, 75, 77, 79):
        paste_foot(canvas, source.bone(phase), (0, 0, max(1, w - 10), h))
        s = shield(12, 20); canvas.alpha_composite(s, (w - 13, h - 21))
        return canvas, "licensed skeleton with project-drawn wooden shield"
    if n >= 97:
        paste_foot(canvas, source.skeleton_mage(phase))
        return canvas, "licensed skeleton with project-drawn violet magic hat and staff"
    paste_foot(canvas, source.bone(phase))
    return canvas, "licensed skeleton walk cycle"


def make_tile(source: LicensedSprites, path: str, rect: dict, index: int) -> tuple[Image.Image, str]:
    w, h = rect["width"], rect["height"]; tile = Image.new("RGBA", (w, h))
    if path == "allEnemy.png":
        return enemy_tile(source, rect["name"], w, h, index)
    if path == "PlayerAll.png":
        paste_foot(tile, source.archer([0, 1, 0, 2, 0][index]))
        return tile, "licensed male walk cycle; new green hood, cloak palette and wooden bow"
    if path == "Banditka/Bandit Faceset.png":
        # Original licensed portrait, not the old unknown Bandit Faceset.
        boxes = [(0, 0, 260, 380), (0, 380, 300, 768), (300, 380, 600, 768),
                 (600, 380, 900, 768), (300, 380, 600, 768), (0, 0, 260, 380)]
        portrait = source.portrait.crop(boxes[index])
        tile.alpha_composite(portrait.resize((w, h), Image.Resampling.LANCZOS))
        return tile, "cropped and resized confirmed PlayCraft/ZeNeRIA29 portrait"
    if path.startswith("Banditka/"):
        # Match the female character embedded beside the verified portrait.
        actor = source.portrait.crop((265, 8, 294, 58))
        paste_foot(tile, actor, (index % 2, 0, max(1, w - 1), h - index % 2))
        return tile, "confirmed portrait companion sprite; subtle walking bob"
    if path == "nun-black-NESW.png":
        paste_foot(tile, source.traveller(index % 3, index // 3))
        return tile, "licensed ordinary traveller; no religious habit or symbol"
    if path == "SciDE.png":
        paste_foot(tile, source.skeleton_mage(index))
        return tile, "licensed skeleton mage with violet hat and magic staff"
    if path in ("Healer.png", "HealerAtack.png"):
        # Reserve space for the cast; never stretch a small body across a wide frame.
        body_width = min(w, 26); x = (w - body_width) // 2
        paste_foot(tile, source.skeleton_mage(index, teal=True), (x, 0, body_width, h - 1))
        if path == "HealerAtack.png":
            d = ImageDraw.Draw(tile)
            for j in range(1 + index % 4):
                sx = max(2, min(w - 3, (w // 2 + round(math.cos(j * 2 + index) * (6 + index)))))
                sy = max(2, min(h - 3, h // 2 + round(math.sin(j * 2 + index) * 8)))
                spark(d, sx, sy, 1 + (index + j) % 2, TEAL)
        return tile, "licensed skeleton healer; teal spell particles drawn within preserved frame"
    if path == "ShoperAllAnim.png":
        paste_foot(tile, source.traveller(index % 3), (0, min(2, index % 3), w, h - min(2, index % 3)))
        if 1 <= index <= 11:
            d = ImageDraw.Draw(tile); y = 1 + index % 3
            d.polygon([(w // 2, y), (w // 2 + 4, y + 4), (w // 2, y + 8), (w // 2 - 4, y + 4)], fill=GOLD)
            d.line((w // 2 - 2, y + 4, w // 2 + 2, y + 4), fill=(255, 241, 178, 255))
        return tile, "licensed merchant walk/idle cycle; new small coin emote"
    if path == "Boss/Assassin.png":
        if 38 <= index <= 47:
            # Friendly disappearance, including dormant death-animation frames.
            if index <= 41:
                paste_foot(tile, source.assassin(index))
                alpha = tile.getchannel("A").point(lambda value: value * (42 - index) // 4)
                tile.putalpha(alpha)
            d = ImageDraw.Draw(tile)
            for j in range(3):
                spark(d, min(w - 2, 2 + (index + j * 5) % max(1, w - 3)),
                      min(h - 2, 1 + (index * 3 + j * 7) % max(1, h - 2)), 1, GOLD)
            return tile, "golden disappearance sparks; no fallen body"
        paste_foot(tile, source.assassin(index, 10 <= index <= 27))
        return tile, "licensed strike/walk poses; slate palette and small fantasy blade"
    raise ValueError(path)


def prepare():
    for name, expected in SOURCE_HASHES.items():
        actual = sha(SOURCE / name)
        if actual != expected:
            raise RuntimeError(f"Source hash mismatch: {name}: {actual}")
    if sha(ROOT / "Assets/Sprits/LPCportrait33.png") != PORTRAIT_HASH:
        raise RuntimeError("Verified portrait source changed")
    return LicensedSprites()


def render(source: LicensedSprites, short: str):
    path = ROOT / "Assets/Sprits" / short
    current = Image.open(path)
    size = current.size
    canvas = Image.new("RGBA", size)
    info = []
    for index, rect in enumerate(rects(path, size)):
        tile, role = make_tile(source, short, rect, index)
        if tile.size != (rect["width"], rect["height"]):
            raise RuntimeError(f"Wrong tile size: {short}/{rect['name']}")
        if tile.getbbox() is None:
            raise RuntimeError(f"Empty frame: {short}/{rect['name']}")
        top = size[1] - rect["y"] - rect["height"]
        canvas.alpha_composite(tile, (rect["x"], top))
        info.append(dict(**rect, role=role, opaqueBounds=tile.getbbox()))
    return path, canvas, info


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    if not args.apply and not args.verify:
        parser.error("Choose --apply or --verify")
    source = prepare()
    rows = []
    previews = []
    previous_files = json.loads(MANIFEST.read_text('utf-8'))['files'] if MANIFEST.exists() else []
    retained = {item['path']: item for item in json.loads(MANIFEST.read_text('utf-8'))['files']
                if item.get('restoredFromBackup')} if MANIFEST.exists() else {}
    for short in FILES:
        key = 'Assets/Sprits/' + short
        if key in retained:
            entry = retained[key]
            path = ROOT / key
            if sha(path) != entry['sha256'] or sha(path) != sha(ROOT / entry['restoredFromBackup']):
                raise RuntimeError(f'Restored original no longer matches the reviewed backup: {short}')
            if sha(Path(str(path) + '.meta')) != entry['metadataSHA256']:
                raise RuntimeError(f'Restored original Unity metadata changed: {short}')
            rows.append(entry)
            continue
        path, generated, frames = render(source, short)
        meta = Path(str(path) + ".meta")
        old_meta_hash = sha(meta)
        backup = BACKUP / short
        if args.apply:
            backup.parent.mkdir(parents=True, exist_ok=True)
            if not backup.exists():
                shutil.copy2(path, backup)
                shutil.copy2(meta, Path(str(backup) + ".meta"))
            elif sha(Path(str(backup) + ".meta")) != old_meta_hash:
                raise RuntimeError(f"Unity metadata changed since backup: {short}")
            generated.save(path, compress_level=9)
        actual = Image.open(path).convert("RGBA")
        if actual.size != generated.size or actual.tobytes() != generated.tobytes():
            raise RuntimeError(f"Generated pixels differ: {short}")
        if old_meta_hash != sha(meta):
            raise RuntimeError(f"Unity metadata modified: {short}")
        rows.append(dict(path=path.relative_to(ROOT).as_posix(), size=list(generated.size),
                         originalSHA256=sha(backup), sha256=sha(path),
                         metadataSHA256=old_meta_hash, frameCount=len(frames), frames=frames))
        # First three frames on a labelled neutral strip for visual review.
        for f in frames[:3]:
            top = generated.height - f["y"] - f["height"]
            previews.append((short + "/" + f["name"], generated.crop((f["x"], top, f["x"] + f["width"], top + f["height"]))))
    for entry in previous_files:
        if not entry.get('preservedVariantSource'):
            continue
        path = ROOT / entry['path']
        if sha(path) != entry['sha256'] or sha(path) != sha(ROOT / entry['preservedVariantSource']):
            raise RuntimeError('Preserved visual variant changed: ' + entry['path'])
        if sha(Path(str(path) + '.meta')) != entry['metadataSHA256']:
            raise RuntimeError('Preserved visual variant metadata changed: ' + entry['path'])
        rows.append(entry)
    report = dict(createdOn="2026-10-17", generator="Tools/ReplaceCharacterSprites.py",
                  pillowVersion=pillow_version, originalBackup=BACKUP.relative_to(ROOT).as_posix(),
                  sourceHashes=SOURCE_HASHES, portraitSourceSHA256=PORTRAIT_HASH,
                  sources=[
                      dict(title="Tiny 16: Basic", author="Lanea Zimmerman (Sharm)",
                           url="https://opengameart.org/content/tiny-16-basic", license="CC-BY-3.0"),
                      dict(title="Tiny 16: Expanded Character Sprites", author="withthelove, Lanea Zimmerman, Evert, mdkieran",
                           url="https://opengameart.org/content/tiny-16-expanded-character-sprites", license="CC-BY-3.0"),
                      dict(title="Tiny 16: more character animations", author="Lanea Zimmerman, Clint Bellanger, Charles Gabriel, basxto",
                           url="https://opengameart.org/content/tiny-16-more-character-animations", license="CC-BY-3.0"),
                      dict(title="LPC Anime Portrait #2", author="PlayCraft, ZeNeRIA29; posted by ZaPaper",
                           url="https://opengameart.org/content/lpc-anime-portrait-2", license="CC-BY-3.0"),
                  ], unityMetaChanged=False, files=rows,
                  checks=dict(sourceHashes=True, dimensionsPreserved=True, allFramesNonempty=True,
                              pixelReproduction=True, unityMetadataPreserved=True),
                  runtimeVisualReview="Pending Unity/browser review")
    if args.apply:
        MANIFEST.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", "utf-8")
        width, cell_w, cell_h = 960, 320, 150
        sheet = Image.new("RGB", (width, math.ceil(len(previews) / 3) * cell_h), (32, 52, 43))
        d = ImageDraw.Draw(sheet)
        for i, (label, image) in enumerate(previews):
            x, y = i % 3 * cell_w, i // 3 * cell_h
            preview = fit(image, cell_w - 24, cell_h - 36)
            sheet.paste(preview, (x + (cell_w - preview.width) // 2, y + 8), preview)
            d.text((x + 4, y + cell_h - 21), label[:48], fill=(235, 228, 205))
        sheet.save(ROOT / "Builds/AssetReplacements/character-preview.png")
    print(f"CHARACTER_REPLACEMENTS_PASS: {len(rows)} PNG; {sum(r['frameCount'] for r in rows)} frames; metadata unchanged")


if __name__ == "__main__":
    main()
