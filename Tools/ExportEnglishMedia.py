"""Export unchanged PlayMode frames as localized Yandex card media.

The phone layout is the SDK's editor simulation, not an Android browser test.
No HUD, enemies, rewards or scenery are composited into the captured frames.
"""
from pathlib import Path
import hashlib
import argparse
import json
import shutil
import subprocess
import sys

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Builds/CardCapture/pytools'))
import imageio_ffmpeg


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--language', choices=('en', 'ru'), default='en')
    parser.add_argument('--mobile-stills-only', action='store_true')
    args = parser.parse_args()
    language = args.language
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    output = ROOT / 'Publishing/Card'
    capture = ROOT / 'Builds/CardCapture' / ('Russian' if language == 'ru' else 'English')
    report = {
        'createdOn': '2026-10-17',
        'localVersion': '0.0.0.13',
        'captureMethod': 'Real Unity 2022.3.47f1 PlayMode cameras and game HUD; automated movement and weapon firing on normal early waves in a fresh isolated profile.',
        'captureHelper': 'Tools/EditorPromoCapture.cs',
        'captureHelperSnapshot': (capture / 'capture-helper.cs.txt').relative_to(ROOT).as_posix(),
        'captureHelperSha256': digest(capture / 'capture-helper.cs.txt'),
        'exportHelper': 'Tools/ExportEnglishMedia.py',
        'language': language,
        'gameAreaRatio': 1.0,
        'processing': 'Unchanged full game frames; H.264 encoding only. Screenshots retain source PNG bytes. No overlays, crop, zoom or added gameplay.',
        'uploadedToYandex': False,
        'limitations': [
            'Mobile layout is the SDK editor simulator, not a capture or integration check on a physical phone.',
            'Silent video; audio balance has not been reviewed by listening.',
            'Visual review samples frames and does not establish sustained device FPS.',
        ],
        'videos': [], 'screenshots': [],
    }
    sheet = Image.new('RGB', (1280, 900), '#142b26')
    draw = ImageDraw.Draw(sheet)
    tile = 0
    for layout in ('pc', 'mobile'):
        folder = capture / layout
        result = ROOT / f'Builds/RatingValidation/promo-{"ru-" if language == "ru" else ""}{layout}.txt'
        capture_result = result.read_text('utf-8')
        frames = sorted(folder.glob('frame????.png'))
        stills_only = layout == 'mobile' and args.mobile_stills_only
        if stills_only:
            # The recorder writes a frame only while the player is alive. A normal
            # defeat can end a take after both requested stills without invalidating them.
            assert capture_result.startswith('PASS\n') or capture_result == 'FAIL\nPlayer defeated during recording'
            assert len(frames) > 480
            report['mobileStillCapture'] = {
                'resultFile': result.relative_to(ROOT).as_posix(),
                'captureResultExact': capture_result,
                'framesWrittenBeforeDefeat': len(frames),
                'use': 'Only unchanged, already captured frames preceding defeat; no mobile video exported.',
            }
        else:
            assert capture_result.startswith('PASS\n') and len(frames) == 600
        video = output / f'gameplay-{language}-{layout}.mp4'
        if not stills_only:
            subprocess.run([
            ffmpeg, '-hide_banner', '-loglevel', 'error', '-y',
            '-framerate', '30', '-start_number', '0', '-i', str(folder / 'frame%04d.png'),
            '-frames:v', '600', '-c:v', 'libx264', '-preset', 'medium', '-crf', '20',
            '-pix_fmt', 'yuv420p', '-movflags', '+faststart', '-an', str(video),
            ], check=True)
            reader = imageio_ffmpeg.read_frames(str(video), pix_fmt='rgb24')
            metadata = next(reader)
            decoded = sum(1 for _ in reader)
            assert decoded == 600 and metadata['size'] == (1280, 720)
            assert abs(metadata['fps'] - 30) < .01 and abs(metadata['duration'] - 20) < .05
            assert 'h264' in metadata['codec'] and not metadata.get('audio_codec')
            report['videos'].append({
            'file': video.relative_to(ROOT).as_posix(), 'layout': layout,
            'sourceDirectory': folder.relative_to(ROOT).as_posix(),
            'captureResult': result.relative_to(ROOT).as_posix(),
            'dimensions': [1280, 720], 'fps': 30, 'durationSeconds': 20,
            'frameCount': decoded, 'fullDecode': True, 'codec': 'H.264',
            'pixelFormat': 'yuv420p', 'fastStart': True, 'audio': False,
            'bytes': video.stat().st_size, 'sha256': digest(video),
            })
        selected_frames = (185, 480) if language == 'ru' and layout == 'mobile' else (180, 480)
        for index, number in enumerate(selected_frames, 1):
            source = folder / f'frame{number:04d}.png'
            target = output / f'screenshot-{layout}-{language}-{index}.png'
            with Image.open(source) as image:
                image.verify()
            shutil.copyfile(source, target)
            report['screenshots'].append({
                'file': target.relative_to(ROOT).as_posix(), 'layout': layout,
                'sourceFrame': source.relative_to(ROOT).as_posix(),
                'sourceTimeSeconds': number / 30, 'dimensions': [1280, 720],
                'bytes': target.stat().st_size, 'sha256': digest(target),
                'sourcePixelsUnchanged': digest(target) == digest(source),
            })
        for number in range(30, min(600, len(frames)), 60):
            source = folder / f'frame{number:04d}.png'
            with Image.open(source) as image:
                image.thumbnail((320, 180))
                x, y = tile % 4 * 320, tile // 4 * 180
                sheet.paste(image, (x, y))
                draw.rectangle((x, y + 162, x + 130, y + 179), fill='#142b26')
                draw.text((x + 4, y + 164), f'{layout} {number / 30:.0f}s', fill='white')
                tile += 1
        print(f'{layout}: unchanged stills exported' if stills_only else f'{layout}: {decoded} frames decoded, {video.stat().st_size} bytes', flush=True)
    sheet.save(capture / 'contact-sheet-final.png')
    (output / f'media-{language}-report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
