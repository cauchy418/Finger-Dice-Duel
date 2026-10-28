"""Synchronize the Chinese codex skill text with its canonical Markdown source."""
from pathlib import Path
import argparse
import json
import re

root = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--write', action='store_true', help='Update the generated C# text; otherwise check only.')
args = parser.parse_args()
source = (root / 'Docs/character-skill-copy-draft.md').read_text(encoding='utf-8-sig')
identity = (root / 'Assets/Scripts/Core/CharacterIdentity.cs').read_text(encoding='utf-8-sig')
roles = re.findall(r'new CharacterIdentity\("([^"]+)", "([^"]+)", "([^"]+)", "([^"]+)", "([^"]+)"\)', identity)
lines = [
    'using System.Collections.Generic;', '', 'namespace FingerDiceDuel.Characters', '{',
    '    /// <summary>Chinese profession/subclass skills, synchronized with Docs/character-skill-copy-draft.md.</summary>',
    '    public static class CharacterSkillText', '    {',
    '        private static readonly Dictionary<string, string> ById = new Dictionary<string, string>', '        {',
]
count = 0
for _, role_id, profession, name, english in roles:
    if profession == 'monk':
        continue
    match = re.search(r'^### [^\n]+·' + re.escape(name) + ' ' + re.escape(english) + r'\n(.*?)(?=\n##|\Z)', source, re.M | re.S)
    if match is None:
        raise SystemExit('Missing skill description: ' + role_id)
    text = match.group(1).strip().replace('**', '')
    if '被动技能（' in text or '待核对' in text:
        raise SystemExit('Unresolved skill copy: ' + role_id)
    lines.append('            { ' + json.dumps(role_id) + ', ' + json.dumps(text, ensure_ascii=False) + ' },')
    count += 1
lines.extend(['        };', '        public static bool TryGet(string id, out string text) => ById.TryGetValue(id, out text);', '    }', '}', ''])
generated = '\n'.join(lines)
target = root / 'Assets/Scripts/Characters/CharacterSkillText.cs'
if args.write:
    target.write_bytes(b'\xef\xbb\xbf' + generated.replace('\n', '\r\n').encode('utf-8'))
elif not target.exists() or target.read_text(encoding='utf-8-sig') != generated:
    raise SystemExit('Skill text is stale. Run python Tools/generate-skill-descriptions.py --write')
print('PASS: canonical skill descriptions synchronized for ' + str(count) + ' subclasses.')
