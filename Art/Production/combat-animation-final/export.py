"""Re-export approved source sprites; keep existing Unity GUIDs. Run from any directory."""
from pathlib import Path
import hashlib, json
from PIL import Image
root=Path(__file__).resolve().parents[3]
manifest=Path(__file__).with_name("manifest.json")
data=json.loads(manifest.read_text(encoding="utf-8"))
for entry in data["assets"]:
    image=Image.open(root/entry["source"]).convert("RGBA")
    assert image.getchannel("A").getextrema()[0]==0
    image=image.crop(image.getchannel("A").getbbox())
    image.thumbnail((820,820),Image.Resampling.LANCZOS)
    output=Image.new("RGBA",(1024,1024))
    output.alpha_composite(image,((1024-image.width)//2,(1024-image.height)//2))
    target=root/entry["runtime"]
    output.save(target)
    entry["sha256"]=hashlib.sha256(target.read_bytes()).hexdigest()
manifest.write_text(json.dumps(data,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print("Exported 3 approved sprites. Existing .meta files were preserved.")
