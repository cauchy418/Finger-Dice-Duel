"""Prepare the four generated tower plates; never writes Unity metadata."""
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Art/Production/mage-tower-v1'

def main():
    manifest = json.loads((ART / 'generation-manifest.json').read_text(encoding='utf-8'))
    selected = []
    for item in manifest['assets']:
        source = ART / 'sources' / item['source']
        target = ART / 'selected' / item['selected']
        target.parent.mkdir(parents=True, exist_ok=True)
        with Image.open(source) as original:
            if item['kind'] == 'background':
                assert abs(original.width / original.height - 16 / 9) < .01
                shutil.copy2(source, target)
            else:
                rgba = original.convert('RGBA')
                assert rgba.getchannel('A').getextrema()[0] == 0, source
                rgba = rgba.crop(rgba.getchannel('A').getbbox())
                rgba.thumbnail((480, 480), Image.Resampling.LANCZOS)
                canvas = Image.new('RGBA', (512, 512))
                canvas.alpha_composite(rgba, ((512-rgba.width)//2, (512-rgba.height)//2))
                canvas.save(target)
        with Image.open(target) as image:
            selected.append(dict(id=item['id'], file=item['selected'],
                resource_key=item['selected'][:-4], size=list(image.size), mode=image.mode,
                sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
    (ART / 'selected-manifest.json').write_text(json.dumps(dict(status='runtime_integrated' if all((ROOT / 'Assets/Resources/Art/Battle/v1' / i['file']).is_file() for i in selected) else 'prepared_not_imported', assets=selected), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Prepared', len(selected), 'mage tower assets')

if __name__ == '__main__':
    main()
