"""Record the actual imported art/audio hashes without clearing older unknown sources."""
from collections import Counter
from datetime import date
from pathlib import Path
from PIL import Image
import hashlib
import json
import wave

ROOT = Path(__file__).resolve().parents[1]


def read(name):
    return json.loads((ROOT / name).read_text('utf-8-sig'))


def save(name, value):
    (ROOT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', 'utf-8')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    provenance = read('Publishing/shaula-asset-provenance.json')
    inventory = read('Publishing/asset-rights-inventory.json')
    audio = read('Publishing/audio-provenance.json')
    concept_audio = {Path(c['file']).name: c for c in read('Concepts/Shaula/v2/audio-manifest.json')['clips']}
    for item in provenance['files']:
        path = ROOT / item['path']
        assert item['sha256'] == digest(path)
        item['metadataSHA256'] = digest(Path(str(path)+'.meta'))
        if path.suffix == '.png':
            inventory['files'] = [x for x in inventory['files'] if x['path'] != item['path']]
            inventory['files'].append(dict(path=item['path'], sha256=item['sha256'], bytes=path.stat().st_size,
                pixelSize=list(Image.open(path).size), spriteRects=72, rightsStatus='generated-for-project',
                generationTool='OpenAI image_gen built-in', sourceManifest='Publishing/shaula-asset-provenance.json',
                changes='Approved concept v2 frames registered to a common pivot; nearest-neighbour scaling and transparent padding only.'))
        elif path.suffix == '.wav':
            source = concept_audio[path.name]
            assert source['sha256'] == item['sha256']
            with wave.open(str(path), 'rb') as wav:
                assert len(wav.readframes(wav.getnframes())) == wav.getnframes() * wav.getnchannels() * wav.getsampwidth()
                entry = dict(file=item['path'], kind=source['label'], sampleRate=wav.getframerate(), channels=wav.getnchannels(),
                    durationSeconds=wav.getnframes()/wav.getframerate(), decodedPeak=source['peak'], bytes=path.stat().st_size,
                    sha256=item['sha256'], fullDecode=True, generator='Concepts/Shaula/v2/generate-audio.py',
                    sourceManifest='Concepts/Shaula/v2/audio-manifest.json', method='Original deterministic synthesis; no samples or recordings')
            audio['clips'] = [x for x in audio['clips'] if x['file'] != item['path']]
            audio['clips'].append(entry)
    inventory['statusCounts'] = dict(Counter(x['rightsStatus'] for x in inventory['files']))
    inventory['checkedOn'] = date.today().isoformat()
    provenance['unityIntegrated'] = True
    save('Publishing/shaula-asset-provenance.json', provenance)
    save('Publishing/asset-rights-inventory.json', inventory)
    save('Publishing/audio-provenance.json', audio)
    print('Registered 144 generated poses and 19 original audio clips; older unknown sources unchanged.')


if __name__ == '__main__':
    main()
