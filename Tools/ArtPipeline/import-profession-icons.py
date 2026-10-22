"""Rebuild selected profession/subclass PNGs using authorized crop/resize only."""
import hashlib, json, shutil
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'Art/Production/profession-subclass-v1'

def main():
    inputs = json.loads((BASE / 'generated-inputs.json').read_text(encoding='utf-8'))
    entries = []
    for item in inputs:
        key = item['key']; group = item.get('group', 'Professions' if key == 'druid' else 'Subclasses')
        source = BASE / 'source' / (key + '.png')
        source.parent.mkdir(parents=True, exist_ok=True)
        origin = ROOT / item['path']
        if origin.resolve() != source.resolve(): shutil.copy2(origin, source)
        prompt = BASE / 'prompts' / (key + '.txt'); prompt.parent.mkdir(exist_ok=True)
        prompt.write_text(item['prompt'], encoding='utf-8')
        runtime = ROOT / 'Assets/Resources/Art/Battle/v1' / group / (key + '.png')
        previous = BASE / 'previous' / group / runtime.name
        legacy_previous = BASE / 'previous' / runtime.name
        if runtime.exists() and not previous.exists() and not legacy_previous.exists():
            previous.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(runtime, previous)
        im = Image.open(source).convert('RGBA'); bounds = im.getchannel('A').getbbox()
        cut = im.crop(bounds); cut.thumbnail((456,456), Image.Resampling.LANCZOS)
        out = Image.new('RGBA', (512,512)); out.paste(cut, ((512-cut.width)//2,(512-cut.height)//2))
        runtime.parent.mkdir(parents=True, exist_ok=True); out.save(runtime)
        entries.append(dict(id=key,group=group,source=source.relative_to(ROOT).as_posix(),
            path=runtime.relative_to(ROOT).as_posix(),bounds=bounds,
            sha256=hashlib.sha256(runtime.read_bytes()).hexdigest()))
    (BASE / 'painted-assets.json').write_text(json.dumps(dict(date='2026-10-23',tool='built-in image_gen',
        processing='Authorized alpha crop, centering and resize only',assets=entries),ensure_ascii=False,indent=2),encoding='utf-8')
    for item in inputs: item['path'] = 'Art/Production/profession-subclass-v1/source/' + item['key'] + '.png'
    (BASE / 'generated-inputs.json').write_text(json.dumps(inputs,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Imported',len(entries),'painted emblems; metadata untouched.')

if __name__ == '__main__': main()
