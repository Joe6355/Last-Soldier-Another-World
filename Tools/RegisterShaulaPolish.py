"""Track the new generated puppet parts and original sound synthesis without changing older rights records."""
from pathlib import Path
from collections import Counter
from datetime import date
import hashlib
import json
import wave
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
def sha(path): return hashlib.sha256((ROOT / path).read_bytes()).hexdigest()
def read(path): return json.loads((ROOT / path).read_text('utf-8-sig'))
def save(path, data): (ROOT / path).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', 'utf-8')

def main():
    image_path = 'Assets/Bosses/Shaula/rig-parts.png'
    inventory = read('Publishing/asset-rights-inventory.json')
    art = dict(path=image_path, sha256=sha(image_path), metadataSHA256=sha(image_path + '.meta'),
        bytes=(ROOT / image_path).stat().st_size, pixelSize=list(Image.open(ROOT / image_path).size), spriteRects=9,
        rightsStatus='generated-for-project', generationTool='OpenAI image_gen built-in',
        sourceManifest='Concepts/Shaula/v3/generation.json',
        changes='Generated 9-part puppet atlas; source pixels preserved. Individually measured sprite rectangles; runtime skeletal animation and palette shader.')
    inventory['files'] = [x for x in inventory['files'] if x['path'] != image_path] + [art]
    inventory['statusCounts'] = dict(Counter(x['rightsStatus'] for x in inventory['files']))
    inventory['checkedOn'] = date.today().isoformat()
    audio = read('Publishing/audio-provenance.json')
    changes = read('Concepts/Shaula/v3/audio-manifest.json')['clips']
    for item in changes:
        assert sha(item['file']) == item['sha256']
        path = ROOT / item['file']
        with wave.open(str(path), 'rb') as wav:
            assert len(wav.readframes(wav.getnframes())) == wav.getnframes() * wav.getnchannels() * wav.getsampwidth()
            record = dict(file=item['file'], kind=item['label'], sampleRate=wav.getframerate(), channels=wav.getnchannels(),
                durationSeconds=wav.getnframes() / wav.getframerate(), decodedPeak=item['peak'], bytes=path.stat().st_size,
                sha256=item['sha256'], fullDecode=True, generator='Concepts/Shaula/v3/generate-impact-audio.py',
                sourceManifest='Concepts/Shaula/v3/audio-manifest.json', method='Original deterministic synthesis; no third-party samples')
        audio['clips'] = [x for x in audio['clips'] if x['file'] != item['file']] + [record]
    source = read('Publishing/shaula-asset-provenance.json')
    for item in source['files']:
        item['sha256'] = sha(item['path']); item['metadataSHA256'] = sha(item['path'] + '.meta')
    source['files'] = [x for x in source['files'] if x['path'] != image_path] + [art]
    source['currentPresentation'] = dict(version='0.0.0.23', artManifest='Concepts/Shaula/v3/generation.json',
        puppetParts=9, oldMotionSheetsReferencedByPrefab=False, animations='Runtime articulation driven by traveled distance, charge, strike and recoil')
    save('Publishing/shaula-asset-provenance.json', source)
    save('Publishing/asset-rights-inventory.json', inventory)
    save('Publishing/audio-provenance.json', audio)
    print('Shaula v23 provenance: 9 generated sprite parts, 4 revised synthesized cues; older rights status unchanged.')

if __name__ == '__main__': main()
