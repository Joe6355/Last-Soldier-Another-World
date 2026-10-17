"""Verify the auditable source/provenance records against the final Unity packed-asset list."""
from pathlib import Path
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def read(name):
    return json.loads((ROOT / name).read_text('utf-8-sig'))


def sha(path):
    return hashlib.sha256((ROOT / path).read_bytes()).hexdigest()


def main():
    release = read('Publishing/build-report.json')
    assert sha(release['archive']) == release['sha256'], release['archive']
    with zipfile.ZipFile(ROOT / release['archive']) as archive:
        assert archive.testzip() is None
        for path, expected in release['fileHashes'].items():
            assert hashlib.sha256(archive.read(path)).hexdigest() == expected, path
    for item in release['sourceSnapshot']['files']:
        assert sha(item['path']) == item['sha256'], item['path']
    inventory = read('Publishing/asset-rights-inventory.json')
    character = read('Publishing/character-replacements.json')
    interface = read('Publishing/interface-sprite-provenance.json')
    audio = read('Publishing/audio-provenance.json')
    for manifest, key in [(character, 'path'), (interface, 'file')]:
        for item in manifest['files']:
            path = item[key]
            assert sha(path) == item['sha256'], path
            assert sha(path + '.meta') == (item.get('metadataSHA256') or item['metaSha256']), path + '.meta'
    for item in audio['clips']:
        assert sha(item['file']) == item['sha256'], item['file']
    for item in inventory['proofs']:
        assert sha(item['downloadPath']) == item['downloadSHA256'], item['downloadPath']
        if item.get('archiveEntry'):
            with zipfile.ZipFile(ROOT / item['downloadPath']) as archive:
                source_hash = hashlib.sha256(archive.read(item['archiveEntry'])).hexdigest()
        else:
            source_hash = item['downloadSHA256']
        assert item['exactBytesMatch'] and item['localSHA256'] == item['sourceSHA256'] == source_hash == sha(item['localPath']), item['localPath']
    packed = read('Publishing/release-packed-assets.json')['paths']
    rasters = [path for path in packed if Path(path).suffix.lower() in {'.png', '.jpg', '.jpeg'}]
    by_path = {item['path']: item for item in inventory['files']}
    unknown = [path for path in rasters if path.startswith('Assets/') and path not in by_path]
    assert not unknown, unknown
    assert not any('EmojiOne' in path or '/Examples & Extras/Textures/' in path for path in packed)
    assert not any('original-audio' in path or 'original-sprites' in path for path in packed)
    for path in rasters:
        if path in by_path:
            assert sha(path) == by_path[path]['sha256'], path
            assert by_path[path]['rightsStatus'] not in {'unconfirmed', 'project-created-source-record-pending'}, path
    assert 'Assets/Resources/AssetCredits.txt' in packed
    required = ['PTSerif-OFL.txt', 'LiberationSans-OFL.txt', 'CC0-1.0.txt', 'CC-BY-3.0.txt',
                'CC-BY-4.0.txt', 'CC-BY-SA-3.0.txt', 'CC-BY-SA-4.0.txt']
    for name in required:
        assert (ROOT / 'Builds/YandexGames/StreamingAssets/ThirdParty' / name).is_file(), name
    summary = {
        'checkedOn': '2026-10-17', 'result': 'PASS',
        'archive': release['archive'], 'archiveSha256': release['sha256'],
        'archiveFileCount': len(release['fileHashes']),
        'matchedSourceFiles': len(release['sourceSnapshot']['files']),
        'packedAssetsReport': 'Publishing/release-packed-assets.json',
        'packedAssetsReportSha256': sha('Publishing/release-packed-assets.json'),
        'packedSourceAssets': len(packed), 'packedRasterPaths': len(rasters),
        'unknownPackedRasterPaths': unknown, 'exactSourceProofs': len(inventory['proofs']),
        'licensedCharacterAtlases': len(character['files']), 'originalProceduralPngs': len(interface['files']),
        'originalSynthesizedAudioClips': len(audio['clips']),
        'bundledLicenseFiles': required,
        'scope': 'Verifies recorded provenance and hashes; not a legal warranty or a moderation decision.'
    }
    (ROOT / 'Publishing/release-assets-validation.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(summary, indent=2))


if __name__ == '__main__':
    main()
