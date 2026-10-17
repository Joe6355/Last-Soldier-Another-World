"""Rebuild the game's audio from oscillators and seeded noise, without sampled recordings.

Original assets are backed up outside Assets before replacement. Existing Unity GUIDs stay intact.
Requires numpy and ffmpeg (the existing CardCapture imageio_ffmpeg runtime is supported).
"""
from __future__ import annotations
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess
import sys
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
RATE = 32000
BACKUP = ROOT / "Builds/AssetReplacements/original-audio"


def ffmpeg_path():
    system = shutil.which("ffmpeg")
    if system:
        return system
    sys.path.insert(0, str(ROOT / "Builds/CardCapture/pytools"))
    import imageio_ffmpeg
    return imageio_ffmpeg.get_ffmpeg_exe()


def tone(freq, seconds, kind="pluck", gain=1.0):
    t = np.arange(max(1, round(seconds * RATE))) / RATE
    phase = 2 * math.pi * freq * t
    if kind == "pad":
        sound = np.sin(phase) + .18 * np.sin(phase * 2) + .06 * np.sin(phase * 3)
        envelope = np.minimum(t / .12, 1) * np.minimum((seconds - t) / .2, 1)
    elif kind == "bell":
        sound = np.sin(phase) + .22 * np.sin(phase * 2.006) + .1 * np.sin(phase * 3.99)
        envelope = np.exp(-t * 4 / seconds) * np.minimum(t / .004, 1)
    elif kind == "bass":
        sound = np.sin(phase) + .2 * np.sin(phase * 2)
        envelope = np.exp(-t * 3 / seconds) * np.minimum(t / .012, 1)
    else:
        sound = np.sin(phase) + .35 * np.sin(phase * 2) + .12 * np.sin(phase * 3)
        envelope = np.exp(-t * 5 / seconds) * np.minimum(t / .003, 1)
    return (sound * envelope * gain).astype(np.float64)


def hz(midi):
    return 440 * 2 ** ((midi - 69) / 12)


def add(buffer, sound, at, pan=0, loop=False):
    start = round(at * RATE)
    weights = [math.sqrt((1 - pan) / 2), math.sqrt((1 + pan) / 2)]
    indices = np.arange(len(sound)) + start
    if loop:
        indices %= len(buffer)
    else:
        valid = indices < len(buffer)
        indices, sound = indices[valid], sound[valid]
    for channel, weight in enumerate(weights):
        np.add.at(buffer[:, channel], indices, sound * weight)


def soften_edges(sound, duration=.006):
    frames = min(round(duration * RATE), len(sound) // 4)
    if frames:
        ramp = np.linspace(0, 1, frames)
        if sound.ndim == 2:
            ramp = ramp[:, None]
        sound[:frames] *= ramp
        sound[-frames:] *= ramp[::-1]
    return sound


def music(name):
    # Original 16-bar themes: a different mode, rhythm and lead for each location.
    bpm = {"Menu": 78, "Home": 88, "Fite": 116}[name]
    beat = 60 / bpm
    buffer = np.zeros((round(64 * beat * RATE), 2), dtype=np.float64)
    chords = [[50, 53, 57], [46, 50, 53], [53, 57, 60], [48, 52, 55],
              [50, 53, 57], [55, 58, 62], [46, 50, 53], [48, 52, 57]]
    melody = [[74, 77, 81, 77], [70, 74, 77, 74], [77, 81, 84, 81], [72, 76, 79, 76],
              [74, 77, 81, 86], [79, 82, 86, 82], [77, 74, 70, 74], [76, 72, 69, 72]]
    rng = np.random.default_rng({"Menu": 107, "Home": 211, "Fite": 313}[name])
    for bar in range(16):
        chord = chords[bar % 8]
        for index, note in enumerate(chord):
            add(buffer, tone(hz(note + 12), 4.3 * beat, "pad", .027), bar * 4 * beat,
                pan=(index - 1) * .55, loop=True)
        for step in range(8):
            note = chord[step % 3] + (12 if step % 2 else 24)
            add(buffer, tone(hz(note), beat * 1.25, "pluck", .09 if name != "Fite" else .075),
                (bar * 4 + step * .5) * beat, pan=(-.4 if step % 2 else .4), loop=True)
        for step in range(4):
            note = melody[bar % 8][step] + (0 if bar < 8 else -12)
            if name == "Home" and step == 3:
                continue
            at = (bar * 4 + step + (.5 if name == "Fite" and step % 2 else 0)) * beat
            add(buffer, tone(hz(note), beat * 1.65, "bell" if name == "Menu" else "pluck", .105),
                at, pan=.12 * math.sin(bar), loop=True)
        for step in (0, 2):
            add(buffer, tone(hz(chord[0] - 12), beat * 1.7, "bass", .13),
                (bar * 4 + step) * beat, loop=True)
        if name == "Fite":
            for step in range(4):
                t = np.arange(round(.16 * RATE)) / RATE
                drum = np.sin(2 * math.pi * (64 * t + 22 * .025 * (1 - np.exp(-t / .025))))
                drum *= np.exp(-t * 28) * .12
                add(buffer, soften_edges(drum), (bar * 4 + step) * beat, loop=True)
            for step in range(8):
                noise = rng.normal(0, .015, round(.05 * RATE))
                noise -= np.convolve(noise, np.ones(7) / 7, mode="same")
                noise *= np.exp(-np.arange(len(noise)) / RATE * 80)
                add(buffer, soften_edges(noise), (bar * 4 + step * .5 + .25) * beat, pan=.25, loop=True)
    # A very short silent seam avoids clicks when Unity loops PCM at different sample rates.
    return soften_edges(buffer, .005), {"kind": "original 16-bar fantasy instrumental loop", "bpm": bpm, "bars": 16}


def sfx(path):
    name = path.stem.lower()
    seed = int(hashlib.sha256(str(path.relative_to(ROOT)).encode()).hexdigest()[:8], 16)
    rng = np.random.default_rng(seed)
    if name in {"pickitup", "apple-pay-succes"} or "monetki" in name:
        duration = .42
        sound = np.zeros(round(duration * RATE))
        notes = [84, 91, 96] if name != "pickitup" else [86, 93]
        for index, note in enumerate(notes):
            start = round(index * .065 * RATE)
            signal = tone(hz(note), duration - index * .065, "bell", .23)
            sound[start:start + len(signal)] += signal
        kind = "soft coin chime"
    elif any(word in name for word in ("heal", "spawn", "tp", "dead", "sos")):
        duration = 1.05 if "dead" in name else .85
        sound = np.zeros(round(duration * RATE))
        notes = [72, 76, 79, 84] if "dead" not in name else [79, 76, 72, 67]
        for index, note in enumerate(notes):
            start = round(index * .12 * RATE)
            signal = tone(hz(note), duration - index * .12, "bell", .14)
            sound[start:start + len(signal)] += signal
        kind = "gentle magical dissolve" if "dead" in name else "ascending restoration chime"
    elif "portalzone" in name:
        duration = 2.4
        t = np.arange(round(duration * RATE)) / RATE
        sound = (.17 * np.sin(2 * math.pi * 196 * t) + .09 * np.sin(2 * math.pi * 294 * t))
        sound *= np.sin(math.pi * t / duration) ** 2
        kind = "soft arcane resonance"
    elif any(word in name for word in ("vzryiv", "bomber")):
        duration = .65
        t = np.arange(round(duration * RATE)) / RATE
        noise = rng.normal(0, 1, len(t))
        noise = np.convolve(noise, np.ones(13) / 13, mode="same")
        sound = (.4 * noise + .2 * np.sin(2 * math.pi * (75 * t - 26 * t * t))) * np.exp(-t * 7)
        kind = "stylised magical burst"
    elif any(word in name for word in ("cow", "shee", "chicken", "babka")):
        duration = .65
        t = np.arange(round(duration * RATE)) / RATE
        base = 150 if "cow" in name else 360 if "shee" in name else 620
        frequency = base * (1 + .11 * np.sin(t * 32))
        phase = 2 * math.pi * np.cumsum(frequency) / RATE
        sound = (.2 * np.sin(phase) + .05 * np.sin(phase * 2)) * np.sin(math.pi * t / duration) ** 2
        kind = "cartoon creature warble, no voice sample"
    elif "slime" in name:
        duration = .24
        t = np.arange(round(duration * RATE)) / RATE
        sound = .32 * np.sin(2 * math.pi * (330 * t - 380 * t * t)) * np.sin(math.pi * t / duration) ** 2
        kind = "elastic bubble pop"
    elif "block" in name or "sheled" in name:
        duration = .25
        sound = tone(740, duration, "bell", .23) + tone(1045, duration, "bell", .08)
        kind = "light fantasy shield clink"
    else:
        duration = .15 if name == "shot" else .24
        t = np.arange(round(duration * RATE)) / RATE
        noise = rng.normal(0, 1, len(t))
        noise = np.convolve(noise, np.ones(5) / 5, mode="same")
        sound = noise * np.sin(math.pi * t / duration) ** 2 * .3
        if any(word in name for word in ("damage", "att", "934", "936")):
            sound += tone(220 + seed % 90, duration, "pluck", .16)
        else:
            sound += tone(500, duration, "pluck", .04)
        kind = "soft wood impact" if "damage" in name else "bow string or cloth swish"
    sound = soften_edges(sound)
    peak = np.max(np.abs(sound))
    if peak > .5:
        sound *= .5 / peak
    return sound, {"kind": kind, "seed": seed}


def write_wav(path, sound):
    channels = 1 if sound.ndim == 1 else sound.shape[1]
    pcm = np.round(np.clip(sound, -.95, .95) * 32767).astype('<i2')
    with wave.open(str(path), "wb") as stream:
        stream.setnchannels(channels); stream.setsampwidth(2); stream.setframerate(RATE)
        stream.writeframes(pcm.tobytes())


def main():
    ffmpeg = ffmpeg_path()
    files = sorted(p for p in (ROOT / "Assets/Sounds").rglob('*') if p.suffix.lower() in {'.wav', '.mp3', '.ogg'})
    if len(files) != 40:
        raise RuntimeError(f"Expected 40 source clips, found {len(files)}; review the inventory first")
    BACKUP.mkdir(parents=True, exist_ok=True)
    records = []
    for path in files:
        relative = path.relative_to(ROOT / "Assets/Sounds")
        original = BACKUP / relative
        if not original.exists():
            original.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, original)
        sound, details = music(path.stem) if relative.parts[0] == 'Song' else sfx(path)
        if np.max(np.abs(sound)) > .65:
            sound *= .65 / np.max(np.abs(sound))
        wav = BACKUP.parent / 'synth-work.wav'
        write_wav(wav, sound)
        if path.suffix.lower() == '.wav':
            shutil.copyfile(wav, path)
        else:
            subprocess.run([ffmpeg, '-v', 'error', '-y', '-i', str(wav), '-map_metadata', '-1',
                            '-codec:a', 'libmp3lame', '-q:a', '3', '-id3v2_version', '0', str(path)], check=True)
        decoded = subprocess.run([ffmpeg, '-v', 'error', '-i', str(path), '-f', 'f32le', '-codec:a', 'pcm_f32le', '-'],
                                 check=True, stdout=subprocess.PIPE).stdout
        samples = np.frombuffer(decoded, dtype='<f4')
        peak = float(np.max(np.abs(samples)))
        if not np.isfinite(samples).all() or peak >= .99 or not np.any(samples):
            raise RuntimeError(f"Invalid encoded audio: {path}")
        records.append({'file':str(path.relative_to(ROOT)).replace('\\','/'), **details,
                        'sampleRate':RATE, 'channels':1 if sound.ndim == 1 else 2,
                        'durationSeconds':len(sound)/RATE, 'decodedPeak':round(peak,6),
                        'bytes':path.stat().st_size, 'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                        'originalSha256':hashlib.sha256(original.read_bytes()).hexdigest(),
                        'fullDecode':True})
        print(relative, 'OK', round(len(sound)/RATE, 3), 's', flush=True)
    result={'createdOn':'2026-10-17','method':'Original additive/FM synthesis and deterministic noise; no recordings or samples',
            'generator':'Tools/GenerateGameAudio.py','originalBackup':'Builds/AssetReplacements/original-audio',
            'unityMetaChanged':False,'listeningReview':'Not yet performed','clips':records}
    (ROOT/'Publishing/audio-provenance.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (BACKUP/'manifest.json').write_text(json.dumps([{'file':r['file'],'sha256':r['originalSha256']} for r in records],indent=2),encoding='utf-8')
    (ROOT/'Publishing/AUDIO_PROVENANCE.md').write_text(
        '# Происхождение нового аудио\n\nВсе 40 клипов в `Assets/Sounds` заменены 17 октября 2026 собственным процедурным синтезом. '
        'Внешние звукозаписи и музыкальные сэмплы не использовались. Генератор: `Tools/GenerateGameAudio.py`. '
        'Существующие пути и Unity GUID сохранены. Исходные неизвестные записи сохранены только вне Assets в ignored '
        '`Builds/AssetReplacements/original-audio` и не должны попадать в релиз.\n\n'
        'Музыка: три самостоятельные темы по 16 тактов (меню, лагерь, арена), синтез струнных, колокольчиков, баса и мягкой перкуссии. '
        'Эффекты: монеты, лук, ткань, щит, магическое восстановление, исчезновение и условные голоса животных; реалистичных криков нет.\n\n'
        'Техническая проверка: полное декодирование всех файлов FFmpeg; конечные ненулевые данные; пиковая амплитуда ниже 0,99; '
        'края исходного PCM плавно сведены к нулю. Параметры и SHA-256 каждого файла: `audio-provenance.json`. '
        '**Прослушивание и оценка баланса в браузере пока не выполнены.** Новый тембр отличается от старых записей.\n',encoding='utf-8')


if __name__ == '__main__':
    main()
