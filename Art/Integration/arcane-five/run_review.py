from pathlib import Path
import json,subprocess,sys
root=Path(__file__).resolve().parents[3]
folder=Path(__file__).resolve().parent
helpers=(folder/'helpers.cs.txt').read_text(encoding='utf-8-sig')
script='using System.Linq;\n'+helpers+(folder/'review.cs.txt').read_text(encoding='utf-8-sig')
request=root/'Tools/UiCompileCheck/bin/request-arcane-five.json'
request.write_text(json.dumps({'type':'execute_csharp_script','params':{'execution_mode':'play','enable_repl':False,'script':script,'summary':'Verify five arcane roles, curse artwork, and teammate AI in the actual editor'}},ensure_ascii=False),encoding='utf-8')
result=subprocess.run([sys.executable,str(root/'Tools/UiCompileCheck/bin/codely_request.py'),str(request)],cwd=root,capture_output=True,text=True,encoding='utf-8')
response=(root/'Tools/UiCompileCheck/bin/bridge-last-response.json').read_text(encoding='utf-8-sig')
(folder/'engine-response.json').write_text(response,encoding='utf-8')
data=json.loads(response)
print(str(data.get('data',data))[:4000])
