"""Run saved runtime UI checks through the existing local Codely bridge."""
from pathlib import Path
import json
import subprocess
import sys

root = Path(__file__).resolve().parents[3]
folder = Path(__file__).resolve().parent
phase = sys.argv[1]
files = {"guided": "validate-guided-ui.csx", "finale-start": "validate-finale-start.csx", "finale-finish": "validate-finale-finish.csx", "regression": "validate-regression.csx"}
script = (folder / "ui-common.csx").read_text(encoding="utf-8-sig") + (folder / files[phase]).read_text(encoding="utf-8-sig")
if phase == "guided" and len(sys.argv) == 4:
    script = script.replace("int firstLevel=21,lastLevel=29;", f"int firstLevel={int(sys.argv[2])},lastLevel={int(sys.argv[3])};")
request = root / "Tools/UiCompileCheck/bin/request-banners-ui.json"
request.write_text(json.dumps({"type": "execute_csharp_script", "params": {"execution_mode": "play", "enable_repl": False, "script": script, "summary": "Verify Tattered Banners " + phase}}, ensure_ascii=False), encoding="utf-8")
subprocess.run([sys.executable, str(root / "Tools/UiCompileCheck/bin/codely_request.py"), str(request)], cwd=root, check=True, stdout=subprocess.DEVNULL)
result = json.loads((root / "Tools/UiCompileCheck/bin/bridge-last-response.json").read_text(encoding="utf-8-sig"))
data = result.get("data", {})
print(str(data.get("message", result.get("message")))[:3000])
if not result.get("success") or not data.get("success"):
    raise SystemExit(1)
