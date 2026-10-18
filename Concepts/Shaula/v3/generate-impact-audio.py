"""Original samples-free synthesis; only replaces Shaula's charge, discharge, impact and needle."""
from pathlib import Path
import hashlib
import importlib.util
import json
import math
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('synth', ROOT / 'Tools/GenerateGameAudio.py')
synth = importlib.util.module_from_spec(spec); spec.loader.exec_module(synth)
RATE = synth.RATE
rng = np.random.default_rng(2026101823)

def times(seconds): return np.arange(round(seconds * RATE)) / RATE
def noise(seconds, width=5):
    return np.convolve(rng.normal(0, 1, round(seconds * RATE)), np.ones(width) / width, 'same')
def drop(seconds, high, low, gain, decay):
    t = times(seconds)
    freq = low + (high - low) * np.exp(-t * 32)
    return gain * np.sin(2 * math.pi * np.cumsum(freq) / RATE) * np.exp(-t * decay)

clips = {}
t = times(1.1)
phase = 2 * math.pi * np.cumsum(190 + 1400 * (t / 1.1) ** 1.5) / RATE
clips['laser-charge'] = (.04 + .2 * (t / 1.1) ** 2) * (np.sin(phase) + .15 * np.sin(2.01 * phase)) + noise(1.1, 11) * .05 * (t / 1.1)
t = times(.42)
clips['laser-fire'] = drop(.42, 140, 46, .7, 14) + drop(.42, 2400, 210, .23, 38) + noise(.42, 2) * .38 * np.exp(-t * 58)
clips['laser-fire'] += np.sin(2 * math.pi * 560 * t + 2.5 * np.sin(2 * math.pi * 92 * t)) * .13 * np.exp(-t * 24)
t = times(.4)
clips['laser-impact'] = drop(.4, 195, 56, .55, 17) + noise(.4, 13) * .45 * np.exp(-t * 22) + synth.tone(1320, .4, 'bell', .07)
t = times(.12)
clips['needle'] = drop(.12, 1900, 640, .17, 32) + noise(.12, 4) * .1 * np.exp(-t * 42)

records = []
for name, sound in clips.items():
    sound = synth.soften_edges(sound, .002)
    peak = np.max(np.abs(sound))
    if peak > .8: sound *= .8 / peak
    target = ROOT / 'Assets/Bosses/Shaula/Audio' / (name + '.wav')
    synth.write_wav(target, sound)
    records.append(dict(file=target.relative_to(ROOT).as_posix(), label=name, sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
        seconds=len(sound) / RATE, peak=float(np.max(np.abs(sound))), rms=float(np.sqrt(np.mean(sound ** 2)))))
(OUT / 'audio-manifest.json').write_text(json.dumps(dict(method='Original deterministic synthesis, no third-party samples', clips=records), indent=2) + '\n', 'utf-8')
print('Shaula: four revised impact cues synthesized, no clipping.')
