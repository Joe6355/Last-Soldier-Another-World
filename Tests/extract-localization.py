from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[1]
CYR = re.compile('[\u0400-\u04ff]')

def csharp_strings(source):
    pattern = re.compile(r'(\$)?"')
    offset = 0
    while (match := pattern.search(source, offset)):
        line_start = source.rfind('\n', 0, match.start()) + 1
        prefix = source[line_start:match.start()]
        if prefix.lstrip().startswith('//'):
            offset = match.end()
            continue
        cursor = match.end()
        value = []
        number = 0
        while cursor < len(source):
            char = source[cursor]
            if char == '"':
                cursor += 1
                break
            if char == '\\' and cursor + 1 < len(source):
                value.append(source[cursor:cursor + 2]); cursor += 2
                continue
            if char == '{' and match.group(1) and source[cursor:cursor+2] != '{{':
                depth = 1; cursor += 1; in_string = False
                while cursor < len(source) and depth:
                    char = source[cursor]
                    if char == '\\' and in_string: cursor += 2; continue
                    if char == '"': in_string = not in_string
                    if not in_string:
                        if char == '{': depth += 1
                        if char == '}': depth -= 1
                    cursor += 1
                value.append('{' + str(number) + '}'); number += 1
                continue
            value.append(char); cursor += 1
        offset = cursor
        text = ''.join(value)
        try: text = json.loads('"' + text + '"')
        except ValueError: continue
        if CYR.search(text) and not re.search(r'Debug\.|\[(?:Header|Tooltip)|Log\(', prefix):
            yield text, source.count('\n', 0, match.start()) + 1

def yaml_strings(source):
    for match in re.finditer(r'^  (m_Text|m_text|itemName|destinationLabel): (.*(?:\n(?=    \S).*)*)', source, re.M):
        value = re.sub(r'\n    ', ' ', match.group(2)).strip()
        if value.startswith('"'):
            try: value = json.loads(re.sub(r'\\x([0-9a-fA-F]{2})', r'\\u00\1', value))
            except ValueError: continue
        elif value.startswith("'"):
            value = value.strip("'").replace("''", "'")
        if CYR.search(value): yield value, source.count('\n', 0, match.start()) + 1

def inventory():
    result = {}
    for path in sorted((ROOT / 'Assets/Scripts').rglob('*.cs')):
        if path.name in ('GameLocalization.cs',): continue
        source = path.read_text(encoding='utf-8', errors='replace')
        for value, line in csharp_strings(source):
            result.setdefault(value, []).append(f'{path.relative_to(ROOT)}:{line}')
    for path in [*(ROOT / 'Assets/Scenes').glob('*.unity'), *(ROOT / 'Assets/Prefabs').rglob('*.prefab')]:
        for value, line in yaml_strings(path.read_text(encoding='utf-8', errors='replace')):
            result.setdefault(value, []).append(f'{path.relative_to(ROOT)}:{line}')
    return result

if __name__ == '__main__':
    result = inventory()
    output = ROOT / 'Builds/LocalizationInventory.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    for index, value in enumerate(result): print(f'{index}: {json.dumps(value, ensure_ascii=False)}')
    print(f'TOTAL {len(result)}')
