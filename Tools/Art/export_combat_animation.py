"""Re-export approved raster sources. Run from repository root; requires Pillow."""
from pathlib import Path
import hashlib
import json
import re
import uuid
from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
TEMPLATE = ROOT / 'Assets/Resources/Art/Battle/v1/Overlay/shield.png.meta'

def export(source, size):
    image = Image.open(ROOT / source).convert('RGBA')
    image = image.crop(image.getchannel('A').getbbox())
    image.thumbnail((int(size * .82), int(size * .82)), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA', (size, size))
    canvas.alpha_composite(image, ((size-image.width)//2, (size-image.height)//2))
    return canvas

def ensure_meta(path):
    meta = path.with_suffix('.png.meta')
    if not meta.exists():
        meta.write_text(re.sub(r'guid: .*', 'guid: ' + uuid.uuid4().hex,
                              TEMPLATE.read_text(encoding='utf-8-sig')), encoding='utf-8')

def main():
    for folder, group in [('hand-states-v2','states'), ('combat-animation-v1','weapons'), ('combat-animation-v1','effects')]:
        manifest = ROOT / 'Art/Production' / folder / (group+'-manifest.json')
        data = json.loads(manifest.read_text(encoding='utf-8-sig'))
        for row in data['assets']:
            out = ROOT / row['runtime']
            export(row['source'], row['size']).save(out)
            ensure_meta(out)
            row['sha256'] = hashlib.sha256(out.read_bytes()).hexdigest()
        manifest.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    shield = export('Art/Production/hand-states-v2/source/shield_broken.png',512)
    fragments = []
    for index in range(2):
        mask = Image.new('L', (512,512));draw=ImageDraw.Draw(mask)
        draw.polygon([(0,0),(511,0),(0,511)] if index==0 else [(511,0),(511,511),(0,511)],fill=255)
        image = shield.copy();image.putalpha(ImageChops.multiply(image.getchannel('A'),mask))
        out = ROOT / f'Assets/Resources/Art/Battle/v1/Animation/shield_fragment_{index+1}.png'
        image.save(out);ensure_meta(out)
        fragments.append(dict(key=out.stem,runtime=out.relative_to(ROOT).as_posix(),size=512,
            source='Art/Production/hand-states-v2/source/shield_broken.png',
            processing='diagonal alpha mask, common canvas and pivot',sha256=hashlib.sha256(out.read_bytes()).hexdigest()))
    (ROOT/'Art/Production/combat-animation-v1/fragments-manifest.json').write_text(
        json.dumps(dict(assets=fragments),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Exported 24 RGBA sprites; original state GUIDs preserved.')

if __name__ == '__main__':
    main()
